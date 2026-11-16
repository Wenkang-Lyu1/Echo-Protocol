using System;
using UnityEngine;

public enum EnemyRole { Pursuer, Patrol, NodeGuard, Hunter, NodeBoss }

[Serializable]
public sealed class EnemyAssignment
{
    public EnemyRole role;
    public string nodeId;
    public Transform[] patrolPoints = new Transform[0];
    public float detectionRadius = 13;
    public float leashRadius = 10;
    public float guardRadius = 6;
}
