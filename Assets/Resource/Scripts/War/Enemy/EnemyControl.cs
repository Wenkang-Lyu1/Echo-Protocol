using System;
using UnityEngine;
using UnityEngine.AI;

// ============================================================
// EnemyControl -- controller for a single enemy.
// Created and managed by EnemyControl (wave owner).
// Holds EnemyModel and implements a simple FSM.
// ============================================================
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AnimDriver))]
public class EnemyControl : GlobalControl<EnemyControl, EnemyModel>
{
    [SerializeField] private Transform eyePoint;
    [SerializeField] private float eyeHeight = 1.6f;

    private NavMeshAgent _agent;
    private AnimDriver _anim;
    private Transform _player;
    private float _scanTimer;

    public bool IsDead => model.isDead;
    public float HpRatio => model.HpRatio;

    protected override void OnInitialize()
    {
        base.OnInitialize();
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponent<AnimDriver>();

        if (eyePoint == null)
        {
            eyePoint = new GameObject("Eye").transform;
            eyePoint.SetParent(transform);
            eyePoint.localPosition = Vector3.up * eyeHeight;
        }

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;

        if (_agent != null) _agent.speed = model.moveSpeed;

        EventCenter.Instance.AddListener(GameEvent.Echo_PlaybackStart, OnEchoPlayback);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Echo_PlaybackStart, OnEchoPlayback);
    }

    public override void Tick(float deltaTime)
    {
        if (model.isDead) return;

        model.attackTimer -= deltaTime;
        _scanTimer -= deltaTime;

        var playerVisible = EnemyUtil.IsPlayerVisible(this, _player, eyePoint);
        var distToPlayer = _player != null
            ? Vector3.Distance(transform.position, _player.position)
            : float.MaxValue;

        switch (model.state)
        {
            case EnemyState.Patrol:
                if (playerVisible && distToPlayer <= model.alertRange)
                    Transition(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                if (_player != null && _agent != null)
                    _agent.SetDestination(_player.position);
                if (distToPlayer <= model.attackRange)
                    Transition(EnemyState.Attack);
                else if (!playerVisible && _agent != null && _agent.remainingDistance < 0.5f)
                    Transition(EnemyState.Patrol);
                break;

            case EnemyState.Attack:
                if (_player != null)
                {
                    if (_agent != null) _agent.ResetPath();
                    transform.LookAt(new Vector3(_player.position.x, transform.position.y, _player.position.z));
                }
                if (model.attackTimer <= 0f)
                {
                    Attack();
                    model.attackTimer = model.attackCooldown;
                }
                if (distToPlayer > model.attackRange * 1.2f)
                    Transition(EnemyState.Chase);
                break;
        }

        if (_anim != null && _agent != null)
            _anim.SetMoveSpeed(_agent.hasPath ? _agent.velocity.magnitude : 0f);
    }

        // Pull exerted on AI by an echo ghost.
    private void OnEchoPlayback(GameEventArgs args)
    {
        if (!model.canSearchEcho || model.isDead || model.state == EnemyState.Attack) return;

        Vector3 echoPos = Vector3.zero;
        if (args.data is EchoPlayback ep) echoPos = ep.transform.position;
        else if (args.data is Vector3 v) echoPos = v;

        if (_agent != null) _agent.SetDestination(echoPos);
        if (model.state != EnemyState.Chase) Transition(EnemyState.Alert);
    }

    private void Attack()
    {
        if (_player == null) return;
        _anim?.Play(EchoAnimState.Fire.Name, 0.05f);
        if (Physics.Raycast(eyePoint.position,
                (_player.position - eyePoint.position).normalized,
                out var hit, model.attackRange * 1.2f, -1, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Player"))
                PlayerControl.Instance?.ApplyDamage(model.damage);
        }
    }

    private void Transition(EnemyState next)
    {
        model.state = next;
        if (_agent != null)
        {
            _agent.speed = (next == EnemyState.Chase)
                ? model.chaseSpeed
                : model.moveSpeed;
        }
    }

        // ---- Public interface (called by the damage system) ----
    public void ApplyDamage(float damage)
    {
        if (model.isDead) return;
        model.hp = Mathf.Max(0f, model.hp - damage);

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Enemy_Hurt,
            data = new ValueArgs<float>(model.HpRatio)
        });

        if (model.hp <= 0f) Die();
        else if (model.state != EnemyState.Attack)
            Transition(EnemyState.Chase);
    }

    private void Die()
    {
        model.isDead = true;
        model.state = EnemyState.Dead;
        if (_agent != null) _agent.isStopped = true;
_anim?.Play(EchoAnimState.Die.Name, 0.08f);
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Enemy_Die,
            data = new ValueArgs<float>(model.scoreValue)
        });

        Destroy(gameObject, 2.5f);
    }
}
