#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class LevelConfigValidation
{
    public static void RunBatch()
    {
        try
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPreparationBuilder.ConfigPath);
            Check(config != null, "Level 1 asset imports as LevelConfig");
            config.Validate();
            var a = new NestWaveSchedule(new NestConfigEntry { nestId = "a", firstWaveTimeSeconds = 2,
                waves = new[] { Wave(2, 0), Wave(1, 5), Wave(1, 0) } });
            var b = new NestWaveSchedule(new NestConfigEntry { nestId = "b", firstWaveTimeSeconds = 4,
                waves = new[] { Wave(1, 0) } });
            int aCount = 0, bCount = 0;
            a.Tick(1, i => ++aCount);
            Check(aCount == 0, "First wave waits for configured combat time");
            a.Tick(2, i => ++aCount);
            a.NotifyDeath(1, 3);
            a.Tick(100, i => ++aCount);
            Check(aCount == 2 && !a.SpawningComplete, "A surviving enemy blocks the next wave even after a long wait");
            b.Tick(4, i => 100 + ++bCount);
            Check(bCount == 1 && b.SpawningComplete, "Other nests advance independently");
            a.NotifyDeath(2, 100);
            a.NotifyDeath(2, 102);
            a.Tick(104.9f, i => ++aCount);
            Check(aCount == 2, "Clear starts the full delay; duplicate deaths cannot reset it");
            a.Tick(105, i => ++aCount);
            Check(aCount == 3 && !a.SpawningComplete, "Next wave starts exactly after the clear delay");
            a.NotifyDeath(3, 106);
            a.Tick(106, i => ++aCount);
            Check(aCount == 4 && a.SpawningComplete, "Zero delay permits the next wave at the clear time");
            a.NotifyDeath(4, 107);
            a.Tick(200, i => ++aCount);
            Check(aCount == 4 && a.SpawningComplete, "Final-wave completion never creates an extra wave");

            var retry = new NestWaveSchedule(new NestConfigEntry { nestId = "retry", waves = new[] { Wave(2, 0), Wave(1, 1) } });
            retry.Tick(0, i => i == 0 ? (int?)10 : null);
            retry.NotifyDeath(10, 0);
            retry.Tick(10, i => null);
            Check(!retry.SpawningComplete && retry.CurrentWave == 1, "Partial spawn failure prevents a false wave clear");
            retry.Tick(11, i => 11);
            retry.NotifyDeath(11, 12);
            int attempts = 0;
            retry.Tick(12.9f, i => { attempts++; return 12; });
            Check(attempts == 0, "Retry success still requires all spawned enemies to die before the delay");
            retry.Tick(13, i => { attempts++; return 12; });
            Check(attempts == 1 && retry.SpawningComplete, "A failed spawn can recover without skipping or duplicating enemies");
            var empty = new NestWaveSchedule(new NestConfigEntry());
            Check(empty.SpawningComplete, "Empty nests finish immediately");
            Debug.Log("LEVEL_CONFIG_VALIDATION_OK");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static NestWaveConfigEntry Wave(int count, float delay) => new NestWaveConfigEntry
        { enemyCount = count, delayAfterPreviousClearSeconds = delay };
    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
        Debug.Log("LEVEL_CONFIG_CHECK: " + message);
    }
}
#endif
