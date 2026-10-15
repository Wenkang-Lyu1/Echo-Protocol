using System;
using UnityEngine;

[Serializable]
public class WeaponModel : ModelBase
{
    public string displayName = "Rifle";
    public float damage = 25f;
    public float range = 150f;
    public float fireRate = 8f;
    public int magazineSize = 30;
    public float reloadTime = 1.8f;
    public float recoilKick = 0.6f;
    public float recoilHorizontal = 0.15f;
    public float spread = 0.02f;
    public int pellets = 1;
    public bool isAutomatic;

        // Runtime state
    public int currentAmmo;
    public int reserveAmmo = 90;
    public bool isReloading;
    public float fireCooldown;
    public float reloadTimer;

    public override void Reset()
    {
        currentAmmo = magazineSize;
        isReloading = false;
        fireCooldown = 0f;
        reloadTimer = 0f;
    }

    public bool CanFire => !isReloading && currentAmmo > 0 && fireCooldown <= 0f;
}
