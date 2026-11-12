using System;
using UnityEngine;

/// <summary>Bullet configuration. Every emitted bullet takes its own snapshot.</summary>
[Serializable]
public sealed class ProjectileAttributes
{
    [Min(0)] public float damage = 25;
    [Min(.01f)] public float speed = 70;
    [Min(.01f)] public float range = 65;
    [Min(.001f)] public float collisionRadius = .035f;
    [Min(.001f)] public float visualDiameter = .08f;
    public Material material;

    public ProjectileAttributes Copy() => (ProjectileAttributes)MemberwiseClone();

    public void Validate()
    {
        if (!Valid(damage) || damage < 0 || !Valid(speed) || speed <= 0 || !Valid(range) || range <= 0 ||
            !Valid(collisionRadius) || collisionRadius <= 0 || !Valid(visualDiameter) || visualDiameter <= 0)
            throw new InvalidOperationException("Bullet attributes must be finite; speed, range and dimensions must be positive.");
    }

    private static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
