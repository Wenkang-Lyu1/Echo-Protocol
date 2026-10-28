using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-owned enemy freeze snapshot. No singleton or autonomous combat in preparation.</summary>
public sealed class PreparationEnemy : MonoBehaviour
{
    private Renderer[] _renderers;
    private Material[][] _materials;
    private Behaviour[] _behaviours;
    private bool[] _enabled;
    private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
    private readonly List<bool> _kinematic = new List<bool>();
    private Vector3 _position;
    private Quaternion _rotation;
    public bool IsFrozen { get; private set; }

    public void Freeze(Material material)
    {
        if (IsFrozen) return;
        _position = transform.position; _rotation = transform.rotation;
        // Sample the authored idle/action pose before stopping legacy playback.
        foreach (var animation in GetComponentsInChildren<Animation>(true))
            if (animation.clip != null && !animation.isPlaying) animation.clip.SampleAnimation(animation.gameObject, 0);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _materials = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
        {
            _materials[i] = _renderers[i].sharedMaterials;
            var shadows = new Material[_materials[i].Length];
            for (int j = 0; j < shadows.Length; j++) shadows[j] = material;
            _renderers[i].sharedMaterials = shadows;
        }
        _behaviours = GetComponentsInChildren<Behaviour>(true);
        _enabled = new bool[_behaviours.Length];
        for (int i = 0; i < _behaviours.Length; i++)
        {
            _enabled[i] = _behaviours[i].enabled;
            if (_behaviours[i] != this) _behaviours[i].enabled = false;
        }
        foreach (var body in GetComponentsInChildren<Rigidbody>(true))
        {
            _bodies.Add(body); _kinematic.Add(body.isKinematic); body.isKinematic = true;
        }
        IsFrozen = true;
    }

    public void Restore()
    {
        if (!IsFrozen) return;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].sharedMaterials = _materials[i];
        for (int i = 0; i < _behaviours.Length; i++)
            if (_behaviours[i] != null && _behaviours[i] != this) _behaviours[i].enabled = _enabled[i];
        for (int i = 0; i < _bodies.Count; i++) if (_bodies[i] != null) _bodies[i].isKinematic = _kinematic[i];
        transform.SetPositionAndRotation(_position, _rotation);
        _bodies.Clear(); _kinematic.Clear();
        IsFrozen = false;
    }

    private void OnDestroy() => Restore();
}
