using UnityEngine;

/// <summary>Visual-only actions, sampled once by the owning combat controller. Never moves the actor or deals damage.</summary>
public sealed class EnemyActionPresentation
{
    private readonly Animation _animation;
    private readonly Transform _visual;
    private readonly Transform[] _bones;
    private readonly Vector3[] _restPositions, _restScales, _previousPositions;
    private readonly Quaternion[] _restRotations, _previousRotations;
    private readonly Vector3[] _basePositions, _baseScales;
    private readonly Quaternion[] _baseRotations;
    private readonly bool[] _upperBodyBones;
    private readonly AnimationClip _idle, _move, _fire, _die;
    private readonly Vector3 _position;
    private readonly Quaternion _rotation;
    private readonly Transform _leftThigh, _rightThigh, _leftCalf, _rightCalf;
    private readonly Transform _actor, _weapon, _muzzle, _leftArm, _rightArm, _leftElbow, _rightElbow, _leftHand, _rightHand;
    private readonly bool _proceduralAim;
    private readonly Transform _spine;
    private AnimationClip _current;
    private float _time, _blend, _stride, _shotAge = 100, _hitAge = 100, _deathTime;
    private bool _dead;
    private bool _shotPosePending;

    public bool DeathFinished => _dead && _deathTime >= DeathDuration;
    public bool IsShooting => !_dead && _shotAge < (_fire != null ? _fire.length : .22f);
    private float DeathDuration => _die != null ? _die.length : .85f;

    public EnemyActionPresentation(GameObject actor, bool boss)
    {
        _actor = actor.transform; _proceduralAim = boss;
        _animation = actor.GetComponentInChildren<Animation>(true);
        if (_animation == null) return;
        _visual = _animation.transform;
        _position = _visual.localPosition; _rotation = _visual.localRotation;
        _bones = _visual.GetComponentsInChildren<Transform>(true);
        _restPositions = new Vector3[_bones.Length]; _restScales = new Vector3[_bones.Length];
        _restRotations = new Quaternion[_bones.Length];
        _previousPositions = new Vector3[_bones.Length]; _previousRotations = new Quaternion[_bones.Length];
        _basePositions = new Vector3[_bones.Length]; _baseScales = new Vector3[_bones.Length];
        _baseRotations = new Quaternion[_bones.Length]; _upperBodyBones = new bool[_bones.Length];
        for (int i = 0; i < _bones.Length; i++)
        {
            _restPositions[i] = _bones[i].localPosition; _restRotations[i] = _bones[i].localRotation; _restScales[i] = _bones[i].localScale;
            switch (_bones[i].name)
            {
                case "Bip001 L Thigh": _leftThigh = _bones[i]; break;
                case "Bip001 R Thigh": _rightThigh = _bones[i]; break;
                case "Bip001 L Calf": _leftCalf = _bones[i]; break;
                case "Bip001 R Calf": _rightCalf = _bones[i]; break;
                case "Bip001_Weapon": _weapon = _bones[i]; break;
                case "fire_01": _muzzle = _bones[i]; break;
                case "Bip001 L UpperArm": _leftArm = _bones[i]; break;
                case "Bip001 R UpperArm": _rightArm = _bones[i]; break;
                case "Bip001 L Forearm": _leftElbow = _bones[i]; break;
                case "Bip001 R Forearm": _rightElbow = _bones[i]; break;
                case "Bip001 L Hand": _leftHand = _bones[i]; break;
                case "Bip001 R Hand": _rightHand = _bones[i]; break;
                case "Bip001 Spine": _spine = _bones[i]; break;
            }
        }
        for (int i = 0; i < _bones.Length; i++)
            _upperBodyBones[i] = (_spine != null && _bones[i].IsChildOf(_spine)) ||
                (_weapon != null && _bones[i].IsChildOf(_weapon));
        var set = boss ? null : Resources.Load<AnimClipSet>("Animation/EnemyAtsuko");
        _idle = set != null && set.idle != null ? set.idle : _animation.clip;
        if (set != null) { _move = set.run; _fire = set.fire; _die = set.die; }
        Reset();
    }

    public void Reset()
    {
        _dead = _shotPosePending = false; _time = _blend = _stride = _deathTime = 0; _shotAge = _hitAge = 100; _current = null;
        if (_visual == null) return;
        _animation.Stop(); _animation.enabled = false;
        RestoreBones();
        if (_idle != null) _idle.SampleAnimation(_visual.gameObject, 0);
        _visual.localPosition = _position; _visual.localRotation = _rotation;
    }

    public void OnShot() { if (!_dead) { _shotAge = 0; _shotPosePending = true; } }
    public void OnHit() { if (!_dead) _hitAge = 0; }
    public void OnDeath()
    {
        _dead = true; _deathTime = 0;
        if (_die != null) SwitchClip(_die);
        else CapturePose();
        TickDeath(0);
    }

    public void Tick(float dt, float speed, float configuredSpeed)
    {
        if (_visual == null || _dead || dt < 0) return;
        _shotAge += dt; _hitAge += dt;
        bool moving = speed > .12f;
        bool layeredFire = IsShooting && _fire != null && moving && _move != null && _spine != null;
        var clip = IsShooting && _fire != null && !layeredFire ? _fire : moving && _move != null ? _move : _idle;
        SwitchClip(clip);
        // A projectile may be emitted immediately after Tick(0): its pose must already be firing.
        if (_shotPosePending && clip == _fire) _blend = 1;
        float rate = moving ? Mathf.Clamp(speed / Mathf.Max(.1f, configuredSpeed), .35f, 1.5f) : 1;
        _time += dt * rate; _stride += dt * Mathf.Lerp(5, 11, Mathf.Clamp01(speed / 7));
        Sample(clip, clip == _fire ? Mathf.Min(_shotAge, clip.length) : _time, dt);
        if (layeredFire) SampleMovingFire();
        if (moving && _move == null)
        {
            float step = Mathf.Sin(_stride);
            Rotate(_leftThigh, step * 24); Rotate(_rightThigh, -step * 24);
            Rotate(_leftCalf, Mathf.Max(0, -step) * 32); Rotate(_rightCalf, Mathf.Max(0, step) * 32);
        }
        float recoil = Mathf.Clamp01(1 - _shotAge / .13f);
        float hit = Mathf.Sin(Mathf.Clamp01(_hitAge / .22f) * Mathf.PI) * Mathf.Clamp01(1 - _hitAge / .22f);
        float bob = moving && _move == null ? Mathf.Abs(Mathf.Sin(_stride)) * .035f : 0;
        _visual.localPosition = _position + Vector3.up * bob + Vector3.forward * (recoil * .035f + hit * .04f);
        _visual.localRotation = _rotation * Quaternion.Euler(recoil * -5 + hit * -12, 0, hit * 7);
        if (_proceduralAim) AimWeapon(recoil);
        _shotPosePending = false;
    }

    private void SampleMovingFire()
    {
        for (int i = 0; i < _bones.Length; i++)
        {
            _basePositions[i] = _bones[i].localPosition; _baseRotations[i] = _bones[i].localRotation;
            _baseScales[i] = _bones[i].localScale;
            // Sparse firing clips must not inherit walking-only finger/arm channels.
            if (_upperBodyBones[i])
            {
                _bones[i].localPosition = _restPositions[i]; _bones[i].localRotation = _restRotations[i];
                _bones[i].localScale = _restScales[i];
            }
        }
        _fire.SampleAnimation(_visual.gameObject, Mathf.Min(_shotAge, _fire.length));
        // Full shooting pose on the emission frame; release the upper body gently at the end.
        float weight = Mathf.Clamp01((_fire.length - _shotAge) / .1f);
        for (int i = 0; i < _bones.Length; i++)
        {
            float w = _upperBodyBones[i] ? weight : 0;
            _bones[i].localPosition = Vector3.Lerp(_basePositions[i], _bones[i].localPosition, w);
            _bones[i].localRotation = Quaternion.Slerp(_baseRotations[i], _bones[i].localRotation, w);
            _bones[i].localScale = Vector3.Lerp(_baseScales[i], _bones[i].localScale, w);
        }
    }

    public void TickDeath(float dt)
    {
        if (!_dead) return;
        _deathTime = Mathf.Min(DeathDuration, _deathTime + Mathf.Max(0, dt));
        if (_visual == null) return;
        if (_die == null)
        {
            for (int i = 0; i < _bones.Length; i++)
            { _bones[i].localPosition = _previousPositions[i]; _bones[i].localRotation = _previousRotations[i]; }
            float t = Mathf.SmoothStep(0, 1, _deathTime / DeathDuration);
            _visual.localRotation = _rotation * Quaternion.Euler(-82 * t, 0, 12 * t);
            _visual.localPosition = _position + Vector3.down * (.12f * t);
        }
        else
        {
            Sample(_die, _deathTime, dt);
            _visual.localPosition = _position; _visual.localRotation = _rotation;
        }
    }

    private static void Rotate(Transform bone, float degrees)
    {
        if (bone != null) bone.localRotation *= Quaternion.Euler(0, 0, degrees);
    }

    private void AimWeapon(float recoil)
    {
        if (_weapon == null || _muzzle == null || _leftArm == null || _rightArm == null) return;
        var forward = -_actor.forward;
        var chest = (_leftArm.position + _rightArm.position) * .5f;
        _weapon.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Inverse(_muzzle.rotation) * _weapon.rotation;
        _weapon.position = chest + forward * (.22f - recoil * .025f) + _actor.right * .06f - Vector3.up * .08f;
        AimArm(_rightArm, _rightElbow, _rightHand, _weapon.position, chest);
        AimArm(_leftArm, _leftElbow, _leftHand, _weapon.position + forward * .17f - _actor.right * .03f, chest);
    }

    // Two-bone arm pose: keep the authored bone lengths and bend elbows out from the chest.
    private static void AimArm(Transform upper, Transform elbow, Transform hand, Vector3 target, Vector3 chest)
    {
        if (upper == null || elbow == null || hand == null) return;
        var shoulder = upper.position;
        float a = Vector3.Distance(shoulder, elbow.position), b = Vector3.Distance(elbow.position, hand.position);
        if (a < .001f || b < .001f || (target - shoulder).sqrMagnitude < .000001f) return;
        var direction = (target - shoulder).normalized;
        float distance = Mathf.Clamp(Vector3.Distance(shoulder, target), Mathf.Abs(a - b) + .001f, a + b - .001f);
        float cos = Mathf.Clamp((a * a + distance * distance - b * b) / (2 * a * distance), -1, 1);
        var bend = Vector3.ProjectOnPlane((shoulder - chest).normalized + Vector3.down * .5f, direction).normalized;
        var elbowTarget = shoulder + direction * (a * cos) + bend * (a * Mathf.Sqrt(1 - cos * cos));
        upper.rotation = Quaternion.FromToRotation(elbow.position - shoulder, elbowTarget - shoulder) * upper.rotation;
        elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, shoulder + direction * distance - elbow.position) * elbow.rotation;
    }

    private void SwitchClip(AnimationClip clip)
    {
        if (_current == clip) return;
        CapturePose();
        _current = clip; _time = _blend = 0;
    }

    private void CapturePose()
    {
        if (_bones != null)
            for (int i = 0; i < _bones.Length; i++)
            { _previousPositions[i] = _bones[i].localPosition; _previousRotations[i] = _bones[i].localRotation; }
    }

    private void Sample(AnimationClip clip, float time, float dt)
    {
        RestoreBones();
        if (clip != null)
        {
            bool loop = clip == _idle || clip == _move;
            clip.SampleAnimation(_visual.gameObject, loop && clip.length > 0 ? Mathf.Repeat(time, clip.length) : Mathf.Min(time, clip.length));
        }
        _blend = Mathf.Min(1, _blend + dt / .1f);
        if (_blend >= 1) return;
        for (int i = 0; i < _bones.Length; i++)
        {
            _bones[i].localPosition = Vector3.Lerp(_previousPositions[i], _bones[i].localPosition, _blend);
            _bones[i].localRotation = Quaternion.Slerp(_previousRotations[i], _bones[i].localRotation, _blend);
        }
    }

    private void RestoreBones()
    {
        for (int i = 0; i < _bones.Length; i++)
        {
            _bones[i].localPosition = _restPositions[i]; _bones[i].localRotation = _restRotations[i]; _bones[i].localScale = _restScales[i];
        }
    }
}
