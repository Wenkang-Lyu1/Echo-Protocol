using System;
using UnityEngine;

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
            preparation.shadowMaterial == null || preparation.enemyShadowMaterial == null || preparation.effectsRoot == null || preparation.hud == null ||
            preparation.enemies == null)
            throw new InvalidOperationException("Formal level preparation configuration is incomplete.");
        foreach (var enemy in preparation.enemies)
            if (enemy == null) throw new InvalidOperationException("Preparation enemy reference is missing.");
        if (preparation.levelConfig == null) throw new InvalidOperationException("请先创建并绑定关卡配置资源。");
        preparation.levelConfig.Validate();
        if (preparation.combat == null || preparation.combat.bossPrefab == null)
            throw new InvalidOperationException("Formal combat configuration is incomplete.");
        foreach (var stats in new[] { preparation.combat.ordinary, preparation.combat.boss })
        {
            if (stats == null || stats.health <= 0 || stats.speed <= 0 ||
                float.IsNaN(stats.health + stats.speed) || float.IsInfinity(stats.health + stats.speed))
                throw new InvalidOperationException("Enemy combat parameters must be finite and positive.");
            stats.Weapon.Validate();
            if (stats.Weapon.projectile.material == null) throw new InvalidOperationException("Enemy bullet material is missing.");
        }
        preparation.combat.PlayerWeapon.Validate();
        if (preparation.combat.PlayerWeapon.projectile.material == null) throw new InvalidOperationException("Player bullet material is missing.");
        ValidateNestBindings();
        foreach (var enemy in preparation.enemies)
            if (InitialEnemy(enemy) == null) throw new InvalidOperationException("EnemyActor is missing from a formal enemy.");
        _preparationInput = InputManager.Instance;
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null && eventSystem.gameObject.scene != gameObject.scene)
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var localInput in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
                    localInput.gameObject.SetActive(false);
        preparation.player.Initialize();
        _playerWeapon = WeaponActor.BindFirstPerson(preparation.player);
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
        if (!usePreparation || preparation == null || _loadingNextLevel) return;
        Configuration.Validate();
        ValidateNestBindings();
        CleanupPreparation();
        GameManager.Instance.SetState(GameState.Playing);
        model.Reset();
        _active = false;
        _waveIndex = _spawnedInWave = 0;
        _waveTimer = _intervalTimer = 0;
        model.runId = Guid.NewGuid().ToString("N");
        model.durationSeconds = preparation.DurationSeconds;
        preparation.player.ResetState();
        preparation.player.ResetPose(_bodySpawn, _bodyRotation);
        _playerWeapon.Configure(this, new ProjectileOwner(model.runId, preparation.player.gameObject, ProjectileSource.Player), preparation.combat.PlayerWeapon);
        preparation.echo.SetRecordingWeapon(_playerWeapon);
        preparation.echo.BeginPreparation(preparation.player, preparation.echoVisual,
            preparation.shadowMaterial, preparation.effectsRoot, preparation.DurationSeconds, Configuration.echoSkill);
        foreach (var enemy in preparation.enemies)
        {
            InitialEnemy(enemy).ResetForRun(this, model.runId, preparation.player, preparation.combat.ordinary);
            enemy.Freeze(preparation.enemyShadowMaterial);
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
        if (input.CursorTogglePressed) SetPreparationCaptured(!Captured);
        if (GameManager.Instance.IsPaused) return;
        if (Captured)
        {
            if (input.EchoPressed) preparation.echo.ToggleControl();
            if (input.EchoSpeedPressed) preparation.echo.CyclePreparationSpeed();
        }
        if (Captured || preparation.echo.GetModel().isPreviewPlaying) preparation.echo.Tick(dt);
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
        CleanupCombat();
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
