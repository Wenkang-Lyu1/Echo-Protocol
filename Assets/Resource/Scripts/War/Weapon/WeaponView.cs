using TMPro;
using UnityEngine;

// ============================================================
// WeaponView -- visual presentation layer for weapons.
// Reads ammo data through WeaponControl.GetModel().
// ============================================================
public class WeaponView : ViewBase
{
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI reserveText;
    [SerializeField] private UnityEngine.UI.Slider reloadBar;
    [SerializeField] private GameObject reloadIndicator;
    [SerializeField] private Animator weaponAnimator;

    WeaponModel _weaponModel;

    protected override void Subscribe()
    {
        EventCenter.Instance.AddListener(GameEvent.Weapon_AmmoChanged, OnAmmoChanged);
        EventCenter.Instance.AddListener(GameEvent.Weapon_Reload, OnReload);
        EventCenter.Instance.AddListener(GameEvent.Weapon_Reloaded, OnReloaded);
        EventCenter.Instance.AddListener(GameEvent.Weapon_Fired, OnFired);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Weapon_AmmoChanged, OnAmmoChanged);
        EventCenter.Instance.RemoveListener(GameEvent.Weapon_Reload, OnReload);
        EventCenter.Instance.RemoveListener(GameEvent.Weapon_Reloaded, OnReloaded);
        EventCenter.Instance.RemoveListener(GameEvent.Weapon_Fired, OnFired);
    }

    protected override void OnRefresh()
    {
        _weaponModel = _model as WeaponModel;
        SyncAmmo();
    }

    void Update()
    {
        SyncReloadBar();
    }

    void SyncAmmo()
    {
        if (_weaponModel == null) return;
        if (ammoText != null) ammoText.text = _weaponModel.currentAmmo.ToString();
        if (reserveText != null) reserveText.text = _weaponModel.reserveAmmo.ToString();
    }

    void SyncReloadBar()
    {
        if (reloadBar == null || _weaponModel == null) return;
        if (_weaponModel.isReloading && _weaponModel.reloadTime > 0f)
        {
            reloadBar.gameObject.SetActive(true);
            reloadBar.value = 1f - (_weaponModel.reloadTimer / _weaponModel.reloadTime);
        }
        else
        {
            reloadBar.gameObject.SetActive(false);
        }
    }

    void OnAmmoChanged(GameEventArgs args)
    {
        if (args.data is IntPairArgs p)
        {
            if (ammoText != null) ammoText.text = p.current.ToString();
            if (reserveText != null) reserveText.text = p.total.ToString();
        }
    }

    void OnReload(GameEventArgs args)
    {
        if (reloadIndicator != null) reloadIndicator.SetActive(true);
    }

    void OnReloaded(GameEventArgs args)
    {
        if (reloadIndicator != null) reloadIndicator.SetActive(false);
    }

    void OnFired(GameEventArgs args)
    {
        if (weaponAnimator != null) weaponAnimator.SetTrigger("Fire");
    }
}
