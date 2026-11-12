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
    private LevelControl _owner;
    private PlayerControl _target;
    private EnemyCombatStats _stats;
    private double _fireAccumulator;
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
    }

    public void ResetForRun(LevelControl owner, string runId, PlayerControl target, EnemyCombatStats stats)
    {
        Cache(); SetRunning(false); Agent.enabled = false;
        _owner = owner; _target = target; _stats = stats; _fireAccumulator = 0;
        Model = new CombatEnemyModel { runId = runId, instanceId = gameObject.GetInstanceID(), hp = stats.health };
        transform.SetPositionAndRotation(_spawn, _spawnRotation);
        for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = _visible[i];
        for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = _collision[i];
        _actions.Reset();
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
        if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = !running;
    }

    public void Tick(float dt)
    {
        bool live = _owner != null && _owner.IsCurrentCombat(Model.runId) && !IsDead && _target != null && !_target.IsDead;
        SetRunning(live);
        if (!live || dt <= 0) return;
        if (Agent.enabled && Agent.isOnNavMesh) Agent.SetDestination(_target.transform.position);
        var origin = transform.position + Vector3.up * 1.25f;
        var targetPoint = _target.transform.position + Vector3.up * .9f;
        var offset = targetPoint - origin;
        var flat = new Vector3(offset.x, 0, offset.z);
        if (flat.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(-flat);
        if (offset.magnitude > _stats.range || !HasLineOfSight(origin, offset))
        {
            _fireAccumulator = 0;
            _actions.Tick(dt, Agent.velocity.magnitude, _stats.speed);
            return;
        }
        int due = CombatShotClock.Advance(ref _fireAccumulator, dt, _stats.fireInterval);
        for (int i = 0; i < due; i++)
        {
            if (!_owner.IsCurrentCombat(Model.runId) || _target.IsDead) break;
            var direction = Quaternion.Euler(UnityEngine.Random.Range(-_stats.spreadDegrees, _stats.spreadDegrees),
                UnityEngine.Random.Range(-_stats.spreadDegrees, _stats.spreadDegrees), 0) * offset.normalized;
            _owner.SpawnProjectile(new ProjectileOwner(Model.runId, gameObject, IsBoss ? ProjectileSource.Boss : ProjectileSource.Enemy),
                origin, direction, _stats.damage, _stats.range);
            Model.shotsFired++;
            _actions.OnShot();
        }
        _actions.Tick(dt, Agent.velocity.magnitude, _stats.speed);
    }

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
