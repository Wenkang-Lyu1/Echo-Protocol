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
    public EnemyAssignment assignment = new EnemyAssignment();
    protected LevelControl Owner;
    protected PlayerControl Target;
    protected EnemyCombatStats Stats;
    private Vector3 _home;
    private int _patrolIndex;
    private bool _alert;
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
        Owner = owner; Target = target; Stats = stats;
        _home = _spawn; _patrolIndex = 0; _alert = false;
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
        Agent.speed = Stats.speed; Agent.acceleration = 40; Agent.angularSpeed = 720;
        Agent.stoppingDistance = IsBoss ? 3 : 5;
        Agent.updateRotation = false;
        SetRunning(true);
    }

    public virtual void SetRunning(bool running)
    {
        if (Agent == null) return;
        running &= !IsDead;
        float multiplier = IsShooting && Weapon != null && Weapon.Attributes != null ? Weapon.Attributes.shootingMoveMultiplier : 1;
        if (Stats != null) Agent.speed = Stats.speed * multiplier;
        if (Agent.enabled && Agent.isOnNavMesh)
        {
            Agent.isStopped = !running || multiplier <= 0;
            if (Agent.isStopped) Agent.velocity = Vector3.zero;
            else if (Agent.velocity.sqrMagnitude > Agent.speed * Agent.speed) Agent.velocity = Vector3.ClampMagnitude(Agent.velocity, Agent.speed);
        }
    }

    public virtual void Tick(float dt)
    {
        bool live = Owner != null && Owner.IsCurrentCombat(Model.runId) && !IsDead && Target != null && !Target.IsDead;
        SetRunning(live);
        if (!live || dt <= 0) return;
        bool engage = ResolveDuty(out var destination);
        if (Agent.enabled && Agent.isOnNavMesh) Agent.SetDestination(destination);
        var targetPoint = (engage ? Target.transform.position : destination) + Vector3.up * .9f;
        var offset = targetPoint - transform.position;
        var flat = new Vector3(offset.x, 0, offset.z);
        if (flat.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(-flat);
        _actions.Tick(dt, PresentationSpeed, Stats.speed);
        SetRunning(live);
        if (engage) Model.shotsFired += Weapon.TickAutomatic(dt, targetPoint, HasLineOfSight, PrepareWeaponShot);
        else Weapon.Model.fireAccumulator = 0;
    }

    private bool ResolveDuty(out Vector3 destination)
    {
        destination = Target.transform.position;
        if (assignment.role == EnemyRole.Pursuer || assignment.role == EnemyRole.Hunter) return true;
        float distance = Vector3.Distance(transform.position, Target.transform.position);
        bool visible = distance <= assignment.detectionRadius && HasLineOfSight(transform.position + Vector3.up, Target.transform.position + Vector3.up * .9f - (transform.position + Vector3.up));
        if (visible) _alert = true;
        if (Vector3.Distance(Target.transform.position, _home) > assignment.leashRadius + assignment.detectionRadius) _alert = false;
        if (assignment.role == EnemyRole.Patrol)
        {
            if (_alert && Vector3.Distance(transform.position, _home) <= assignment.leashRadius) { Agent.stoppingDistance = 5; return true; }
            _alert = false;
            var points = assignment.patrolPoints;
            destination = points != null && points.Length > 0 && points[_patrolIndex % points.Length] != null ? points[_patrolIndex % points.Length].position : _home;
            if (Vector3.Distance(transform.position, destination) < 1 && points != null && points.Length > 0) _patrolIndex = (_patrolIndex + 1) % points.Length;
            Agent.stoppingDistance = .4f;
            return false;
        }
        Agent.stoppingDistance = 3;
        if (distance > assignment.detectionRadius || Vector3.Distance(transform.position, _home) > assignment.guardRadius || !_alert)
        { destination = _home; return false; }
        destination = _home + Vector3.ClampMagnitude(Target.transform.position - _home, assignment.guardRadius * .8f);
        return distance <= assignment.detectionRadius;
    }

    protected void PrepareWeaponShot()
    {
        _actions.OnShot();
        SetRunning(true);
        _actions.Tick(0, PresentationSpeed, Stats.speed);
    }

    // NavMeshAgent.velocity reports the last simulation step until navigation updates.
    private float PresentationSpeed => Agent.isStopped ? 0 : Mathf.Min(Agent.velocity.magnitude, Agent.speed);
    protected void TickPose(float dt) => _actions.Tick(dt, PresentationSpeed, Stats.speed);

    protected bool HasLineOfSight(Vector3 origin, Vector3 offset)
    {
        var hits = Physics.RaycastAll(origin, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<EnemyActor>() != null) continue;
            return hit.collider.GetComponentInParent<PlayerControl>() == Target;
        }
        return true;
    }

    public virtual void ApplyDamage(float damage)
    {
        if (IsDead || Owner == null || !Owner.IsCurrentCombat(Model.runId) || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0) return;
        Model.hp = Mathf.Max(0, Model.hp - damage);
        if (Model.hp > 0) { _actions.OnHit(); return; }
        Model.isDead = true; SetRunning(false);
        for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = false;
        _actions.OnDeath();
        Owner.NotifyEnemyDeath(Model.runId, Model.instanceId);
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
