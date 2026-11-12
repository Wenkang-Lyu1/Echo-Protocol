using System;
using UnityEngine;
using UnityEngine.AI;

public sealed class CombatEnemyModel
{
    public string runId;
    public int instanceId;
    public float hp;
    public bool isDead;
    public int shotsFired;
}

/// <summary>Non-singleton enemy instance. Navigation is autonomous, combat logic is driven once by LevelControl.</summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyActor : CharacterActor, IDamageable
{
    public CombatEnemyModel Model { get; private set; } = new CombatEnemyModel();
    public virtual bool IsBoss => false;
    public float Hp => Model.hp;
    public bool IsDead => Model.isDead;
    public NavMeshAgent Agent { get; private set; }
    public WeaponActor Weapon { get; private set; }
    public bool IsShooting => _actions != null && _actions.IsShooting;
    private LevelControl _owner;
    private PlayerControl _target;
    private EnemyCombatStats _stats;
    private Vector3 _spawn;
    private Quaternion _spawnRotation;
    private Renderer[] _renderers;
    private bool[] _visible;
    private Collider[] _colliders;
    private bool[] _collision;
    private EnemyActionPresentation _actions;

    private void Cache()
    {
        if (Agent != null) return;
        Agent = GetComponent<NavMeshAgent>();
        _spawn = transform.position; _spawnRotation = transform.rotation;
        _renderers = GetComponentsInChildren<Renderer>(true); _visible = new bool[_renderers.Length];
        for (int i = 0; i < _visible.Length; i++) _visible[i] = _renderers[i].enabled;
        _colliders = GetComponentsInChildren<Collider>(true); _collision = new bool[_colliders.Length];
        for (int i = 0; i < _collision.Length; i++) _collision[i] = _colliders[i].enabled;
        _actions = new EnemyActionPresentation(gameObject, IsBoss);
        Weapon = WeaponActor.BindModelGun(gameObject);
    }

    public void ResetForRun(LevelControl owner, string runId, PlayerControl target, EnemyCombatStats stats)
    {
        Cache(); SetRunning(false); Agent.enabled = false;
        _owner = owner; _target = target; _stats = stats;
        Model = new CombatEnemyModel { runId = runId, instanceId = gameObject.GetInstanceID(), hp = stats.health };
        transform.SetPositionAndRotation(_spawn, _spawnRotation);
        for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = _visible[i];
        for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = _collision[i];
        _actions.Reset();
        Weapon.Configure(owner, new ProjectileOwner(runId, gameObject, IsBoss ? ProjectileSource.Boss : ProjectileSource.Enemy), stats.Weapon);
    }

    public bool TryGetStartPosition(out Vector3 position)
    {
        Cache();
        bool found = NavMesh.SamplePosition(transform.position, out var sample, 2, NavMesh.AllAreas);
        position = sample.position; return found;
    }

    public void Activate(Vector3 position)
    {
        Agent.enabled = true;
        if (!Agent.Warp(position)) throw new InvalidOperationException("Enemy cannot enter navigation mesh: " + name);
        Agent.speed = _stats.speed; Agent.acceleration = 40; Agent.angularSpeed = 720;
        Agent.stoppingDistance = IsBoss ? 3 : 5;
        Agent.updateRotation = false;
        SetRunning(true);
    }

    public void SetRunning(bool running)
    {
        if (Agent == null) return;
        running &= !IsDead;
        float multiplier = IsShooting && Weapon != null && Weapon.Attributes != null ? Weapon.Attributes.shootingMoveMultiplier : 1;
        if (_stats != null) Agent.speed = _stats.speed * multiplier;
        if (Agent.enabled && Agent.isOnNavMesh)
        {
            Agent.isStopped = !running || multiplier <= 0;
            if (Agent.isStopped) Agent.velocity = Vector3.zero;
            else if (Agent.velocity.sqrMagnitude > Agent.speed * Agent.speed) Agent.velocity = Vector3.ClampMagnitude(Agent.velocity, Agent.speed);
        }
    }

    public void Tick(float dt)
    {
        bool live = _owner != null && _owner.IsCurrentCombat(Model.runId) && !IsDead && _target != null && !_target.IsDead;
        SetRunning(live);
        if (!live || dt <= 0) return;
        if (Agent.enabled && Agent.isOnNavMesh) Agent.SetDestination(_target.transform.position);
        var targetPoint = _target.transform.position + Vector3.up * .9f;
        var offset = targetPoint - transform.position;
        var flat = new Vector3(offset.x, 0, offset.z);
        if (flat.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(-flat);
        _actions.Tick(dt, PresentationSpeed, _stats.speed);
        SetRunning(live);
        Model.shotsFired += Weapon.TickAutomatic(dt, targetPoint, HasLineOfSight, PrepareWeaponShot);
    }

    private void PrepareWeaponShot()
    {
        _actions.OnShot();
        SetRunning(true);
        _actions.Tick(0, PresentationSpeed, _stats.speed);
    }

    // NavMeshAgent.velocity reports the last simulation step until navigation updates.
    private float PresentationSpeed => Agent.isStopped ? 0 : Mathf.Min(Agent.velocity.magnitude, Agent.speed);

    private bool HasLineOfSight(Vector3 origin, Vector3 offset)
    {
        var hits = Physics.RaycastAll(origin, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<EnemyActor>() != null) continue;
            return hit.collider.GetComponentInParent<PlayerControl>() == _target;
        }
        return true;
    }

    public void ApplyDamage(float damage)
    {
        if (IsDead || _owner == null || !_owner.IsCurrentCombat(Model.runId) || float.IsNaN(damage) || damage <= 0) return;
        Model.hp = Mathf.Max(0, Model.hp - damage);
        if (Model.hp > 0) { _actions.OnHit(); return; }
        Model.isDead = true; SetRunning(false);
        for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = false;
        _actions.OnDeath();
        _owner.NotifyEnemyDeath(Model.runId, Model.instanceId);
    }

    public bool TickDeathPresentation(float dt)
    {
        if (!IsDead) return true;
        _actions.TickDeath(dt);
        if (!_actions.DeathFinished) return false;
        foreach (var renderer in _renderers) renderer.enabled = false;
        return true;
    }
}
