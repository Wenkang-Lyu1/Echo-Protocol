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
    private float _verticalVelocity;
    private float _pitch;
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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void Tick(float deltaTime)
    {
        if (model.isDead) return;

        if (_invincibleTimer > 0f) _invincibleTimer -= deltaTime;

        HandleLook();
        HandleMove(deltaTime);
        HandleCrouch();

        if (!_cc.isGrounded) _verticalVelocity -= gravity * deltaTime;
        else if (_verticalVelocity < 0f) _verticalVelocity = -1f;

        _cc.Move(Vector3.up * _verticalVelocity * deltaTime);

                // Sync model position.
        model.position = transform.position;
        model.rotation = transform.rotation;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Player_Moved,
            data = new ValueArgs<Vector3>(transform.position)
        });
    }

    private void HandleLook()
    {
        var input = InputManager.Instance;
        if (input == null) return;

        var look = input.LookInput * (input.LookSensitivity * 100f);
        transform.Rotate(Vector3.up * look.x);

        _pitch -= look.y;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

                // Recoil accumulation.
        var recoil = recoilWeapon != null ? recoilWeapon.ConsumeRecoil() : Vector3.zero;
        if (recoil != Vector3.zero)
            cameraPivot.localRotation *= Quaternion.Euler(-recoil.y * 2f, recoil.x * 2f, 0f);
    }

    private void HandleMove(float deltaTime)
    {
        var input = InputManager.Instance;
        var move = input != null ? input.MoveInput : Vector2.zero;
        var worldMove = transform.forward * move.y + transform.right * move.x;

        var speed = walkSpeed;
        if (input != null && input.SprintHeld && !model.isCrouching) speed = sprintSpeed;
        if (model.isCrouching) speed = crouchSpeed;

        if (worldMove.sqrMagnitude > 1f) worldMove.Normalize();
        _cc.Move(worldMove * speed * deltaTime);

        model.isSprinting = input != null && input.SprintHeld && !model.isCrouching;
    }

    private void HandleCrouch()
    {
        var input = InputManager.Instance;
        if (input == null) return;
        model.isCrouching = input.CrouchHeld;
        _cc.height = model.isCrouching ? 1.2f : 1.8f;
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

    public void ResetPose(Vector3 position, Quaternion rotation)
    {
        _cc.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        _cc.enabled = true;
        _verticalVelocity = _pitch = 0;
        cameraPivot.localRotation = Quaternion.identity;
        model.position = position;
        model.rotation = rotation;
    }
}
