using UnityEngine;
using UnityEngine.AI;

/// <summary>Readable warning, suppressive sweep, locked charge, then a vulnerable counterattack window.</summary>
public sealed class DestructionBossActor : EnemyActor
{
    public override bool IsBoss => true;
    public enum AttackStage { Approach, SweepWarning, Sweep, ChargeWarning, Charge, Exposed }
    public AttackStage Stage { get; private set; }
    public string ActionText => Stage == AttackStage.Approach ? "REPOSITIONING" : Stage == AttackStage.Exposed ? "CORE EXPOSED" : Stage == AttackStage.SweepWarning ? "SWEEP WARNING" :
        Stage == AttackStage.ChargeWarning ? "CHARGE WARNING" : Stage == AttackStage.Charge ? "CHARGING" : "SWEEP";
    private string _abilityRun;
    private float _elapsed;
    private Vector3 _aim, _chargeEnd;
    private bool _chargeHit;
    private LineRenderer _telegraph;

    public override void Tick(float dt)
    {
        if (Owner == null || !Owner.UsesNodeObjectives) { base.Tick(dt); return; }
        bool live = Owner.IsCurrentCombat(Model.runId) && !IsDead && Target != null && !Target.IsDead;
        SetRunning(live);
        if (!live || dt <= 0) return;
        if (_abilityRun != Model.runId)
        {
            _abilityRun = Model.runId; Stage = AttackStage.Approach; _elapsed = 0;
        }
        var config = Owner.Configuration.purgeBoss;
        _elapsed += dt;
        TickPose(dt);
        if (Stage != AttackStage.Approach && Agent.enabled && Agent.isOnNavMesh) { Agent.isStopped = true; Agent.velocity = Vector3.zero; }
        if (Stage == AttackStage.Approach)
        {
            if (_telegraph != null) _telegraph.enabled = false;
            if (Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.stoppingDistance = 3;
                Agent.isStopped = false;
                Agent.SetDestination(Target.transform.position);
            }
            Face(Target.transform.position);
            Vector3 delta = Target.transform.position - transform.position;
            if (delta.magnitude <= 18 && Mathf.Abs(delta.y) < 1.25f &&
                HasLineOfSight(transform.position + Vector3.up, delta))
            { Change(AttackStage.SweepWarning); LockAim(); }
        }
        else if (Stage == AttackStage.SweepWarning)
        {
            ShowTelegraph(_aim, new Color(1, .55f, .1f));
            if (_elapsed >= config.warningSeconds) Change(AttackStage.Sweep);
        }
        else if (Stage == AttackStage.Sweep)
        {
            float angle = Mathf.Lerp(-config.sweepArc * .5f, config.sweepArc * .5f, Mathf.Clamp01(_elapsed / config.sweepSeconds));
            Vector3 direction = Quaternion.Euler(0, angle, 0) * (_aim - transform.position).normalized;
            Vector3 point = transform.position + direction * 28;
            point.y = Target.transform.position.y + .9f;
            Face(point); ShowTelegraph(point, new Color(1, .2f, .1f));
            // Sweep the telegraphed sector even when the player dodges the current line.
            // Physical bullets resolve cover; a ray that misses the player must not cancel suppression.
            Model.shotsFired += Weapon.TickAutomatic(dt, point, null, PrepareWeaponShot);
            // The shot animation must not restart pursuit during a stationary sweep.
            if (Agent.enabled && Agent.isOnNavMesh) { Agent.isStopped = true; Agent.velocity = Vector3.zero; }
            if (_elapsed >= config.sweepSeconds) { Change(AttackStage.ChargeWarning); LockAim(); }
        }
        else if (Stage == AttackStage.ChargeWarning)
        {
            Face(_chargeEnd); ShowTelegraph(_chargeEnd, Color.red);
            if (_elapsed >= config.chargeWarningSeconds) { Change(AttackStage.Charge); _chargeHit = false; }
        }
        else if (Stage == AttackStage.Charge)
        {
            Vector3 start = transform.position;
            Vector3 next = Vector3.MoveTowards(start, _chargeEnd, config.chargeDistance / config.chargeSeconds * dt);
            if (Agent.enabled && Agent.isOnNavMesh) Agent.Move(next - start);
            // Navigation can synchronize Transform later in the frame. Sweep its committed position
            // so a long frame cannot move through the player without registering contact.
            Vector3 committed = Agent.enabled && Agent.isOnNavMesh ? Agent.nextPosition : transform.position;
            Vector3 segment = committed - start;
            float t = segment.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector3.Dot(Target.transform.position - start, segment) / segment.sqrMagnitude) : 0;
            Vector3 closest = start + segment * t;
            if (!_chargeHit && Vector3.Distance(Target.transform.position, closest) <= 1.5f && HasLineOfSight(closest + Vector3.up, Target.transform.position + Vector3.up * .9f - (closest + Vector3.up)))
            { _chargeHit = true; Target.ApplyDamage(config.chargeDamage); }
            if (_elapsed >= config.chargeSeconds || Vector3.Distance(committed, _chargeEnd) < .2f) Change(AttackStage.Exposed);
        }
        else
        {
            ShowTelegraph(transform.position + Vector3.up * 3, Color.cyan);
            if (_elapsed >= config.exposedSeconds) Change(AttackStage.Approach);
        }
    }

    private void Change(AttackStage stage) { Stage = stage; _elapsed = 0; Weapon.Model.fireAccumulator = 0; }

    private void LockAim()
    {
        _aim = Target.transform.position;
        Vector3 delta = _aim - transform.position; delta.y = 0;
        var destination = transform.position + delta.normalized * Mathf.Min(delta.magnitude + 2, Owner.Configuration.purgeBoss.chargeDistance);
        _chargeEnd = NavMesh.Raycast(transform.position, destination, out var hit, NavMesh.AllAreas) ? hit.position : destination;
        Face(_aim);
    }

    private void Face(Vector3 point)
    {
        Vector3 flat = point - transform.position; flat.y = 0;
        if (flat.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(-flat);
    }

    private void ShowTelegraph(Vector3 end, Color color)
    {
        if (_telegraph == null)
        {
            var go = new GameObject("Purge attack warning"); go.transform.SetParent(transform, false);
            _telegraph = go.AddComponent<LineRenderer>(); _telegraph.positionCount = 2;
            // A vertex-color shader preserves the warning/exposure colors independently of bullets.
            _telegraph.sharedMaterial = Owner.PurgeTelegraphMaterial;
            _telegraph.startWidth = .12f; _telegraph.endWidth = .3f;
        }
        _telegraph.enabled = true; _telegraph.startColor = _telegraph.endColor = color;
        _telegraph.SetPosition(0, transform.position + Vector3.up * .15f);
        _telegraph.SetPosition(1, end + Vector3.up * .15f);
    }

    public override void SetRunning(bool running)
    {
        base.SetRunning(running);
        if (!running && _telegraph != null) _telegraph.enabled = false;
    }

    public override void ApplyDamage(float damage)
    {
        if (Owner != null && Owner.UsesNodeObjectives && Stage == AttackStage.Exposed)
            damage *= Owner.Configuration.purgeBoss.exposedDamageMultiplier;
        base.ApplyDamage(damage);
    }
}
