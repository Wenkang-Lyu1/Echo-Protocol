using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local tutorial logic. The registry drives Tick; the player is a child module.</summary>
public sealed class TutorialControl : GlobalControl<TutorialControl, TutorialModel>
{
    [SerializeField] private PlayerControl player;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private TutorialEnemyView[] enemies;
    [SerializeField] private GameObject echoPrefab;
    [SerializeField] private Transform effectsRoot;
    [SerializeField] private Transform gun;
    [SerializeField] private Material shotMaterial;
    [SerializeField] private Material echoMaterial;
    [SerializeField] private U_Tutorial_MainView hud;
    [SerializeField] private float fireInterval = .18f;
    [SerializeField] private float damage = 20;
    [SerializeField] private float echoDuration = 6;
    [SerializeField] private float echoCooldown = 2;
    private readonly List<TutorialPose> _history = new List<TutorialPose>();
    private readonly List<Replay> _echoes = new List<Replay>();
    private float _clock, _fireCooldown;
    private Vector3 _spawn;
    private Quaternion _spawnRotation;
    private InputManager _input;
    private Vector3 _gunPosition;
    private bool _pendingShot;
    private Vector3 _shotOrigin, _shotDirection;

    private sealed class Replay
    {
        public GameObject visual;
        public TutorialPose[] frames;
        public float elapsed;
        public int cursor;
    }

    private void Start()
    {
        // Supports opening Scene_Tutorial directly, and an already initialized Frame.
        ControlRegistry.Instance.InitializeAll();
    }

    protected override void OnInitialize()
    {
        _input = InputManager.Instance;
        player.Initialize();
        _spawn = player.transform.position;
        _spawnRotation = player.transform.rotation;
        _gunPosition = gun.localPosition;
        model.totalEnemies = enemies.Length;
        model.Reset();
        for (int i = 0; i < enemies.Length; i++) enemies[i].Bind(this, i);
        hud.Bind(model);
        hud.Open();
        GameManager.Instance.SetState(GameState.Playing);
        SetCaptured(true);
    }

    public override void Tick(float dt)
    {
        if (_input.PausePressed) SetCaptured(!model.captured);
        if (!model.captured)
        {
            if (_input.FirePressed) SetCaptured(true);
            return;
        }
        if (_input.RestartPressed) { RestartTrial(); return; }
        if (dt <= 0) return;
        _clock += dt;
        _fireCooldown = Mathf.Max(0, _fireCooldown - dt);
        model.echoCooldown = Mathf.Max(0, model.echoCooldown - dt);
        model.hitFeedback = Mathf.Max(0, model.hitFeedback - dt);
        player.Tick(dt);
        model.moved |= Vector3.Distance(player.transform.position, _spawn) > 1;
        bool fired = _input.FireHeld && Fire();
        Record(fired);
        if (_input.EchoPressed) CreateEcho();
        TickEchoes(dt);
        model.complete = model.defeated == model.totalEnemies && model.echoesCreated > 0 && model.moved;
        gun.localPosition = Vector3.Lerp(gun.localPosition, _gunPosition, dt * 14);
        foreach (var enemy in enemies) enemy.Present(dt);
        hud.Refresh();
    }

    public bool Fire()
    {
        if (!model.captured || _fireCooldown > 0) return false;
        _fireCooldown = fireInterval;
        model.shots++;
        _pendingShot = true;
        _shotOrigin = playerCamera.transform.position;
        _shotDirection = playerCamera.transform.forward;
        gun.localPosition = _gunPosition + Vector3.back * .09f;
        Shoot(_shotOrigin, _shotDirection, false);
        return true;
    }

    private void Shoot(Vector3 origin, Vector3 direction, bool isEcho)
    {
        Vector3 end = origin + direction * 65;
        // Layer 2 contains the local body, first-person weapon and non-colliding echoes.
        if (Physics.Raycast(origin, direction, out var hit, 65, ~(1 << 2), QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            DamageSystem.Instance.Dispatch(new HitArgs(hit.collider.gameObject, player.gameObject, damage, isEcho));
            if (hit.collider.GetComponentInParent<TutorialEnemyView>() != null)
                model.hitFeedback = .14f;
        }
        var trail = new GameObject(isEcho ? "Echo shot" : "Shot");
        trail.transform.SetParent(effectsRoot, false);
        var line = trail.AddComponent<LineRenderer>();
        line.sharedMaterial = isEcho ? echoMaterial : shotMaterial;
        line.startWidth = .025f; line.endWidth = .008f;
        line.positionCount = 2;
        line.SetPosition(0, isEcho ? origin : playerCamera.transform.TransformPoint(.22f, -.18f, .45f));
        line.SetPosition(1, end);
        Destroy(trail, .07f);
    }

    private void Record(bool fired)
    {
        // One sample per frame also preserves every accepted shot exactly once.
        _history.Add(new TutorialPose { time = _clock, position = player.transform.position,
            rotation = player.transform.rotation, origin = _shotOrigin,
            direction = _shotDirection, fired = fired || _pendingShot });
        _pendingShot = false;
        int trim = 0;
        while (trim < _history.Count - 1 && _history[trim].time < _clock - echoDuration) trim++;
        if (trim > 0) _history.RemoveRange(0, trim);
        model.recordedSeconds = _history.Count < 2 ? 0 : _clock - _history[0].time;
    }

    public bool CreateEcho()
    {
        if (!model.captured || model.echoCooldown > 0 || _echoes.Count >= 3 || _history.Count == 0) return false;
        var visual = Instantiate(echoPrefab, effectsRoot);
        visual.name = "Akane - Echo " + (model.echoesCreated + 1);
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = echoMaterial;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // Immutable snapshot: later recordings and new echoes cannot alter this replay.
        var frames = _history.ToArray();
        visual.transform.SetPositionAndRotation(frames[0].position, frames[0].rotation);
        _echoes.Add(new Replay { visual = visual, frames = frames });
        model.echoesCreated++;
        model.activeEchoes = _echoes.Count;
        model.echoCooldown = echoCooldown;
        return true;
    }

    private void TickEchoes(float dt)
    {
        for (int i = _echoes.Count - 1; i >= 0; i--)
        {
            var replay = _echoes[i];
            replay.elapsed += dt;
            float time = replay.frames[0].time + replay.elapsed;
            while (replay.cursor < replay.frames.Length && replay.frames[replay.cursor].time <= time)
            {
                var frame = replay.frames[replay.cursor++];
                if (frame.fired) Shoot(frame.origin, frame.direction, true);
            }
            int index = Mathf.Clamp(replay.cursor - 1, 0, replay.frames.Length - 1);
            var a = replay.frames[index];
            var b = replay.frames[Mathf.Min(index + 1, replay.frames.Length - 1)];
            float t = b.time > a.time ? Mathf.InverseLerp(a.time, b.time, time) : 0;
            replay.visual.transform.SetPositionAndRotation(Vector3.Lerp(a.position, b.position, t), Quaternion.Slerp(a.rotation, b.rotation, t));
            if (replay.elapsed >= Mathf.Max(1.5f, replay.frames[replay.frames.Length - 1].time - replay.frames[0].time))
            {
                Destroy(replay.visual);
                _echoes.RemoveAt(i);
            }
        }
        model.activeEchoes = _echoes.Count;
    }

    public void ApplyEnemyDamage(int index, float amount)
    {
        if (index < 0 || index >= enemies.Length || model.enemyHealth[index] <= 0 || amount <= 0) return;
        model.enemyHealth[index] = Mathf.Max(0, model.enemyHealth[index] - amount);
        enemies[index].ShowHit(model.enemyHealth[index] <= 0);
        if (model.enemyHealth[index] <= 0) model.defeated++;
        model.complete = model.defeated == model.totalEnemies && model.echoesCreated > 0 && model.moved;
    }

    public void SetCaptured(bool captured)
    {
        model.captured = captured;
        _input.EnableGameplay(captured);
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
        hud.Refresh();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && IsInitialized) SetCaptured(false);
    }

    public void RestartTrial()
    {
        foreach (var replay in _echoes) if (replay.visual != null) Destroy(replay.visual);
        _echoes.Clear(); _history.Clear();
        model.Reset(); _clock = _fireCooldown = 0;
        _pendingShot = false;
        player.ResetPose(_spawn, _spawnRotation);
        foreach (var enemy in enemies) enemy.Restore();
        SetCaptured(true);
    }

    protected override void OnTeardown()
    {
        if (_input != null) _input.EnableGameplay(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        player?.Shutdown();
    }
}
