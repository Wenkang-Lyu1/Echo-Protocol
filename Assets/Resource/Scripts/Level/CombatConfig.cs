using System;
using UnityEngine;

[Serializable]
public sealed class EnemyCombatStats
{
    [Min(1)] public float health = 60;
    [Min(.1f)] public float speed = 3.5f;
    [Min(.01f)] public float fireInterval = 1.2f;
    [Min(0)] public float damage = 12;
    [Min(1)] public float range = 25;
    [Range(0, 45)] public float spreadDegrees = 2;
}

[Serializable]
public sealed class NestSpawnBinding
{
    public string nestId;
    public EnemyActor prefab;
    public Transform[] spawnPoints;
}

[Serializable]
public sealed class CombatConfig
{
    public EnemyCombatStats ordinary = new EnemyCombatStats();
    public EnemyActor ordinaryPrefab;
    public EnemyCombatStats boss = new EnemyCombatStats { health = 10000, speed = 7, fireInterval = .06f, range = 60, spreadDegrees = 5 };
    public DestructionBossActor bossPrefab;
    public Transform[] bossSpawnPoints;
    public NestSpawnBinding[] nests = new NestSpawnBinding[0];
    [Min(1)] public float playerDamage = 25;
    [Min(.01f)] public float playerFireInterval = .18f;
    [Min(1)] public float projectileSpeed = 70;
    [Min(1)] public float projectileRange = 65;
    public Material playerShotMaterial;
    public Material enemyShotMaterial;
}
