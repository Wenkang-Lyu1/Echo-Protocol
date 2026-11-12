using UnityEngine;

/// <summary>Instance movement state shared by the body and the controlled echo. No singleton.</summary>
[RequireComponent(typeof(CharacterController))]
public sealed class CharacterMotor : MonoBehaviour
{
    public float walkSpeed = 5, sprintSpeed = 8.5f, crouchSpeed = 2.5f;
    public float gravity = 25, minPitch = -80, maxPitch = 80;
    public Transform pivot;
    public float movementMultiplier = 1;
    public bool Crouching { get; private set; }
    public bool Sprinting { get; private set; }
    private CharacterController _controller;
    private float _vertical, _pitch;

    public void ResetMotion()
    {
        _vertical = 0;
        movementMultiplier = 1;
        _pitch = pivot != null ? Mathf.DeltaAngle(0, pivot.localEulerAngles.x) : 0;
    }

    public void Move(float dt, float lookScale = 1)
    {
        if (dt <= 0) return;
        if (_controller == null) _controller = GetComponent<CharacterController>();
        var input = InputManager.Instance;
        var look = input.LookInput * (input.LookSensitivity * 100 * lookScale);
        transform.Rotate(Vector3.up * look.x);
        _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);
        if (pivot != null) pivot.localRotation = Quaternion.Euler(_pitch, 0, 0);
        Crouching = input.CrouchHeld;
        Sprinting = input.SprintHeld && !Crouching;
        _controller.height = Crouching ? 1.2f : 1.8f;
        var direction = transform.forward * input.MoveInput.y + transform.right * input.MoveInput.x;
        if (direction.sqrMagnitude > 1) direction.Normalize();
        var speed = (Crouching ? crouchSpeed : Sprinting ? sprintSpeed : walkSpeed) * Mathf.Clamp01(movementMultiplier);
        if (_controller.isGrounded && _vertical < 0) _vertical = -1;
        else _vertical -= gravity * dt;
        _controller.Move((direction * speed + Vector3.up * _vertical) * dt);
    }
}
