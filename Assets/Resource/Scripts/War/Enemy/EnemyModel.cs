using System;
using UnityEngine;

[Serializable]
public class EnemyModel : ModelBase
{
    public string displayName = "Grunt";
    public EnemyType type = EnemyType.Grunt;
    public float hp = 60f;
    public float maxHp = 60f;
    public float moveSpeed = 3.5f;
    public float chaseSpeed = 5f;
    public float alertRange = 14f;
    public float attackRange = 8f;
    public float attackCooldown = 1.2f;
    public float damage = 12f;
    public float scoreValue = 100f;
    public bool canSearchEcho;

        // Runtime
    public bool isDead;
    public EnemyState state;
    public float attackTimer;
    public Vector3 patrolPoint;

    public float HpRatio => maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;

    public override void Reset()
    {
        hp = maxHp;
        isDead = false;
        state = EnemyState.Patrol;
        attackTimer = 0f;
    }
}

public enum EnemyType { Grunt, Guard, Hunter }
public enum EnemyState { Idle, Patrol, Alert, Chase, Attack, Dead }
