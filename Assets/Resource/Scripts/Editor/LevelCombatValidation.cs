#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>End-to-end combat checks, restricted to an isolated project.</summary>
public static class LevelCombatValidation
{
    private static LevelControl _level;
    private static PreparationConfig _config;
    private static int _stage, _results, _warEnds;
    private static double _deadline, _until;
    private static Vector3[] _positions;
    private static readonly List<ProjectileHitArgs> _hits = new List<ProjectileHitArgs>();
    public static void RunBatch()
    {
        if (!Path.GetFullPath(Application.dataPath).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use an isolated temporary project.");
        try
        {
            LevelPreparationBuilder.Build();
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            _stage = _results = _warEnds = 0; _hits.Clear();
            _deadline = EditorApplication.timeSinceStartup + 120;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Fail(error); }
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) Fail(new Exception(message + "\n" + stack));
    }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Debug.Log("COMBAT_CHECK: " + description);
    }
    private static void OnResult(GameEventArgs e) => _results++;
    private static void OnWarEnd(GameEventArgs e) => _warEnds++;
    private static void OnHit(GameEventArgs e) { if (e.data is ProjectileHitArgs hit) _hits.Add(hit); }
    private static EnemyActor[] Enemies() => UnityEngine.Object.FindObjectsOfType<EnemyActor>()
        .Where(a => a.Model.runId == _level.model.runId && !a.IsDead).OrderBy(a => a.transform.position.z).ToArray();
    private static void KillAll()
    {
        foreach (var enemy in Enemies())
            DamageSystem.Instance.Dispatch(new HitArgs(enemy.gameObject, _level.CombatPlayer.gameObject, 100000, false));
    }
    private static void Enter()
    {
        SetInput("StartCombatPressed", true); _level.Tick(0); SetInput("StartCombatPressed", false);
        Check(_level.model.phase == LevelPhase.Combat, "Enter starts formal combat");
        _level.SetPreparationCaptured(false);
    }
    private static void Restart()
    {
        _level.RestartPreparation(); SetInput("FireHeld", false); SetInput("MoveInput", Vector2.zero);
        Check(GameManager.Instance.State == GameState.Playing && Time.timeScale == 1 && _level.CombatPlayer.Hp == 100,
            "Restart restores global playing state and player health");
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Combat validation timed out.");
            if (_stage == 0)
            {
                _level = UnityEngine.Object.FindObjectOfType<LevelControl>();
                if (_level == null || !_level.IsInitialized) return;
                _config = (PreparationConfig)typeof(LevelControl).GetField("preparation", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_level);
                // This suite isolates the existing four-enemy combat mechanics; config/nest behavior has its own suite.
                _config.levelConfig = UnityEngine.Object.Instantiate(_config.levelConfig);
                _config.levelConfig.nests = new NestConfigEntry[0];
                _config.combat.nests = new NestSpawnBinding[0];
                _level.RestartPreparation();
                EventCenter.Instance.AddListener(LevelEvent.OnResult, OnResult);
                EventCenter.Instance.AddListener(GameEvent.War_End, OnWarEnd);
                EventCenter.Instance.AddListener(LevelEvent.OnProjectileHit, OnHit);
                Check(NavMesh.CalculateTriangulation().indices.Length > 0, "Scene navigation data loaded");
                Check(_level.model.phase == LevelPhase.EchoPreparation && _level.model.combatElapsed == 0 && _config.enemies.All(e => e.IsFrozen), "Preparation does not start combat clock or enemies");
                foreach (int fps in new[] { 30, 60, 120 })
                {
                    double clock = 0; int shots = 0;
                    for (int frame = 0; frame < fps * 60; frame++) shots += CombatShotClock.Advance(ref clock, 1f / fps, .06f);
                    Check(shots == 1000, "Boss clock emits 1000 shots in 60 seconds at " + fps + " FPS");
                }
                double stalled = 0;
                Check(CombatShotClock.Advance(ref stalled, 5, .06f) == 8 && stalled == 0, "Long frames have a bounded burst without a later backlog");
                _positions = Enemies().Select(e => e.transform.position).ToArray();
                Enter();
                Check(_level.PreparationEcho.model.segments.Count == 0 && _level.model.RemainingTime == 240 && _config.enemies.All(e => !e.IsFrozen), "Zero-segment start restores enemies and starts at 240 seconds");
                _level.Tick(.01f);
                float elapsed = _level.model.combatElapsed;
                GameManager.Instance.RequestPause(); _level.Tick(1);
                Check(_level.model.combatElapsed == elapsed && Enemies().All(e => e.Agent.isStopped), "Pause freezes countdown and every enemy agent");
                GameManager.Instance.RequestResume(); _level.Tick(.01f);
                Check(Enemies().All(e => !e.Agent.isStopped), "Resume restores enemy navigation");
                _until = EditorApplication.timeSinceStartup + .45;
                _stage = 1; return;
            }
            if (_stage == 1)
            {
                if (EditorApplication.timeSinceStartup < _until) return;
                Check(Enemies().Select((enemy, index) => Vector3.Distance(enemy.transform.position, _positions[index])).Any(d => d > .05f), "Ordinary enemies navigate toward the player");
                CheckProjectileDamage();
                CheckEchoReplay();
                CheckBossAndResults();
                CheckPendingWaves();
                CheckBossRetryAndOldRun();
                Restart(); Enter();
                _level.SpawnProjectile(new ProjectileOwner(_level.model.runId, _level.CombatPlayer.gameObject, ProjectileSource.Player),
                    _level.CombatPlayer.CameraPivot.position, Vector3.forward, 25, 65);
                _level.ExitLevel();
                _stage = 2; return;
            }
            if (_stage == 2)
            {
                if (UnityEngine.Object.FindObjectOfType<LevelControl>() != null) return;
                Check(GameManager.Instance.State == GameState.MainMenu && Time.timeScale == 1, "Exit returns to login state");
                Check(UnityEngine.Object.FindObjectsOfType<ProjectileActor>().Length == 0 && UnityEngine.Object.FindObjectsOfType<EnemyActor>().Length == 0, "Exit destroys enemies and independent bullets");
                Check(NavMesh.CalculateTriangulation().indices.Length == 0 && UnityEngine.EventSystems.EventSystem.current != null, "Exit removes navigation data and preserves working UI input");
                Check(_results == _warEnds, "Every final result emits exactly one War_End notification");
                Debug.Log("COMBAT_VALIDATION_OK");
                EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
                EventCenter.Instance.RemoveListener(LevelEvent.OnResult, OnResult);
                EventCenter.Instance.RemoveListener(GameEvent.War_End, OnWarEnd);
                EventCenter.Instance.RemoveListener(LevelEvent.OnProjectileHit, OnHit);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static void CheckProjectileDamage()
    {
        var enemy = Enemies()[0]; float hp = enemy.Hp;
        var origin = enemy.transform.position + Vector3.up * .9f + Vector3.back * 2;
        Physics.SyncTransforms();
        var bullet = _level.SpawnProjectile(new ProjectileOwner(_level.model.runId, _level.CombatPlayer.gameObject, ProjectileSource.Player), origin, Vector3.forward, 25, 10);
        bullet.Tick(.1f);
        Check(bullet.Model.hitTarget == enemy.gameObject && enemy.Hp == hp - 25, "Independent player bullet remembers collision target and applies damage");
        _level.ResolveProjectileHit(bullet);
        Check(enemy.Hp == hp - 25, "A repeated hit notification cannot apply damage twice");
        var player = _level.CombatPlayer;
        float playerHp = player.Hp;
        var hostile = _level.SpawnProjectile(new ProjectileOwner(_level.model.runId, enemy.gameObject, ProjectileSource.Enemy),
            player.transform.position + Vector3.up * .9f + Vector3.forward * 2, Vector3.back, 12, 10);
        hostile.Tick(.1f);
        Check(hostile.Model.hitTarget == player.gameObject && player.Hp == playerHp - 12,
            "Hostile bullet can hit the player collider on layer two and reports its enemy source");
        float interval = _config.combat.ordinary.fireInterval;
        _config.combat.ordinary.fireInterval = 999;
        _level.Tick(.6f);
        _config.combat.ordinary.fireInterval = interval;
        var next = _level.SpawnProjectile(new ProjectileOwner(_level.model.runId, enemy.gameObject, ProjectileSource.Enemy),
            player.transform.position + Vector3.up * .9f + Vector3.forward * 2, Vector3.back, 12, 10);
        next.Tick(.1f);
        Check(player.Hp == playerHp - 24, "Released mouse mode still advances the player's damage cooldown");
    }

    private static EchoSegment Segment(string id, Vector3 position, Vector3 origin)
    {
        return new EchoSegment { id = id, startTime = .2f, duration = 1,
            frames = new[] { new EchoFrame { time = 0, position = position, rotation = Quaternion.identity }, new EchoFrame { time = 1, position = position + Vector3.forward, rotation = Quaternion.identity } },
            shots = new[] { new EchoFireEvent { time = .1f, muzzlePosition = origin, fireDirection = Vector3.forward } } };
    }
    private static void CheckEchoReplay()
    {
        Restart();
        var player = _level.CombatPlayer;
        var parent = player.CameraPivot.parent;
        var enemy = Enemies()[0]; var position = enemy.transform.position;
        var echo = _level.PreparationEcho;
        echo.model.segments.Add(Segment("recorded-A", player.transform.position + Vector3.right, position + Vector3.up + Vector3.back * 4));
        echo.model.segments.Add(Segment("recorded-B", player.transform.position + Vector3.right * 2, position + Vector3.right * 2 + Vector3.up + Vector3.back * 4));
        echo.ToggleControl(); echo.Tick(.05f);
        Enter();
        Check(echo.model.segments.Count == 3 && !echo.model.isPreparing && player.CameraPivot.parent == parent && _level.model.combatElapsed == 0,
            "Enter saves an in-progress segment and restores body camera before starting the clock");
        _level.Tick(.1f);
        Check(UnityEngine.Object.FindObjectsOfType<PlayerEchoActor>().All(a => !a.name.StartsWith("Combat echo recorded-")), "Future-start segments do not play early");
        Check(enemy.Agent.Warp(position + Vector3.right * 2), "Enemy can move away from the position seen during recording");
        Physics.SyncTransforms();
        _hits.Clear(); _level.Tick(.2f);
        var actors = UnityEngine.Object.FindObjectsOfType<PlayerEchoActor>().Where(a => a.name.StartsWith("Combat echo recorded-")).ToArray();
        Check(actors.Length == 2 && actors.All(a => a.GetComponentsInChildren<Collider>().All(c => !c.enabled)), "Same-start overlapping combat echoes coexist without physical collision");
        var hit = _hits.FirstOrDefault(h => h.owner.kind == ProjectileSource.Echo && h.owner.segmentId == "recorded-B");
        Check(hit.target == enemy.gameObject && enemy.Hp == 35, "Recorded projectile hits the enemy's current position rather than a saved target");
        Check(hit.owner.instanceId != 0 && hit.owner.runId == _level.model.runId && hit.owner.segmentId == "recorded-B", "Echo bullet carries immutable run, source actor and segment identity");
        Check(_level.ProjectileCount > 0, "The other echo's missed shot continues as its own moving projectile");
    }

    private static void CheckBossAndResults()
    {
        Restart(); Enter(); KillAll();
        _level.model.combatElapsed = 239.99f; _level.Tick(.02f);
        var boss = Enemies().OfType<DestructionBossActor>().Single();
        Check(_level.model.phase == LevelPhase.Combat && _level.model.bossSpawned && boss.Hp == 10000 && _level.model.aliveEnemyCount == 1,
            "Expiry and clear on the same frame spawn exactly one 10000 HP Yoshimi boss before victory evaluation");
        Check(Vector3.Distance(boss.transform.position, _level.CombatPlayer.transform.position) > 2 && boss.Agent.isOnNavMesh, "Boss spawns at a reachable navigation point away from the player");
        int shots = boss.Model.shotsFired;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = (boss.transform.position + _level.CombatPlayer.transform.position) * .5f + Vector3.up;
        wall.transform.localScale = new Vector3(30, 8, 1); Physics.SyncTransforms();
        boss.Tick(.5f);
        Check(boss.Model.shotsFired == shots, "Walls block boss fire without banking a burst");
        wall.SetActive(false); UnityEngine.Object.Destroy(wall); Physics.SyncTransforms();
        CaptureBoss(boss);
        boss.ApplyDamage(10000); _level.Tick(.01f);
        int results = _results, ends = _warEnds; float time = _level.model.combatElapsed;
        _level.Tick(10); _level.Tick(10);
        Check(_level.model.result == LevelResult.Victory && _level.model.bossState == BossSpawnState.Defeated && _results == results && _warEnds == ends && _level.model.combatElapsed == time,
            "Boss defeat settles once, never respawns, and stops the battle clock");
        Check(_level.ProjectileCount == 0 && UnityEngine.Object.FindObjectsOfType<PlayerEchoActor>().All(a => !a.name.StartsWith("Combat echo ")), "Settlement clears bullets and echo actors");
        Restart(); Enter(); KillAll(); _level.Tick(.01f);
        Check(_level.model.result == LevelResult.Victory && !_level.model.bossSpawned, "Clearing all planned enemies early wins without a boss");
        Restart(); Enter(); KillAll(); _level.model.combatElapsed = 239.99f;
        _level.CombatPlayer.ApplyDamage(10000); _level.Tick(.02f);
        Check(_level.model.result == LevelResult.Defeat && !_level.model.bossSpawned && GameManager.Instance.State == GameState.GameOver, "Player death takes priority over both clear and expiry on the same frame");
    }

    private static void CheckPendingWaves()
    {
        _config.levelConfig.nests = new[] { new NestConfigEntry { nestId = "pending", firstWaveTimeSeconds = 10,
            waves = new[] { new NestWaveConfigEntry { enemyCount = 1 } } } };
        _config.combat.nests = new[] { new NestSpawnBinding { nestId = "pending", spawnPoints = _config.combat.bossSpawnPoints } };
        Restart(); Enter(); KillAll(); _level.Tick(.01f);
        Check(_level.model.phase == LevelPhase.Combat && !_level.model.normalSpawningComplete && _level.model.aliveEnemyCount == 0, "A pending ordinary wave prevents premature victory");
        _level.model.combatElapsed = 10; _level.Tick(.01f);
        Check(_level.model.aliveEnemyCount == 1 && _level.model.normalSpawningComplete, "A future wave registers its own independently driven actor");
        KillAll(); _level.Tick(.01f);
        Check(_level.model.result == LevelResult.Victory, "Clearing the final planned wave wins");
        _config.levelConfig.nests = new NestConfigEntry[0];
        _config.combat.nests = new NestSpawnBinding[0];
    }

    private static void CheckBossRetryAndOldRun()
    {
        Restart(); Enter();
        var prefab = _config.combat.bossPrefab; _config.combat.bossPrefab = null;
        KillAll(); _level.model.combatElapsed = 239.99f; _level.Tick(.02f);
        Check(_level.model.phase == LevelPhase.Combat && _level.model.bossState == BossSpawnState.Spawning && !_level.model.bossSpawned,
            "Failed boss creation blocks victory while retry is pending");
        _config.combat.bossPrefab = prefab; _level.Tick(1.01f);
        Check(_level.model.bossSpawned && Enemies().OfType<DestructionBossActor>().Count() == 1, "Restored boss resource succeeds on controlled retry");
        var oldRun = _level.model.runId;
        var bullet = _level.SpawnProjectile(new ProjectileOwner(oldRun, Enemies()[0].gameObject, ProjectileSource.Boss), Vector3.up * 30, Vector3.down, 10000, 65);
        Restart();
        bullet.Model.hitTarget = _level.CombatPlayer.gameObject; _level.ResolveProjectileHit(bullet);
        _level.NotifyEnemyDeath(oldRun, 123);
        Check(_level.CombatPlayer.Hp == 100 && _level.model.killCount == 0 && _level.ProjectileCount == 0 && _level.model.runId != oldRun,
            "Restart discards old-run bullets and death notifications");
        Restart();
        Check(_config.enemies.All(e => e.IsFrozen) && _level.model.combatElapsed == 0, "Repeated cleanup leaves a fresh frozen preparation stage");
    }

    private static void CaptureBoss(DestructionBossActor boss)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation;
        camera.transform.position = boss.transform.position + new Vector3(-3, 2, -4);
        camera.transform.LookAt(boss.transform.position + Vector3.up);
        var hud = UnityEngine.Object.FindObjectOfType<U_Level_MainView>(); hud.Refresh();
        var canvas = hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .08f;
        var target = new RenderTexture(1600, 900, 24); var previous = RenderTexture.active;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        Directory.CreateDirectory("Validation"); File.WriteAllBytes("Validation/LevelCombat.png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; camera.transform.SetPositionAndRotation(position, rotation);
    }
    private static void SetInput(string name, object value) => typeof(InputManager).GetProperty(name).SetValue(InputManager.Instance, value);
    private static void Fail(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
        Debug.LogError("COMBAT_VALIDATION_FAILED: " + error); EditorApplication.Exit(1);
    }
}
#endif
