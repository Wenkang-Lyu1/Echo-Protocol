using System;
using UnityEngine;

// ============================================================
// PlayerControl -- controller for the player.
// Responsibilities:
//   1) Holds PlayerModel.
//   2) Handles input, movement, taking damage, death.
//   3) Cooperates with Weapon / Echo (via their respective Controls).
// ============================================================
[RequireComponent(typeof(CharacterController))]
public class PlayerControl : GlobalControl<PlayerControl, PlayerModel>, IDamageable
{
        [Header("Movement Parameters")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8.5f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float gravity = 25f;
    [SerializeField] private float jumpHeight = 1.2f;

        [Header("View")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private WeaponControl recoilWeapon;

    private CharacterController _cc;
    private CharacterMotor _motor;
    private float _invincibleTimer;

    public Transform CameraPivot => cameraPivot;
    public void SetRecoilWeapon(WeaponControl weapon) => recoilWeapon = weapon;

    // ---- IDamageable (see DamageSystem) ----
    public float Hp => model.hp;
    public bool IsDead => model.isDead;

    // WarControl owns and ticks this sub-module.
    protected override bool AutoRegister => false;

    protected override void OnInitialize()
    {
        base.OnInitialize();
        _cc = GetComponent<CharacterController>();
        if (cameraPivot == null)
            cameraPivot = transform.Find("CameraPivot") ?? transform;

        _motor = CreateMotor(gameObject, cameraPivot);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void Tick(float deltaTime)
    {
        if (model.isDead) return;

        if (_invincibleTimer > 0f) _invincibleTimer -= deltaTime;

        _motor.Move(deltaTime);
        model.isCrouching = _motor.Crouching;
        model.isSprinting = _motor.Sprinting;
        var recoil = recoilWeapon != null ? recoilWeapon.ConsumeRecoil() : Vector3.zero;
        if (recoil != Vector3.zero)
            cameraPivot.localRotation *= Quaternion.Euler(-recoil.y * 2f, recoil.x * 2f, 0f);

                // Sync model position.
        model.position = transform.position;
        model.rotation = transform.rotation;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Player_Moved,
            data = new ValueArgs<Vector3>(transform.position)
        });
    }

    public CharacterMotor CreateMotor(GameObject actor, Transform pivot)
    {
        var motor = actor.GetComponent<CharacterMotor>() ?? actor.AddComponent<CharacterMotor>();
        motor.walkSpeed = walkSpeed; motor.sprintSpeed = sprintSpeed; motor.crouchSpeed = crouchSpeed;
        motor.gravity = gravity; motor.minPitch = minPitch; motor.maxPitch = maxPitch;
        motor.pivot = pivot;
        motor.ResetMotion();
        return motor;
    }

        // ---- Public Interface ----
    public void ApplyDamage(float damage)
    {
        if (model.isDead || _invincibleTimer > 0f) return;

        model.hp = Mathf.Max(0f, model.hp - damage);
        _invincibleTimer = 0.5f;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Player_Hurt,
            data = new ValueArgs<float>(model.HpRatio)
        });

        if (model.hp <= 0f)
        {
            model.isDead = true;
            EventCenter.Instance.Dispatch(new GameEventArgs { eventType = GameEvent.Player_Died });
        }
    }

    public void Heal(float amount)
    {
        model.hp = Mathf.Min(model.maxHp, model.hp + amount);
    }

    public override void ResetState()
    {
        base.ResetState();
        _invincibleTimer = 0;
        if (_motor != null) _motor.ResetMotion();
    }

    public void ResetPose(Vector3 position, Quaternion rotation)
    {
        _cc.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        _cc.enabled = true;
        cameraPivot.localRotation = Quaternion.identity;
        _motor.ResetMotion();
        model.position = position;
        model.rotation = rotation;
    }
}
