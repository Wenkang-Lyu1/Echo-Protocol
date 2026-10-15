using UnityEngine;

// ============================================================
// WeaponUtil -- weapon calculation utility.
// Pure static, stateless.
// ============================================================
public static class WeaponUtil
{
        // Apply spread.
    public static Vector3 ApplySpread(Vector3 direction, float spread)
    {
        return Quaternion.Euler(
            UnityEngine.Random.Range(-spread, spread) * 100f,
            UnityEngine.Random.Range(-spread, spread) * 100f,
            0f) * direction;
    }

        // Apply recoil.
    public static void ApplyRecoil(ref Vector3 bias, float kick, float horizontal)
    {
        bias.y += kick;
        bias.x += UnityEngine.Random.Range(-horizontal, horizontal);
    }

        // Damage calculation.
    public static float CalculateDamage(WeaponModel weapon, float distance, float armor)
    {
        var dmg = weapon.damage;
        dmg *= 1f - Mathf.Clamp01(distance / weapon.range);
        dmg *= (1f - armor * 0.01f);
        return Mathf.Max(0f, dmg);
    }

        // Whether firing is allowed.
    public static bool CanFire(WeaponModel weapon)
    {
        return !weapon.isReloading && weapon.currentAmmo > 0 && weapon.fireCooldown <= 0f;
    }

        // DPS estimation.
    public static float GetDps(WeaponModel weapon)
    {
        return weapon.damage * weapon.fireRate;
    }
}
