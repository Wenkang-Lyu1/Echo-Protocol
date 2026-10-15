using System;
using UnityEngine;

// ============================================================
// EchoPlayback -- the entity that plays back a recorded echo.
// Created and driven by EchoControl.
// Responsible for: position / rotation / animation / lifetime.
// ============================================================
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AnimDriver))]
public class EchoPlayback : MonoBehaviour
{
    public event Action<EchoPlayback> OnExpired;

    private EchoModel _timeline;
    private CharacterController _cc;
    private AnimDriver _anim;
    private float _playbackTime;
    private bool _started;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _anim = GetComponent<AnimDriver>();
    }

    public void Begin(EchoModel timeline, Vector3 deployPosition, Quaternion deployRotation)
    {
        _timeline = timeline;
        _playbackTime = 0f;
        _started = false;

        transform.position = deployPosition;
        transform.rotation = deployRotation;

        IsPlaying = true;
        gameObject.SetActive(true);

        // _anim?.Play<EchoAnimState.Recording>();
        
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Echo_PlaybackStart,
            data = this
        });
    }

    private void Update()
    {
        if (!IsPlaying || _timeline == null || _timeline.FrameCount == 0) return;

        if (!_started)
        {
            _started = true;
            // _anim?.Play<EchoAnimState.Playback>();
        }

                // Position sampling.
        if (EchoUtil.SampleFrame(_timeline.frames, _playbackTime, out var frame))
        {
            if (_cc != null)
                _cc.Move(frame.position - transform.position);
            else
                transform.position = frame.position;
            transform.rotation = frame.rotation;

            _anim?.SetMoveSpeed(frame.IsMoving ? 1f : 0f);
            _anim?.SetCrouch(frame.IsCrouching);

            // if (frame.IsReloading) _anim?.Play<EchoAnimState.Reload>();
            // else if (frame.IsFiring) _anim?.Play<EchoAnimState.Fire>();
        }
    }

    
    // Explicit teardown matching the framework lifecycle vocabulary.
    // EchoControl.ResetState calls Destroy(go) on pooled echoes; this
    // is the canonical release point for non-pooled instances.
    public void Destroy()
    {
        if (!IsPlaying && gameObject != null && !gameObject.activeSelf) return;
        IsPlaying = false;
        OnExpired?.Invoke(this);
        if (gameObject != null)
            UnityEngine.Object.Destroy(gameObject);
    }

public void Expire()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        gameObject.SetActive(false);
        OnExpired?.Invoke(this);
    }

    private void OnDisable()
    {
        IsPlaying = false;
    }
}

