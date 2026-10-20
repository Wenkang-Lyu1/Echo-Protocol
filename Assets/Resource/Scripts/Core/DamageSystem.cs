using UnityEngine;

// ============================================================
// DamageSystem -- the one entry point for "something was hit".
//
//     DamageSystem.Instance.Dispatch(new HitArgs(target, source, damage, isEcho));
//
// It applies the hit-box multiplier (Head / Body / Limb), lets listeners
// adjust the number through OnModifyDamage, and calls ApplyDamage on the
// target. A target that does not implement IDamageable still gets the
// Combat_HitConfirmed event, so nothing is silently lost.
//
// To make a thing damageable, implement IDamageable (see PlayerControl and
// EnemyControl). To mark head / limb zones, put a HitBox on a child collider.
// ============================================================
public interface IDamageable
{
    float Hp { get; }
    bool IsDead { get; }
    void ApplyDamage(float damage);
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

[DefaultExecutionOrder(-850)]
public class DamageSystem : MonoSingleton<DamageSystem>
{
    [SerializeField] private DamageProfile profile;

    // Last chance to change the number: (damage, target, source) => damage.
    public event System.Func<float, GameObject, GameObject, float> OnModifyDamage;

    public void Dispatch(HitArgs hit)
    {
        if (hit.target == null) return;

        var damage = hit.damage > 0f
            ? hit.damage
            : profile != null ? profile.baseDamage : 10f;

        var hitBox = hit.target.GetComponent<HitBox>();
        if (hitBox != null)
            damage = DamageUtil.ApplyHitMultiplier(damage, hitBox.Type);
        else if (profile != null)
            damage *= profile.bodyMultiplier;

        if (OnModifyDamage != null)
            damage = OnModifyDamage.Invoke(damage, hit.target, hit.source);

        var target = hit.target.GetComponentInParent<IDamageable>();
        if (target != null && !target.IsDead)
            target.ApplyDamage(damage);

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Combat_HitConfirmed,
            data = hit
        });
    }
}

// ============================================================
// HitBox -- optional marker on a child collider (Head / Limb).
// The damageable owner is found by walking up the hierarchy, so there is
// nothing to wire in the Inspector.
// ============================================================
public class HitBox : MonoBehaviour
{
    [SerializeField] private HitBoxType type = HitBoxType.Body;

    public HitBoxType Type => type;

    public void OnHit(float damage, GameObject source, bool isEcho)
    {
        DamageSystem.Instance?.Dispatch(new HitArgs(gameObject, source, damage, isEcho));
    }
}
