using System;
using UnityEngine;

// ============================================================
// WeaponControl -- controller for weapons.
// Responsibilities:
//   1) Holds WeaponModel (ammo / fire rate / recoil ...).
//   2) Handles firing, reloading, recoil.
//   3) Dispatches firing events through EventCenter.
//   4) Cooperates with PlayerControl (reads view direction).
// ============================================================
public class WeaponControl : GlobalControl<WeaponControl, WeaponModel>
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private TrailRenderer bulletTrail;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private LayerMask hitMask = -1;

    private Vector3 _recoilBias;

    // WarControl owns and ticks this sub-module.
    protected override bool AutoRegister => false;

    protected override void OnInitialize()
    {
        base.OnInitialize();
        if (muzzle == null) muzzle = transform;
    }

    public override void Tick(float deltaTime)
    {
        if (model.fireCooldown > 0f) model.fireCooldown -= deltaTime;
        if (model.isReloading)
        {
            model.reloadTimer -= deltaTime;
            if (model.reloadTimer <= 0f) FinishReload();
        }
    }

        // ---- Fire Interface ----
    public bool TryFire()
    {
        if (!model.CanFire) return false;

        var player = PlayerControl.Instance;
        if (player == null || player.CameraPivot == null) return false;

        var origin = player.CameraPivot.position;
        var direction = player.CameraPivot.forward;

        model.fireCooldown = 1f / Mathf.Max(0.1f, model.fireRate);
        model.currentAmmo--;

        for (var i = 0; i < model.pellets; i++)
        {
            var dir = WeaponUtil.ApplySpread(direction, model.spread);
            if (Physics.Raycast(origin, dir, out var r, model.range, hitMask, QueryTriggerInteraction.Ignore))
            {
                // Weapon_Fired carries the hit for views (muzzle flash, anim).
                // Damage is not applied here on purpose: send the same HitArgs
                // to DamageSystem.Instance.Dispatch() when damage is designed.
                EventCenter.Instance.Dispatch(new GameEventArgs
                {
                    eventType = GameEvent.Weapon_Fired,
                    data = new HitArgs(r.collider.gameObject, gameObject, model.damage, false)
                });
                SpawnTrail(muzzle.position, r.point);
            }
        }

        if (muzzleFlash != null) muzzleFlash.Play(true);

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Weapon_AmmoChanged,
            data = new IntPairArgs(model.currentAmmo, model.reserveAmmo)
        });

        WeaponUtil.ApplyRecoil(ref _recoilBias, model.recoilKick, model.recoilHorizontal);

        if (model.currentAmmo <= 0) TryReload();
        return true;
    }

    public void TryReload()
    {
        if (model.isReloading || model.currentAmmo >= model.magazineSize || model.reserveAmmo <= 0) return;
        model.isReloading = true;
        model.reloadTimer = model.reloadTime;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Weapon_Reload,
            data = new ValueArgs<float>(model.reloadTime)
        });
    }

    private void FinishReload()
    {
        var need = model.magazineSize - model.currentAmmo;
        var load = Mathf.Min(need, model.reserveAmmo);
        model.currentAmmo += load;
        model.reserveAmmo -= load;
        model.isReloading = false;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Weapon_Reloaded,
            data = new IntPairArgs(model.currentAmmo, model.reserveAmmo)
        });
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Weapon_AmmoChanged,
            data = new IntPairArgs(model.currentAmmo, model.reserveAmmo)
        });
    }

        // Exposed so PlayerView can read recoil.
    public Vector3 ConsumeRecoil()
    {
        var r = _recoilBias;
        _recoilBias = Vector3.Lerp(_recoilBias, Vector3.zero, Time.deltaTime * 6f);
        return r;
    }

    private void SpawnTrail(Vector3 from, Vector3 to)
    {
        if (bulletTrail == null) return;
        var t = Instantiate(bulletTrail, from, Quaternion.identity);
        t.AddPosition(to);
        Destroy(t.gameObject, 0.5f);
    }
}
