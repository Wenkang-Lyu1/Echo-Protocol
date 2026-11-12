using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class EnemyCombatStats
{
    [Min(1)] public float health = 60;
    [Min(.1f)] public float speed = 3.5f;
    public WeaponAttributes weapon;
    [SerializeField, HideInInspector, FormerlySerializedAs("fireInterval")] private float legacyFireInterval = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("damage")] private float legacyDamage = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("range")] private float legacyRange = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("spreadDegrees")] private float legacySpread = -1;

    public WeaponAttributes Weapon
    {
        get
        {
            if (weapon == null) weapon = new WeaponAttributes { fireInterval = 1.2f, spreadDegrees = 2, shootingMoveMultiplier = 0,
                projectile = new ProjectileAttributes { damage = 12, range = 25 } };
            if (legacyFireInterval > 0) weapon.fireInterval = legacyFireInterval;
            if (legacyDamage >= 0) weapon.projectile.damage = legacyDamage;
            if (legacyRange > 0) weapon.projectile.range = legacyRange;
            if (legacySpread >= 0) weapon.spreadDegrees = legacySpread;
            legacyFireInterval = legacyDamage = legacyRange = legacySpread = -1;
            return weapon;
        }
    }

    // Compatibility for existing scene builders and callers. New code edits Weapon and its bullet attributes.
    public float fireInterval { get => Weapon.fireInterval; set => Weapon.fireInterval = value; }
    public float damage { get => Weapon.projectile.damage; set => Weapon.projectile.damage = value; }
    public float range { get => Weapon.projectile.range; set => Weapon.projectile.range = value; }
    public float spreadDegrees { get => Weapon.spreadDegrees; set => Weapon.spreadDegrees = value; }
}

[Serializable]
public sealed class NestSpawnBinding
{
    public string nestId;
    public EnemyActor prefab;
    public Transform[] spawnPoints;
}

[Serializable]
public sealed class CombatConfig : ISerializationCallbackReceiver
{
    public EnemyCombatStats ordinary = new EnemyCombatStats();
    public EnemyActor ordinaryPrefab;
    public EnemyCombatStats boss = new EnemyCombatStats { health = 10000, speed = 7,
        weapon = new WeaponAttributes { fireInterval = .06f, spreadDegrees = 5, shootingMoveMultiplier = .7f,
            projectile = new ProjectileAttributes { damage = 12, range = 60 } } };
    public DestructionBossActor bossPrefab;
    public Transform[] bossSpawnPoints;
    public NestSpawnBinding[] nests = new NestSpawnBinding[0];
    public WeaponAttributes playerWeapon;
    [SerializeField, HideInInspector, FormerlySerializedAs("playerDamage")] private float legacyPlayerDamage = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("playerFireInterval")] private float legacyPlayerFireInterval = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("projectileSpeed")] private float legacyProjectileSpeed = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("projectileRange")] private float legacyProjectileRange = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("playerShotMaterial")] private Material legacyPlayerMaterial;
    [SerializeField, HideInInspector, FormerlySerializedAs("enemyShotMaterial")] private Material legacyEnemyMaterial;
    [SerializeField, HideInInspector] private bool weaponAttributesMigrated;

    public WeaponAttributes PlayerWeapon { get { EnsureWeapons(); return playerWeapon; } }

    public void EnsureWeapons()
    {
        if (playerWeapon == null) playerWeapon = new WeaponAttributes();
        if (weaponAttributesMigrated) return;
        bool legacyMovement = legacyProjectileSpeed > 0 || legacyPlayerFireInterval > 0;
        if (legacyPlayerDamage >= 0) playerWeapon.projectile.damage = legacyPlayerDamage;
        if (legacyPlayerFireInterval > 0) playerWeapon.fireInterval = legacyPlayerFireInterval;
        if (legacyProjectileRange > 0) playerWeapon.projectile.range = legacyProjectileRange;
        if (legacyProjectileSpeed > 0) playerWeapon.projectile.speed = legacyProjectileSpeed;
        if (!ReferenceEquals(legacyPlayerMaterial, null)) playerWeapon.projectile.material = legacyPlayerMaterial;
        foreach (var enemy in new[] { ordinary, boss })
            if (enemy != null)
            {
                var projectile = enemy.Weapon.projectile;
                if (legacyProjectileSpeed > 0) projectile.speed = legacyProjectileSpeed;
                if (!ReferenceEquals(legacyEnemyMaterial, null)) projectile.material = legacyEnemyMaterial;
            }
        if (legacyMovement)
        {
            if (ordinary != null) ordinary.Weapon.shootingMoveMultiplier = 0;
            if (boss != null) boss.Weapon.shootingMoveMultiplier = .7f;
        }
        legacyPlayerDamage = legacyPlayerFireInterval = legacyProjectileSpeed = legacyProjectileRange = -1;
        legacyPlayerMaterial = legacyEnemyMaterial = null;
        weaponAttributesMigrated = true;
    }

    public void OnBeforeSerialize() => EnsureWeapons();
    public void OnAfterDeserialize() => EnsureWeapons();

    public float playerDamage { get => PlayerWeapon.projectile.damage; set => PlayerWeapon.projectile.damage = value; }
    public float playerFireInterval { get => PlayerWeapon.fireInterval; set => PlayerWeapon.fireInterval = value; }
    public float projectileRange { get => PlayerWeapon.projectile.range; set => PlayerWeapon.projectile.range = value; }
    public float projectileSpeed
    {
        get => PlayerWeapon.projectile.speed;
        set { EnsureWeapons(); playerWeapon.projectile.speed = value; ordinary.Weapon.projectile.speed = boss.Weapon.projectile.speed = value; }
    }
    public Material playerShotMaterial { get => PlayerWeapon.projectile.material; set => PlayerWeapon.projectile.material = value; }
    public Material enemyShotMaterial
    {
        get { EnsureWeapons(); return ordinary.Weapon.projectile.material; }
        set { EnsureWeapons(); ordinary.Weapon.projectile.material = boss.Weapon.projectile.material = value; }
    }
}
