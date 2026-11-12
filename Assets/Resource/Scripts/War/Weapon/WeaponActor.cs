using System;
using UnityEngine;

/// <summary>An equipped weapon instance. The owning controller supplies input and ticks it explicitly.</summary>
[DisallowMultipleComponent]
public sealed class WeaponActor : MonoBehaviour
{
    [SerializeField] private Transform muzzle;
    public Transform Muzzle => muzzle;
    public WeaponAttributes Attributes { get; private set; }
    public WeaponInstanceModel Model { get; private set; } = new WeaponInstanceModel();
    public ProjectileOwner Owner { get; private set; }
    private LevelControl _level;

    public void Configure(LevelControl level, ProjectileOwner owner, WeaponAttributes attributes, Transform muzzleTransform = null)
    {
        if (attributes == null) throw new ArgumentNullException(nameof(attributes));
        attributes.Validate();
        _level = level; Owner = new ProjectileOwner(owner.runId, owner.source, owner.kind, owner.segmentId, GetInstanceID()); Attributes = attributes;
        if (muzzleTransform != null) muzzle = muzzleTransform;
        ResetClock();
    }

    public void ResetClock() => Model = new WeaponInstanceModel();
    public void TickCooldown(float dt) { if (dt > 0) Model.cooldown = Mathf.Max(0, Model.cooldown - dt); }

    public int TickAutomatic(float dt, Vector3 aimPoint, Func<Vector3, Vector3, bool> hasLineOfSight, Action prepareShot)
    {
        if (dt <= 0) return 0;
        if (!CanEmit || muzzle == null || !CanReach(aimPoint, hasLineOfSight)) { Model.fireAccumulator = 0; return 0; }
        int due = CombatShotClock.Advance(ref Model.fireAccumulator, dt, Attributes.fireInterval);
        int fired = 0;
        for (int i = 0; i < due && CanEmit; i++)
        {
            // Apply the shot pose first; both the bullet and flash use the resulting animated muzzle.
            prepareShot?.Invoke();
            Physics.SyncTransforms();
            if (!CanReach(aimPoint, hasLineOfSight)) { Model.fireAccumulator = 0; break; }
            if (Emit(muzzle.position, Spread((aimPoint - muzzle.position).normalized), Attributes.projectile) != null) fired++;
        }
        return fired;
    }

    public bool TryFireAt(Vector3 aimPoint)
    {
        if (!CanEmit || muzzle == null || Model.cooldown > 0 || (aimPoint - muzzle.position).sqrMagnitude < .000001f) return false;
        if (Emit(muzzle.position, Spread((aimPoint - muzzle.position).normalized), Attributes.projectile) == null) return false;
        Model.cooldown = Attributes.fireInterval;
        return true;
    }

    // Preparation captures an accepted shot without spawning a damaging projectile.
    public bool TryRecordShot(float time, Vector3 aimPoint, out EchoFireEvent shot)
    {
        shot = default;
        if (_level != null && (_level.model.runId != Owner.runId || _level.model.phase != LevelPhase.EchoPreparation ||
            GameManager.Instance.State != GameState.Playing)) return false;
        if (Attributes == null || muzzle == null || Model.cooldown > 0 || (aimPoint - muzzle.position).sqrMagnitude < .000001f) return false;
        shot = new EchoFireEvent { time = time, muzzlePosition = muzzle.position,
            fireDirection = Spread((aimPoint - muzzle.position).normalized), projectile = Attributes.projectile.Copy() };
        Model.cooldown = Attributes.fireInterval; Model.shotsFired++;
        return true;
    }

    // Recorded shots were already accepted by the original weapon; never rate-limit playback again.
    public ProjectileActor Replay(EchoFireEvent shot) => CanEmit
        ? Emit(shot.muzzlePosition, shot.fireDirection, shot.projectile ?? Attributes.projectile) : null;

    private bool CanEmit
    {
        get
        {
            if (_level == null || Attributes == null || Owner.source == null || !_level.IsCurrentCombat(Owner.runId) || _level.CombatPlayer.IsDead) return false;
            var source = Owner.source.GetComponentInParent<IDamageable>();
            return source == null || !source.IsDead;
        }
    }

    private bool CanReach(Vector3 point, Func<Vector3, Vector3, bool> hasLineOfSight)
    {
        var offset = point - muzzle.position;
        return offset.sqrMagnitude > .000001f && offset.magnitude <= Attributes.projectile.range &&
            (hasLineOfSight == null || hasLineOfSight(muzzle.position, offset));
    }

    private Vector3 Spread(Vector3 direction) => Quaternion.Euler(UnityEngine.Random.Range(-Attributes.spreadDegrees, Attributes.spreadDegrees),
        UnityEngine.Random.Range(-Attributes.spreadDegrees, Attributes.spreadDegrees), 0) * direction;

    private ProjectileActor Emit(Vector3 origin, Vector3 direction, ProjectileAttributes projectile)
    {
        var bullet = _level.SpawnProjectile(Owner, origin, direction, projectile);
        if (bullet != null)
        {
            Model.shotsFired++;
            Model.lastMuzzlePosition = origin; Model.lastDirection = direction.normalized;
        }
        return bullet;
    }

    public static WeaponActor BindModelGun(GameObject actor)
    {
        var existing = actor.GetComponentInChildren<WeaponActor>(true);
        if (existing != null && existing.Muzzle != null) return existing;
        foreach (var bone in actor.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name != "fire_01") continue;
            var weapon = bone.parent.GetComponent<WeaponActor>() ?? bone.parent.gameObject.AddComponent<WeaponActor>();
            weapon.muzzle = bone;
            return weapon;
        }
        throw new InvalidOperationException("Bind an animated gun muzzle for " + actor.name);
    }

    public static WeaponActor BindFirstPerson(PlayerControl player)
    {
        var gun = player.CameraPivot.Find("Training Carbine");
        if (gun == null) throw new InvalidOperationException("First-person weapon model is missing.");
        var weapon = gun.GetComponent<WeaponActor>() ?? gun.gameObject.AddComponent<WeaponActor>();
        if (weapon.muzzle == null)
        {
            var marker = gun.Find("Muzzle");
            if (marker == null)
            {
                marker = new GameObject("Muzzle").transform;
                marker.SetParent(gun, false);
                // End of the authored training carbine's barrel, in weapon-local space.
                marker.localPosition = new Vector3(0, .02f, .375f);
            }
            weapon.muzzle = marker;
        }
        return weapon;
    }
}
