using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public partial class LevelControl
{
    private readonly Dictionary<int, EnemyActor> _combatEnemies = new Dictionary<int, EnemyActor>();
    private readonly List<EnemyActor> _dynamicEnemies = new List<EnemyActor>();
    private readonly List<EnemyActor> _dyingEnemies = new List<EnemyActor>();
    private readonly List<ProjectileActor> _projectiles = new List<ProjectileActor>();
    private float _bossRetry, _playerFireCooldown;
    private DestructionBossActor _boss;
    private PlayerEchoActor _playerShotView;
    public PlayerControl CombatPlayer => preparation.player;
    public float BossHealth => _boss != null ? _boss.Hp : 0;
    public int ProjectileCount => _projectiles.Count;
    public bool IsCurrentCombat(string runId) => usePreparation && model != null && model.runId == runId &&
        model.phase == LevelPhase.Combat && model.result == LevelResult.None && GameManager.Instance.State == GameState.Playing;

    private EnemyActor InitialEnemy(PreparationEnemy shadow) => shadow.GetComponent<EnemyActor>();

    public bool StartCombat()
    {
        if (!usePreparation || model.phase != LevelPhase.EchoPreparation || GameManager.Instance.State != GameState.Playing) return false;
        preparation.echo.SaveSegment();
        var positions = new Vector3[preparation.enemies.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var actor = InitialEnemy(preparation.enemies[i]);
            if (actor == null || !actor.TryGetStartPosition(out positions[i]))
            { model.runtimeError = "Enemy spawn has no valid navigation data"; Debug.LogWarning(model.runtimeError); return false; }
        }
        preparation.player.ResetPose(_bodySpawn, _bodyRotation);
        model.phase = LevelPhase.Combat;
        model.combatElapsed = 0;
        model.currentWave = 1;
        model.runtimeError = null;
        model.enemyInstanceIds.Clear(); _combatEnemies.Clear();
        for (int i = 0; i < preparation.enemies.Length; i++)
        {
            preparation.enemies[i].Restore();
            var actor = InitialEnemy(preparation.enemies[i]);
            actor.Activate(positions[i]);
            RegisterEnemy(actor);
        }
        InitializeNests();
        TickNests();
        _playerFireCooldown = _bossRetry = 0;
        var shotView = new GameObject("Player shot presentation"); shotView.transform.SetParent(preparation.effectsRoot, false);
        _playerShotView = shotView.AddComponent<PlayerEchoActor>();
        preparation.echo.StartCombatPlayback(model.runId, FireEchoProjectile);
        SetPreparationCaptured(true);
        EventCenter.Instance.Dispatch(LevelEvent.OnCombatStarted, new ValueArgs<string>(model.runId));
        preparation.hud.Refresh();
        return true;
    }

    private void RegisterEnemy(EnemyActor actor)
    {
        if (actor == null || actor.Model.runId != model.runId || _combatEnemies.ContainsKey(actor.Model.instanceId)) return;
        _combatEnemies.Add(actor.Model.instanceId, actor);
        model.enemyInstanceIds.Add(actor.Model.instanceId);
        model.aliveEnemyCount = _combatEnemies.Count;
    }

    public void NotifyEnemyDeath(string runId, int instanceId)
    {
        if (model.runId != runId || model.phase != LevelPhase.Combat || !_combatEnemies.TryGetValue(instanceId, out var actor) || !actor.IsDead) return;
        _combatEnemies.Remove(instanceId); model.enemyInstanceIds.Remove(instanceId);
        _dyingEnemies.Add(actor);
        model.aliveEnemyCount = _combatEnemies.Count; model.killCount++;
        if (_enemyNests.TryGetValue(instanceId, out var nest))
        {
            nest.NotifyDeath(instanceId, model.combatElapsed);
            _enemyNests.Remove(instanceId);
        }
        if (actor.IsBoss) model.bossState = BossSpawnState.Defeated;
        EventCenter.Instance.Dispatch(LevelEvent.OnEnemyDied, new EnemyDeathArgs(runId, instanceId, actor.IsBoss));
    }

    private void TickFormalLevel(float dt)
    {
        if (_loadingNextLevel) return;
        if (_preparationInput.ConsumeQuickVictory()) { CompleteAndAdvance(); return; }
        if (_preparationInput.RestartPressed && model.phase != LevelPhase.Idle) { RestartPreparation(); return; }
        // Death is cosmetic: retire combat identity immediately, then finish the visible action.
        // Result screens stop gameplay time, so only the remaining death poses use real time there.
        if (!GameManager.Instance.IsPaused)
        {
            float presentationDt = model.phase == LevelPhase.Ended ? Time.unscaledDeltaTime : dt;
            for (int i = _dyingEnemies.Count - 1; i >= 0; i--)
                if (_dyingEnemies[i] == null || _dyingEnemies[i].TickDeathPresentation(presentationDt)) _dyingEnemies.RemoveAt(i);
        }
        if (model.phase == LevelPhase.EchoPreparation)
        {
            if (_preparationInput.StartCombatPressed) { StartCombat(); return; }
            TickPreparation(dt); return;
        }
        if (model.phase != LevelPhase.Combat) { preparation.hud.Refresh(); return; }
        if (_preparationInput.CursorTogglePressed) SetPreparationCaptured(!Captured);
        if (_preparationInput.PausePressed)
        {
            if (GameManager.Instance.IsPaused) GameManager.Instance.RequestResume();
            else GameManager.Instance.RequestPause();
        }
        if (GameManager.Instance.State != GameState.Playing)
        {
            foreach (var actor in _combatEnemies.Values) actor.SetRunning(false);
            preparation.hud.Refresh(); return;
        }
        if (preparation.player.IsDead) { Settle(LevelResult.Defeat); return; }
        if (dt <= 0) return;
        string run = model.runId;
        model.combatElapsed += dt;
        TickNests();
        if (model.runId != run || model.phase != LevelPhase.Combat) return;
        if (model.RemainingTime <= 0 && !model.bossSpawned)
        {
            model.bossState = BossSpawnState.Spawning;
            _bossRetry -= dt;
            if (_bossRetry <= 0) TrySpawnBoss();
        }
        if (model.runId != run || model.phase != LevelPhase.Combat) return;
        // Releasing the cursor disables input, but health timers and body physics still run.
        preparation.player.Tick(dt);
        TickPlayerWeapon(dt);
        // Snapshot: a projectile or event callback may synchronously kill an enemy.
        var enemies = new List<EnemyActor>(_combatEnemies.Values);
        foreach (var actor in enemies)
        {
            if (!IsCurrentCombat(model.runId) || preparation.player.IsDead) break;
            actor.Tick(dt);
        }
        preparation.echo.TickCombatPlayback(model.combatElapsed);
        if (model.runId != run || model.phase != LevelPhase.Combat) return;
        Physics.SyncTransforms();
        TickProjectiles(dt);
        if (model.runId != run || model.phase != LevelPhase.Combat) return;
        if (preparation.player.IsDead) Settle(LevelResult.Defeat);
        else if (model.normalSpawningComplete && model.bossState != BossSpawnState.Spawning && _combatEnemies.Count == 0)
            Settle(LevelResult.Victory);
        preparation.hud.Refresh();
    }

    private void TickPlayerWeapon(float dt)
    {
        _playerFireCooldown = Mathf.Max(0, _playerFireCooldown - dt);
        if (!Captured || !_preparationInput.FireHeld || _playerFireCooldown > 0 || preparation.player.IsDead) return;
        _playerFireCooldown = preparation.combat.playerFireInterval;
        var camera = preparation.player.CameraPivot;
        var origin = camera.TransformPoint(.22f, -.06f, .75f);
        var target = camera.position + camera.forward * preparation.combat.projectileRange;
        if (Physics.Raycast(camera.position, camera.forward, out var hit, preparation.combat.projectileRange, ~(1 << 2), QueryTriggerInteraction.Ignore)) target = hit.point;
        var shot = new EchoFireEvent { muzzlePosition = origin, fireDirection = (target - origin).normalized };
        _playerShotView.ShowShot(shot, preparation.combat.playerShotMaterial, false);
        SpawnProjectile(new ProjectileOwner(model.runId, preparation.player.gameObject, ProjectileSource.Player),
            shot.muzzlePosition, shot.fireDirection, preparation.combat.playerDamage, preparation.combat.projectileRange);
    }

    private void FireEchoProjectile(string runId, PlayerEchoActor actor, EchoSegment segment, EchoFireEvent shot)
    {
        if (!IsCurrentCombat(runId) || preparation.player.IsDead) return;
        SpawnProjectile(new ProjectileOwner(runId, actor.gameObject, ProjectileSource.Echo, segment.id), shot.muzzlePosition,
            shot.fireDirection, preparation.combat.playerDamage, preparation.combat.projectileRange);
    }

    public ProjectileActor SpawnProjectile(ProjectileOwner owner, Vector3 origin, Vector3 direction, float damage, float range)
    {
        if (!IsCurrentCombat(owner.runId) || preparation.player.IsDead || direction.sqrMagnitude < .001f) return null;
        var go = new GameObject(owner.kind + " bullet"); go.transform.SetParent(preparation.effectsRoot, false);
        var actor = go.AddComponent<ProjectileActor>();
        actor.Initialize(this, owner, origin, direction, damage, preparation.combat.projectileSpeed, range,
            owner.IsPlayerSide ? preparation.combat.playerShotMaterial : preparation.combat.enemyShotMaterial);
        _projectiles.Add(actor);
        return actor;
    }

    public void ResolveProjectileHit(ProjectileActor projectile)
    {
        var data = projectile.Model;
        if (!IsCurrentCombat(data.owner.runId) || data.hitTarget == null || data.hitReported) return;
        data.hitReported = true;
        var enemy = data.hitTarget.GetComponentInParent<EnemyActor>();
        var player = data.hitTarget.GetComponentInParent<PlayerControl>();
        if (data.damage > 0 && ((data.owner.IsPlayerSide && enemy != null && enemy.Model.runId == model.runId) ||
            (!data.owner.IsPlayerSide && player == preparation.player)))
            DamageSystem.Instance.Dispatch(new HitArgs(data.hitTarget, data.owner.source, data.damage, data.owner.kind == ProjectileSource.Echo));
        EventCenter.Instance.Dispatch(LevelEvent.OnProjectileHit, new ProjectileHitArgs(data.owner, data.hitTarget, data.hitPoint));
    }

    private void TickProjectiles(float dt)
    {
        var bullets = _projectiles.ToArray();
        foreach (var bullet in bullets)
        {
            if (preparation.player.IsDead || !IsCurrentCombat(model.runId)) break;
            if (bullet != null) bullet.Tick(dt);
        }
        for (int i = _projectiles.Count - 1; i >= 0; i--)
            if (_projectiles[i] == null || _projectiles[i].Model.finished)
            {
                if (_projectiles[i] != null) Destroy(_projectiles[i].gameObject);
                _projectiles.RemoveAt(i);
            }
    }

    private void TrySpawnBoss()
    {
        _bossRetry = 1;
        if (model.bossSpawned || model.phase != LevelPhase.Combat) return;
        GameObject created = null;
        try
        {
            if (preparation.combat.bossPrefab == null) throw new InvalidOperationException("Destruction Boss prefab is missing");
            Vector3 position = default; float farthest = 0;
            foreach (var point in preparation.combat.bossSpawnPoints ?? new Transform[0])
            {
                if (point == null || !NavMesh.SamplePosition(point.position, out var nav, 2, NavMesh.AllAreas)) continue;
                float distance = Vector3.Distance(nav.position, preparation.player.transform.position);
                if (distance <= 2 || distance <= farthest) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(nav.position, preparation.player.transform.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                position = nav.position; farthest = distance;
            }
            if (farthest == 0) throw new InvalidOperationException("No reachable boss spawn point away from the player");
            var actor = Instantiate(preparation.combat.bossPrefab, position, Quaternion.identity, preparation.effectsRoot);
            created = actor.gameObject;
            actor.ResetForRun(this, model.runId, preparation.player, preparation.combat.boss);
            actor.Activate(position); RegisterEnemy(actor); _dynamicEnemies.Add(actor); _boss = actor;
            model.bossSpawned = true; model.bossState = BossSpawnState.Active; model.runtimeError = null;
            EventCenter.Instance.Dispatch(LevelEvent.OnBossSpawned, new ValueArgs<string>(model.runId));
        }
        catch (Exception error)
        {
            if (created != null) Destroy(created);
            if (model.runtimeError != error.Message) Debug.LogWarning("Boss spawn retry: " + error.Message);
            model.runtimeError = error.Message;
        }
    }

    private void Settle(LevelResult result)
    {
        if (model.phase != LevelPhase.Combat || model.result != LevelResult.None) return;
        if (preparation.player.IsDead) result = LevelResult.Defeat;
        string run = model.runId;
        model.result = result; model.phase = LevelPhase.Ended; model.objectiveComplete = result == LevelResult.Victory;
        foreach (var enemy in _combatEnemies.Values) enemy.SetRunning(false);
        ClearProjectiles(); preparation.echo.StopCombatPlayback();
        SetPreparationCaptured(false);
        EventCenter.Instance.Dispatch(LevelEvent.OnResult, new LevelResultArgs(run, result));
        if (model.runId != run || model.phase != LevelPhase.Ended) return;
        EventCenter.Instance.Dispatch(GameEvent.War_End, new ValueArgs<bool>(result == LevelResult.Victory));
        if (model.runId != run || model.phase != LevelPhase.Ended || GameManager.Instance.State != GameState.Playing) return;
        if (result == LevelResult.Victory) GameManager.Instance.RequestVictory();
        else GameManager.Instance.RequestGameOver();
        preparation.hud.Refresh();
    }

    private void ClearProjectiles()
    {
        foreach (var bullet in _projectiles) if (bullet != null) { bullet.Stop(); Destroy(bullet.gameObject); }
        _projectiles.Clear();
        if (_playerShotView != null) { _playerShotView.gameObject.SetActive(false); Destroy(_playerShotView.gameObject); _playerShotView = null; }
    }

    private void CleanupCombat()
    {
        foreach (var enemy in _combatEnemies.Values) if (enemy != null) enemy.SetRunning(false);
        _combatEnemies.Clear(); ClearProjectiles();
        preparation.echo?.StopCombatPlayback();
        foreach (var enemy in _dynamicEnemies) if (enemy != null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
        _dynamicEnemies.Clear(); _boss = null; _bossRetry = _playerFireCooldown = 0;
        _dyingEnemies.Clear();
        ClearNests();
    }

    public void ExitLevel()
    {
        if (!usePreparation) return;
        var scene = gameObject.scene;
        CleanupPreparation();
        preparation.hud.gameObject.SetActive(false);
        if (SceneManager.sceneCount == 1)
            SceneManager.SetActiveScene(SceneManager.CreateScene("MainMenu"));
        foreach (var root in scene.GetRootGameObjects())
            foreach (var input in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
                input.gameObject.SetActive(false);
        UIViewManager.Instance.EnsureEventSystem();
        LoginControl.Instance.OpenLogin();
        // Gameplay content has its own scene; the persistent Frame and login remain loaded.
        if (scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
    }
}
