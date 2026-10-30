#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Config-to-scene runtime checks in a disposable project only.</summary>
public static class LevelConfigIntegrationValidation
{
    private static double _deadline;
    private static bool _checked;

    public static void RunBatch()
    {
        if (!Path.GetFullPath(Application.dataPath).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use an isolated temporary project.");
        try
        {
            LevelPreparationBuilder.BindConfiguration();
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            _deadline = EditorApplication.timeSinceStartup + 90;
            _checked = false;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Fail(error); }
    }

    private static void Tick()
    {
        if (_checked || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Config integration validation timed out.");
            var level = UnityEngine.Object.FindObjectOfType<LevelControl>();
            if (level == null || !level.IsInitialized) return;
            _checked = true;
            var preparation = (PreparationConfig)typeof(LevelControl).GetField("preparation", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(level);
            var asset = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPreparationBuilder.ConfigPath);
            Check(preparation.levelConfig == asset, "Scene Level 1 directly references the configured asset");
            Check(level.model.durationSeconds == asset.durationSeconds && level.PreparationEcho.model.echoLimit == asset.echoLimit,
                "Actual asset duration and quota are read on entry");
            Check(preparation.enemies.Length == 4 && preparation.enemies.All(e => e.IsFrozen), "Four authored enemies are retained in preparation");

            // All fixture changes are made on an in-memory clone, never on the shared asset.
            var fixture = UnityEngine.Object.Instantiate(asset);
            preparation.levelConfig = fixture;
            fixture.durationSeconds = 137; fixture.echoLimit = 2;
            fixture.nests = new[]
            {
                new NestConfigEntry { nestId = "nest_a", waves = new[] { Wave(2, 0), Wave(3, 2) } },
                new NestConfigEntry { nestId = "nest_b", firstWaveTimeSeconds = 1, waves = new[] { Wave(1, 0), Wave(1, 4) } }
            };
            preparation.combat.ordinary.damage = preparation.combat.boss.damage = 0;
            level.RestartPreparation();
            var echo = level.PreparationEcho;
            Check(level.model.durationSeconds == 137 && echo.model.levelDuration == 137 && echo.model.echoLimit == 2,
                "Changed config values drive both countdown and echo timeline");
            Check(echo.ToggleControl() && echo.model.RemainingEchoes == 1, "An active recording reserves one use");
            echo.SaveSegment();
            Check(echo.ToggleControl() && echo.model.RemainingEchoes == 0, "The final available use can still record");
            Check(echo.ToggleControl() && !echo.model.isRecording, "Saving remains allowed at the quota limit");
            Check(!echo.ToggleControl(), "A full quota rejects both repeated key and button creation requests");
            Check(echo.DeleteSegment(echo.model.segments[0].id) && echo.model.RemainingEchoes == 1 && echo.ToggleControl(),
                "Deleting a saved clip refunds a use and allows recording again");
            echo.SaveSegment();
            fixture.echoLimit = 0; level.RestartPreparation();
            Check(!echo.ToggleControl() && echo.model.RemainingEchoes == 0, "Zero quota disables recording");
            Check(level.StartCombat(), "Zero quota still permits entering combat without any echoes");
            fixture.echoLimit = 2; level.RestartPreparation();
            Check(echo.model.RemainingEchoes == 2 && echo.model.segments.Count == 0, "Restart restores the full configured quota");
            Check(level.StartCombat(), "Formal combat starts with scene and nest bindings");
            Check(level.model.aliveEnemyCount == preparation.enemies.Length + 2 && NestEnemies("nest_b").Length == 0,
                "Immediate nest enemies are additional to authored enemies; delayed nests do not spawn early");
            float pausedTime = level.model.combatElapsed;
            GameManager.Instance.RequestPause(); level.Tick(10);
            Check(level.model.combatElapsed == pausedTime && NestEnemies("nest_b").Length == 0, "Pause freezes nest and countdown clocks");
            GameManager.Instance.RequestResume();
            NestEnemies("nest_a")[0].ApplyDamage(100000);
            level.Tick(.5f);
            Check(NestEnemies("nest_a").Length == 1, "A partially cleared wave does not start its next wave");
            NestEnemies("nest_a")[0].ApplyDamage(100000);
            level.Tick(.5f);
            Check(NestEnemies("nest_b").Length == 1, "Other nests start on their own configured combat time");
            level.Tick(1.4f);
            Check(NestEnemies("nest_a").Length == 0, "The full clear delay is observed");
            level.Tick(.11f);
            Check(NestEnemies("nest_a").Length == 3 && NestEnemies("nest_b").Length == 1,
                "A nest advances after its own clear delay while other enemies remain alive");
            foreach (var enemy in LiveEnemies()) enemy.ApplyDamage(100000);
            level.Tick(.01f);
            Check(level.model.phase == LevelPhase.Combat && !level.model.normalSpawningComplete && level.model.aliveEnemyCount == 0,
                "A pending next wave prevents premature victory even with no live enemies");
            level.Tick(3.98f);
            Check(NestEnemies("nest_b").Length == 0, "The second nest keeps its distinct four-second clear delay");
            level.Tick(.02f);
            Check(NestEnemies("nest_b").Length == 1 && level.model.normalSpawningComplete,
                "The last configured wave spawns exactly its configured count");
            foreach (var enemy in LiveEnemies()) enemy.ApplyDamage(100000);
            level.Tick(.01f);
            Check(level.model.result == LevelResult.Victory && !level.model.bossSpawned, "Clearing all authored and configured enemies wins early");
            level.RestartPreparation();
            Check(level.model.phase == LevelPhase.EchoPreparation && level.model.combatElapsed == 0 &&
                preparation.enemies.All(e => e.IsFrozen) && NestEnemies("nest_a").Length == 0 && NestEnemies("nest_b").Length == 0,
                "Restart removes generated enemies, resets nest schedules and freezes authored enemies");
            Check(asset.durationSeconds == 240 && asset.echoLimit == 5 && asset.nests[0].waves.Length == 3,
                "Runtime counters and fixture edits leave the original config asset unchanged");
            Debug.Log("LEVEL_CONFIG_INTEGRATION_OK");
            EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Fail(error); }
    }

    private static EnemyActor[] LiveEnemies() => UnityEngine.Object.FindObjectsOfType<EnemyActor>().Where(e => !e.IsDead).ToArray();
    private static EnemyActor[] NestEnemies(string id) => LiveEnemies().Where(e => e.name.StartsWith(id + " - Wave ", StringComparison.Ordinal)).ToArray();
    private static NestWaveConfigEntry Wave(int count, float delay) => new NestWaveConfigEntry { enemyCount = count, delayAfterPreviousClearSeconds = delay };
    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
        Debug.Log("LEVEL_CONFIG_RUNTIME_CHECK: " + message);
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) Fail(new Exception(message + "\n" + stack));
    }
    private static void Fail(Exception error)
    {
        Application.logMessageReceived -= OnLog; EditorApplication.update -= Tick;
        Debug.LogException(error); EditorApplication.Exit(1);
    }
}
#endif
