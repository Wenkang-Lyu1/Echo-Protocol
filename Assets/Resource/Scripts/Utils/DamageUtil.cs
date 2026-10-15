using UnityEngine;

// ============================================================
// DamageUtil -- damage calculation utility.
// Pure static, stateless.
// ============================================================
public static class DamageUtil
{
        // Hit-zone multiplier.
    public static float ApplyHitMultiplier(float damage, HitBoxType type)
    {
        switch (type)
        {
            case HitBoxType.Head: return damage * 2f;
            case HitBoxType.Body: return damage;
            case HitBoxType.Limb: return damage * 0.7f;
            default: return damage;
        }
    }

        // Vulnerability bonus.
    public static float ApplyVulnerability(float damage, float vulnerability)
    {
        return damage * (1f + vulnerability);
    }

        // Armor reduction.
    public static float ApplyArmor(float damage, float armor, float maxArmor = 100f)
    {
        var factor = 1f - Mathf.Clamp01(armor / maxArmor);
        return damage * factor;
    }

        // Critical hit check.
    public static bool RollCritical(float critChance)
    {
        return UnityEngine.Random.value < Mathf.Clamp01(critChance);
    }

        // Full damage pipeline.
    public static float CalculateFinalDamage(float baseDamage, HitBoxType hitType,
        float vulnerability, float armor, float critChance, out bool isCrit)
    {
        isCrit = RollCritical(critChance);
        var dmg = baseDamage * (isCrit ? 2f : 1f);
        dmg = ApplyHitMultiplier(dmg, hitType);
        dmg = ApplyVulnerability(dmg, vulnerability);
        dmg = ApplyArmor(dmg, armor);
        return Mathf.Max(0f, dmg);
    }
}

public enum HitBoxType { Head, Body, Limb }
