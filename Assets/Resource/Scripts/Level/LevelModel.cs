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

    public override void Reset()
    {
        currentWave = 0;
        killCount = 0;
        aliveEnemyCount = 0;
        objectiveComplete = false;
    }
}

public enum LevelObjective
{
    EliminateAll,
    ActivateSwitches,
    ReachExtraction
}
