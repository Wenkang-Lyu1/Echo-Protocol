using UnityEngine;

// ============================================================
// EnemyUtil -- AI calculation utility for enemies.
// Pure static, stateless.
// ============================================================
public static class EnemyUtil
{
        // Whether the player is visible.
    public static bool IsPlayerVisible(EnemyControl enemy, Transform player, Transform eyePoint)
    {
        if (enemy == null || player == null || eyePoint == null) return false;
        var model = enemy.GetModel();
        if (model == null) return false;

        var toPlayer = player.position - eyePoint.position;
        if (toPlayer.magnitude > model.alertRange) return false;

        if (Physics.Raycast(eyePoint.position, toPlayer.normalized,
                out var hit, model.alertRange, -1, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.CompareTag("Player");
        }
        return false;
    }

        // Whether the enemy should chase.
    public static bool ShouldChase(EnemyModel model, float distanceToPlayer)
    {
        if (model == null) return false;
        return distanceToPlayer <= model.alertRange && !model.isDead;
    }

        // Whether the enemy should attack.
    public static bool ShouldAttack(EnemyModel model, float distanceToPlayer)
    {
        if (model == null) return false;
        return distanceToPlayer <= model.attackRange && !model.isDead;
    }

        // Calculate chase speed.
    public static float GetMoveSpeed(EnemyModel model)
    {
        if (model == null) return 0f;
        return model.state == EnemyState.Chase ? model.chaseSpeed : model.moveSpeed;
    }
}
