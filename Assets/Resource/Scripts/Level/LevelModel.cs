using System;
using UnityEngine;

[Serializable]
public struct WaveConfig
{
    public int enemyCount;
    public float spawnInterval;
    public float delayBeforeStart;
    public EnemyType enemyType;
}

[Serializable]
public class LevelModel : ModelBase
{
    public string displayName;
    public LevelObjective objective = LevelObjective.EliminateAll;
    public int killRequirement;
    public string objectiveText;

        // Runtime
    public int currentWave;
    public int killCount;
    public int aliveEnemyCount;
    public bool objectiveComplete;
    public string runId;
    public LevelPhase phase;
    public float combatElapsed;
    public float durationSeconds;
    public bool bossSpawned;
    public BossSpawnState bossState;
    public bool normalSpawningComplete;
    public LevelResult result;
    public string runtimeError;
    public float RemainingTime => Mathf.Max(0, durationSeconds - combatElapsed);
    public System.Collections.Generic.List<int> enemyInstanceIds = new System.Collections.Generic.List<int>();

    public override void Reset()
    {
        currentWave = 0;
        killCount = 0;
        aliveEnemyCount = 0;
        objectiveComplete = false;
        runId = null;
        phase = LevelPhase.Idle;
        combatElapsed = 0;
        durationSeconds = 0;
        bossSpawned = false;
        bossState = BossSpawnState.Pending;
        normalSpawningComplete = false;
        result = LevelResult.None;
        runtimeError = null;
        enemyInstanceIds.Clear();
    }
}

public enum LevelPhase { Idle, EchoPreparation, Combat, Ended }
public enum LevelResult { None, Victory, Defeat }
public enum BossSpawnState { Pending, Spawning, Active, Defeated }

public enum LevelObjective
{
    EliminateAll,
    ActivateSwitches,
    ReachExtraction
}
