public static class LevelEvent
{
    public const GameEvent OnCombatStarted = GameEvent.FormalLevel_CombatStarted;
    public const GameEvent OnEnemyDied = GameEvent.FormalLevel_EnemyDied;
    public const GameEvent OnBossSpawned = GameEvent.FormalLevel_BossSpawned;
    public const GameEvent OnResult = GameEvent.FormalLevel_Result;
    public const GameEvent OnProjectileHit = GameEvent.FormalLevel_ProjectileHit;
}

public readonly struct LevelResultArgs
{
    public readonly string runId;
    public readonly LevelResult result;
    public LevelResultArgs(string id, LevelResult value) { runId = id; result = value; }
}

public readonly struct EnemyDeathArgs
{
    public readonly string runId;
    public readonly int instanceId;
    public readonly bool isBoss;
    public EnemyDeathArgs(string run, int id, bool boss) { runId = run; instanceId = id; isBoss = boss; }
}
