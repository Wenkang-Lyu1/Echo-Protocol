using System;
using UnityEngine;

/// <summary>Firing rules, separate from the bullet's damage and flight attributes.</summary>
[Serializable]
public sealed class WeaponAttributes
{
    [Min(.01f)] public float fireInterval = .18f;
    [Range(0, 45)] public float spreadDegrees;
    [Range(0, 1)] public float shootingMoveMultiplier = .7f;
    public ProjectileAttributes projectile = new ProjectileAttributes();

    public void Validate()
    {
        if (float.IsNaN(fireInterval) || float.IsInfinity(fireInterval) || fireInterval <= 0 ||
            float.IsNaN(spreadDegrees) || float.IsInfinity(spreadDegrees) || spreadDegrees < 0 || spreadDegrees > 45 ||
            float.IsNaN(shootingMoveMultiplier) || float.IsInfinity(shootingMoveMultiplier) || shootingMoveMultiplier < 0 || shootingMoveMultiplier > 1 || projectile == null)
            throw new InvalidOperationException("Weapon attributes require a positive fire interval, valid spread and bullet attributes.");
        projectile.Validate();
    }
}

/// <summary>Independent state per equipped weapon; no scene or gameplay references.</summary>
public sealed class WeaponInstanceModel
{
    public double fireAccumulator;
    public float cooldown;
    public int shotsFired;
    public Vector3 lastMuzzlePosition, lastDirection;
}
