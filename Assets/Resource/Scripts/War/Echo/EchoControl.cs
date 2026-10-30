using System;
using UnityEngine;

// ============================================================
// EchoControl -- controller for the Echo system.
// Responsibilities:
//   1) Holds EchoModel (timeline data, active echo list).
//   2) Samples player behavior at 20 Hz (position / rotation / input / fire).
//   3) Deploys an echo to a designated position.
//   4) Drives EchoPlayback.
//   5) Replicates firing decisions (core of the repeat-fire tactic).
// ============================================================
public partial class EchoControl : GlobalControl<EchoControl, EchoModel>
{
        [Header("Configuration")]
    [SerializeField] private float sampleRate = 20f;
    [SerializeField] private int maxDurationSeconds = 8;
    [SerializeField] private Transform muzzle;
    [SerializeField] private EchoPlayback echoPrefab;
    [SerializeField] private LayerMask fireHitMask = -1;
    [SerializeField] private int poolSize = 4;

    private float _timer;
    private float _accumulator;
    private float _fixedStep;
    private Vector2 _lookAccum;
    private ObjectPool<EchoPlayback> _pool;

    // WarControl owns and ticks this sub-module.
    protected override bool AutoRegister => false;

    protected override void OnInitialize()
    {
        base.OnInitialize();
        _fixedStep = 1f / Mathf.Max(1f, sampleRate);
        if (muzzle == null)
        {
            // A late-registered level can initialize before the scene player's Awake.
            // Prefer the explicit body reference instead of creating a second singleton.
            var player = _body != null ? _body : PlayerControl.Instance;
            if (player != null) muzzle = player.CameraPivot;
        }

        if (echoPrefab != null)
            _pool = new ObjectPool<EchoPlayback>(echoPrefab, poolSize);

        EventCenter.Instance.AddListener(GameEvent.Echo_RecordStart, OnRecordStart);
        EventCenter.Instance.AddListener(GameEvent.Echo_RecordEnd, OnRecordEnd);
        EventCenter.Instance.AddListener(GameEvent.Echo_Deploy, OnDeploy);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Echo_RecordStart, OnRecordStart);
        EventCenter.Instance.RemoveListener(GameEvent.Echo_RecordEnd, OnRecordEnd);
        EventCenter.Instance.RemoveListener(GameEvent.Echo_Deploy, OnDeploy);
    }

    public override void Tick(float deltaTime)
    {
        if (_combatPlayback) return;
        if (model.isPreparing) { TickPreparation(deltaTime); return; }
        if (model.isRecording)
            RecordTick();

        if (model.isPlaying)
            PlaybackTick(deltaTime);
    }

        // ---- Recording ----
    private void RecordTick()
    {
        var input = InputManager.Instance;
        if (input != null)
            _lookAccum += input.LookInput;

        _accumulator += Time.deltaTime;
        while (_accumulator >= _fixedStep)
        {
            _accumulator -= _fixedStep;
            _timer += _fixedStep;
            Sample(_timer);

            EventCenter.Instance.Dispatch(new GameEventArgs
            {
                eventType = GameEvent.Echo_RecordTick,
                data = new ValueArgs<float>(_timer)
            });

            if (_timer >= maxDurationSeconds)
            {
                StopRecording();
                return;
            }
        }
    }

    private void Sample(float t)
    {
        var player = PlayerControl.Instance;
        if (player == null) return;

        var flags = EchoInputFlags.None;
        var input = InputManager.Instance;
        if (input != null)
        {
            if (input.MoveInput.sqrMagnitude > 0.01f) flags |= EchoInputFlags.Move;
            if (input.FireHeld) flags |= EchoInputFlags.Fire;
            if (input.CrouchHeld) flags |= EchoInputFlags.Crouch;
            if (input.SprintHeld) flags |= EchoInputFlags.Sprint;
        }

        var frame = new EchoFrame
        {
            time = t,
            position = player.transform.position,
            rotation = player.transform.rotation,
            lookDelta = _lookAccum,
            flags = flags
        };
        model.frames.Add(frame);

        if ((flags & EchoInputFlags.Fire) != 0 && muzzle != null)
        {
            model.fireEvents.Add(new EchoFireEvent
            {
                time = t,
                muzzlePosition = muzzle.position,
                fireDirection = muzzle.forward
            });
        }
    }

    private void OnRecordStart(GameEventArgs args)
    {
        if (model.isPreparing) return;
        if (model.isRecording) return;
        model.isRecording = true;
        model.recordDuration = 0f;
        model.frames.Clear();
        model.fireEvents.Clear();
        _timer = 0f;
        _accumulator = 0f;
        _lookAccum = Vector2.zero;
    }

    private void OnRecordEnd(GameEventArgs args)
    {
        if (model.isPreparing) return;
        StopRecording();
    }

    private void StopRecording()
    {
        if (!model.isRecording) return;
        model.isRecording = false;
        model.recordDuration = _timer;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Echo_RecordEnd,
            data = new ValueArgs<float>(_timer)
        });
    }

        // ---- Deployment ----
    private void OnDeploy(GameEventArgs args)
    {
        if (args.data is Vector3 pos)
            TryDeploy(pos);
    }

    public bool TryDeploy(Vector3 deployPosition)
    {
        if (model.isPreparing) return false;
        if (model.FrameCount == 0)
        {
                        DispatchFail("No recording data");
            return false;
        }

        if (model.ActiveEchoCount >= model.maxConcurrentEchoes)
        {
                        DispatchFail("Echo capacity reached");
            return false;
        }

        var playback = _pool != null ? _pool.Get() : null;
        if (playback == null)
        {
            if (echoPrefab != null)
                playback = Instantiate(echoPrefab);
        }

        if (playback == null) return false;

        var player = PlayerControl.Instance;
        var rotation = player != null
            ? player.transform.rotation
            : Quaternion.identity;

        playback.Begin(model, deployPosition, rotation);
        model.activeEchoes.Add(playback);
        playback.OnExpired += OnEchoExpired;

        model.isPlaying = true;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Echo_Deploy,
            data = new IntPairArgs(model.ActiveEchoCount, model.maxConcurrentEchoes)
        });
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Echo_CountChanged,
            data = new IntPairArgs(model.ActiveEchoCount, model.maxConcurrentEchoes)
        });

        return true;
    }

        // ---- Playback Driver ----
    private float _playbackTime;
    private int _fireCursor;

    private void PlaybackTick(float deltaTime)
    {
        if (model.FrameCount == 0) return;

        _playbackTime += deltaTime;
        model.playbackProgress = _playbackTime / model.recordDuration;

        ReplicateFireEvents(_playbackTime);

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Echo_PlaybackTick,
            data = new ValueArgs<float>(model.playbackProgress)
        });

        if (_playbackTime >= model.recordDuration)
        {
            model.isPlaying = false;
            _playbackTime = 0f;
            _fireCursor = 0;

            EventCenter.Instance.Dispatch(new GameEventArgs
            {
                eventType = GameEvent.Echo_PlaybackEnd,
                data = null
            });
        }
    }

        // Key mechanism: replicates the firing decisions captured during recording.
    private void ReplicateFireEvents(float t)
    {
        while (_fireCursor < model.fireEvents.Count && model.fireEvents[_fireCursor].time <= t)
        {
            var fire = model.fireEvents[_fireCursor];
            if (Physics.Raycast(fire.muzzlePosition, fire.fireDirection,
                    out var hit, 200f, fireHitMask, QueryTriggerInteraction.Ignore))
            {
                EventCenter.Instance.Dispatch(new GameEventArgs
                {
                    eventType = GameEvent.Weapon_Fired,
                    data = new HitArgs(hit.collider.gameObject, null, 0f, true)
                });
            }
            _fireCursor++;
        }
    }

    private void OnEchoExpired(EchoPlayback echo)
    {
        echo.OnExpired -= OnEchoExpired;
        model.activeEchoes.Remove(echo);
        if (_pool != null) _pool.Return(echo);

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Echo_Expired,
            data = null
        });
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Echo_CountChanged,
            data = new IntPairArgs(model.ActiveEchoCount, model.maxConcurrentEchoes)
        });
    }

    private void DispatchFail(string reason)
    {
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Echo_DeployFailed,
            data = new ValueArgs<string>(reason)
        });
    }

    public override void ResetState()
    {
        ClearPreparation();
        foreach (var e in model.activeEchoes)
            if (e != null) Destroy(e.gameObject);
        base.ResetState();
        _playbackTime = 0f;
        _fireCursor = 0;
    }
}
