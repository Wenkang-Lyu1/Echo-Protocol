using System;
using UnityEngine;

/// <summary>One independent moving bullet. LevelControl is its only simulation driver.</summary>
public sealed class ProjectileActor : MonoBehaviour
{
    public ProjectileModel Model { get; private set; }
    private LevelControl _level;

    public void Initialize(LevelControl level, ProjectileOwner owner, Vector3 origin, Vector3 direction,
        float damage, float speed, float range, Material material)
        => Initialize(level, owner, origin, direction, new ProjectileAttributes { damage = damage, speed = speed, range = range, material = material });

    public void Initialize(LevelControl level, ProjectileOwner owner, Vector3 origin, Vector3 direction, ProjectileAttributes attributes)
    {
        attributes.Validate();
        _level = level;
        Model = new ProjectileModel { owner = owner, direction = direction.normalized,
            damage = attributes.damage, speed = attributes.speed, remainingRange = attributes.range,
            collisionRadius = attributes.collisionRadius, spawnPosition = origin };
        transform.position = origin;
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Bullet visual"; visual.transform.SetParent(transform, false);
        visual.transform.localScale = Vector3.one * attributes.visualDiameter;
        var collider = visual.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        visual.GetComponent<Renderer>().sharedMaterial = attributes.material;
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = attributes.material; trail.time = .06f; trail.startWidth = attributes.collisionRadius; trail.endWidth = .005f;
    }

    public void Tick(float dt)
    {
        if (Model.finished || dt <= 0) return;
        if (!_level.IsCurrentCombat(Model.owner.runId)) { Stop(); return; }
        foreach (var collider in Physics.OverlapSphere(transform.position, Model.collisionRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (Ignore(collider)) continue;
            Hit(collider.gameObject, transform.position);
            return;
        }
        float distance = Mathf.Min(Model.remainingRange, Model.speed * dt);
        var hits = Physics.SphereCastAll(transform.position, Model.collisionRadius, Model.direction, distance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (Ignore(hit.collider)) continue;
            Hit(hit.collider.gameObject, hit.point);
            return;
        }
        transform.position += Model.direction * distance;
        Model.remainingRange -= distance;
        if (Model.remainingRange <= 0) Stop();
    }

    private void Hit(GameObject target, Vector3 point)
    {
        Model.hitTarget = target; Model.hitPoint = point; Model.finished = true;
        transform.position = point;
        _level.ResolveProjectileHit(this);
        gameObject.SetActive(false);
    }

    private bool Ignore(Collider collider)
    {
        if (collider.gameObject == Model.owner.source ||
            (Model.owner.source != null && collider.transform.IsChildOf(Model.owner.source.transform))) return true;
        var enemy = collider.GetComponentInParent<EnemyActor>();
        var player = collider.GetComponentInParent<PlayerControl>();
        if (Model.owner.IsPlayerSide) return player != null || (enemy != null && enemy.IsDead);
        return enemy != null || (player != null && player.IsDead);
    }

    internal bool ResolveDamageOnce()
    {
        if (Model == null || !Model.finished || Model.hitTarget == null || Model.hitReported || !_level.IsCurrentCombat(Model.owner.runId)) return false;
        Model.hitReported = true;
        var enemy = Model.hitTarget.GetComponentInParent<EnemyActor>();
        var player = Model.hitTarget.GetComponentInParent<PlayerControl>();
        if (Model.damage > 0 && ((Model.owner.IsPlayerSide && enemy != null && enemy.Model.runId == Model.owner.runId) ||
            (!Model.owner.IsPlayerSide && player == _level.CombatPlayer)))
            DamageSystem.Instance.Dispatch(new HitArgs(Model.hitTarget, Model.owner.source, Model.damage, Model.owner.kind == ProjectileSource.Echo));
        return true;
    }

    public void Stop()
    {
        if (Model != null) Model.finished = true;
        gameObject.SetActive(false);
    }
}
