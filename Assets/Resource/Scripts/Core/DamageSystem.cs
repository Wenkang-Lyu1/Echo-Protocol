using System;
using UnityEngine;

// ============================================================
// DamageSystem -- central dispatch point for damage.
// Converts HitArgs into calls against IDamageable.
// ============================================================
public interface IDamageable
{
    GameObject gameObject { get; }
    float hp { get; }
    bool isDead { get; }
    void ApplyDamage(float damage, GameObject source, bool isEcho);
}

[CreateAssetMenu(menuName = "EchoProtocol/DamageProfile")]
public class DamageProfile : ScriptableObject
{
    public float baseDamage = 25f;
    public float headMultiplier = 2f;
    public float bodyMultiplier = 1f;
    public float limbMultiplier = 0.7f;
    public bool allowFriendlyFire;
}

public class DamageSystem : MonoSingleton<DamageSystem>
{
    [SerializeField] private DamageProfile profile;

    public event Func<float, GameObject, GameObject, float> OnModifyDamage;

    public void Dispatch(HitArgs hit)
    {
        if (hit.target == null) return;

        var dmg = hit.damage > 0f ? hit.damage : profile != null ? profile.baseDamage : 10f;

                // Hit-zone damage multiplier.
        var hitBox = hit.target.GetComponent<HitBox>();
        if (hitBox != null)
            dmg = DamageUtil.ApplyHitMultiplier(dmg, hitBox.Type);
        else if (profile != null)
            dmg *= profile.bodyMultiplier;

        if (OnModifyDamage != null)
            dmg = OnModifyDamage.Invoke(dmg, hit.target, hit.source);

        var target = hit.target.GetComponent<IDamageable>();
        if (target != null)
            target.ApplyDamage(dmg, hit.source, hit.isEcho);
        else
            EventCenter.Instance.Dispatch(new GameEventArgs
            {
                eventType = GameEvent.Combat_HitConfirmed,
                data = hit
            });
    }
}

public class HitBox : MonoBehaviour
{
    [SerializeField] private HitBoxType type = HitBoxType.Body;
    [SerializeField] private IDamageable owner;

    public HitBoxType Type => type;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<IDamageable>();
    }

    public void OnHit(float damage, GameObject source, bool isEcho)
    {
        if (owner == null) return;
        DamageSystem.Instance?.Dispatch(new HitArgs(gameObject, source, damage, isEcho));
    }
}
