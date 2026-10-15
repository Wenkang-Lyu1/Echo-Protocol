using System;
using UnityEngine;

// ============================================================
// AnimDriver -- AnimatorOverrideController, approach B.
// Conventions:
//   - The Animator Controller only holds state slots; no concrete clips are dragged in.
//   - Concrete clips are injected by AnimClipSet (ScriptableObject).
//   - The gameplay layer only calls semantic APIs: Play / SetMoveSpeed / ...
// ============================================================
[RequireComponent(typeof(Animator))]
public class AnimDriver : MonoBehaviour
{
    [SerializeField] private AnimClipSet defaultClipSet;

    private Animator _animator;
    private AnimatorOverrideController _override;
    private bool _hasOverride;

    public bool isInitialized => _animator != null && _hasOverride;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            // $"[AnimDriver] {name} is missing an Animator or a base Controller is not assigned"
            return;
        }

        _override = new AnimatorOverrideController(_animator.runtimeAnimatorController);
        _override.name = $"{name}_Override";
        _animator.runtimeAnimatorController = _override;
        _hasOverride = true;

        if (defaultClipSet != null)
            Bind(defaultClipSet);
    }

    public void Bind(AnimClipSet set)
    {
        if (!_hasOverride || set == null) return;
        OverrideIfSet("Idle", set.idle);
        OverrideIfSet("Run", set.run);
        OverrideIfSet("Fire", set.fire);
        OverrideIfSet("Reload", set.reload);
        OverrideIfSet("Hit", set.hit);
        OverrideIfSet("Die", set.die);
        OverrideIfSet("Crouch", set.crouch);
        OverrideIfSet("Jump", set.jump);
        OverrideIfSet("Recording", set.recording);
        OverrideIfSet("Playback", set.playback);
        _animator.runtimeAnimatorController = _override;
    }

    private void OverrideIfSet(string stateName, AnimationClip clip)
    {
        if (clip == null) return;
        _override[stateName] = clip;
    }

    public void Play(string state, float fade = 0.1f)
    {
        if (!_hasOverride) return;
        _animator.CrossFade(state, Mathf.Clamp01(fade));
    }

    // Accepts any static string-constants type (EchoAnimState, EnemyAnimState, ...).
    // The generic parameter is constrained to a class with no instance state,
    // which avoids CS0718 (static types cannot be used as type arguments).
    public void Play<T>(float fade = 0.08f) where T : class, IAnimStateToken, new()
    {
        Play(new T().Name, fade);
    }

    // Explicit state token so callers pass a type, not a static class.
    public interface IAnimStateToken
    {
        string Name { get; }
    }

    public void SetMoveSpeed(float speed)
    {
        if (!_hasOverride) return;
        _animator.SetFloat("MoveSpeed", Mathf.Clamp01(speed));
    }

    public void SetCrouch(bool crouching)
    {
        if (!_hasOverride) return;
        _animator.SetBool("Crouch", crouching);
    }

    public void SetAimWeight(float w)
    {
        if (!_hasOverride) return;
        _animator.SetLayerWeight(1, Mathf.Clamp01(w));
    }

    public void SetTrigger(string trigger)
    {
        if (!_hasOverride) return;
        _animator.SetTrigger(trigger);
    }
}
