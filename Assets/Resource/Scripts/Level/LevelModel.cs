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
    public bool bossSpawned;
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
        bossSpawned = false;
        enemyInstanceIds.Clear();
    }
}

public enum LevelPhase { Idle, EchoPreparation, Combat, Ended }

public enum LevelObjective
{
    EliminateAll,
    ActivateSwitches,
    ReachExtraction
}
