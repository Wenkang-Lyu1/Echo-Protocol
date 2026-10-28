using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class LevelControl
{
    [SerializeField] private bool usePreparation;
    [SerializeField] private PreparationConfig preparation;
    private Vector3 _bodySpawn;
    private Quaternion _bodyRotation;
    private InputManager _preparationInput;
    public bool Captured { get; private set; }
    public EchoControl PreparationEcho => preparation.echo;

    private void Start()
    {
        if (usePreparation) ControlRegistry.Instance.InitializeAll();
    }

    private void InitializePreparation()
    {
        if (preparation == null || preparation.player == null || preparation.echo == null || preparation.echoVisual == null ||
            preparation.shadowMaterial == null || preparation.effectsRoot == null || preparation.hud == null ||
            preparation.enemies == null || preparation.enemies.Length == 0)
            throw new InvalidOperationException("Formal level preparation configuration is incomplete.");
        foreach (var enemy in preparation.enemies)
            if (enemy == null) throw new InvalidOperationException("Preparation enemy reference is missing.");
        _preparationInput = InputManager.Instance;
        preparation.player.Initialize();
        preparation.echo.InitializeForPreparation(preparation.player);
        _bodySpawn = preparation.player.transform.position;
        _bodyRotation = preparation.player.transform.rotation;
        preparation.hud.SetOwner(this);
        preparation.hud.Bind(preparation.echo.GetModel());
        preparation.hud.Open();
        GameManager.Instance.SetState(GameState.Playing);
        RestartPreparation();
    }

    public void RestartPreparation()
    {
        if (!usePreparation || preparation == null) return;
        CleanupPreparation();
        model.Reset();
        _active = false;
        _waveIndex = _spawnedInWave = 0;
        _waveTimer = _intervalTimer = 0;
        model.runId = Guid.NewGuid().ToString("N");
        preparation.player.ResetState();
        preparation.player.ResetPose(_bodySpawn, _bodyRotation);
        preparation.echo.BeginPreparation(preparation.player, preparation.echoVisual,
            preparation.shadowMaterial, preparation.effectsRoot);
        foreach (var enemy in preparation.enemies)
        {
            enemy.Freeze(preparation.shadowMaterial);
            model.enemyInstanceIds.Add(enemy.GetInstanceID());
        }
        model.aliveEnemyCount = model.enemyInstanceIds.Count;
        model.phase = LevelPhase.EchoPreparation;
        SetPreparationCaptured(true);
        preparation.hud.Refresh();
    }

    private void TickPreparation(float dt)
    {
        if (model.phase != LevelPhase.EchoPreparation) return;
        var input = InputManager.Instance;
        if (input.PausePressed) SetPreparationCaptured(!Captured);
        if (GameManager.Instance.IsPaused) return;
        bool typing = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
        if (Captured && !typing)
        {
            if (input.RestartPressed) { RestartPreparation(); return; }
            if (input.EchoPressed) preparation.echo.ToggleControl();
            if (input.EchoSpeedPressed) preparation.echo.CyclePreparationSpeed();
            preparation.echo.Tick(dt);
        }
        preparation.hud.Refresh();
    }

    public void SetPreparationCaptured(bool captured)
    {
        Captured = captured;
        if (_preparationInput != null) _preparationInput.EnableGameplay(captured);
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && usePreparation && IsInitialized) SetPreparationCaptured(false);
    }

    private void CleanupPreparation()
    {
        if (!usePreparation || preparation == null) return;
        preparation.echo?.ResetState();
        if (preparation.enemies != null)
            foreach (var enemy in preparation.enemies) if (enemy != null) enemy.Restore();
        model.phase = LevelPhase.Idle;
    }

    public override void ResetState()
    {
        if (usePreparation) RestartPreparation();
        else base.ResetState();
    }

    public override void Shutdown()
    {
        CleanupPreparation();
        if (usePreparation)
        {
            SetPreparationCaptured(false);
            if (_preparationInput != null) _preparationInput.EnableGameplay(true);
            preparation.player?.Shutdown();
            preparation.echo?.Shutdown();
        }
        base.Shutdown();
    }
}
