using System;
using UnityEngine;

public partial class EchoControl
{
    public const float SegmentLimit = 20f;
    private PlayerControl _body;
    private GameObject _visualPrefab;
    private Material _preparationMaterial;
    private Transform _visualRoot;
    private GameObject _controlled;
    private CharacterMotor _motor;
    private Transform _camera;
    private Transform _cameraParent;
    private Vector3 _cameraPosition;
    private Quaternion _cameraRotation;
    private GameObject _bodyShadow;
    private EchoSegment _segment;
    private float _sampleClock;

    public void InitializeForPreparation(PlayerControl body)
    {
        _body = body;
        Initialize();
    }

    public void BeginPreparation(PlayerControl body, GameObject visual, Material material, Transform root)
    {
        ResetState();
        _body = body;
        _visualPrefab = visual;
        _preparationMaterial = material;
        _visualRoot = root;
        model.isPreparing = true;
    }

    public bool ToggleControl()
    {
        if (!model.isPreparing) return false;
        if (model.isRecording) { SaveSegment(); return true; }
        if (_body == null || _body.CameraPivot == null || _visualPrefab == null) return false;
        try
        {
            _controlled = new GameObject("Controlled echo");
            _controlled.transform.SetParent(_visualRoot, false);
            _controlled.transform.SetPositionAndRotation(_body.transform.position, _body.transform.rotation);
            // Exclude body/echo mutual collisions while retaining collisions against the world.
            _controlled.layer = 2;
            var source = _body.GetComponent<CharacterController>();
            var cc = _controlled.AddComponent<CharacterController>();
            cc.height = source.height; cc.center = source.center; cc.radius = source.radius;
            cc.stepOffset = source.stepOffset; cc.skinWidth = source.skinWidth;
            Physics.IgnoreCollision(source, cc);
            var movingVisual = MakeShadow(_controlled.transform);
            foreach (var t in movingVisual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            _bodyShadow = MakeShadow(_visualRoot);
            _bodyShadow.transform.SetPositionAndRotation(_body.transform.position, _body.transform.rotation);
            _camera = _body.CameraPivot;
            _cameraParent = _camera.parent;
            _cameraPosition = _camera.localPosition;
            _cameraRotation = _camera.localRotation;
            var pivot = new GameObject("Echo camera pivot").transform;
            pivot.SetParent(_controlled.transform, false);
            pivot.localPosition = _cameraPosition;
            pivot.localRotation = _cameraRotation;
            _camera.SetParent(pivot, false);
            _camera.localPosition = Vector3.zero;
            _camera.localRotation = Quaternion.identity;
            _motor = _body.CreateMotor(_controlled, pivot);
            _segment = new EchoSegment { id = Guid.NewGuid().ToString("N"), startTime = model.cursor };
            model.frames.Clear();
            model.recordDuration = _sampleClock = 0;
            model.isRecording = true;
            CapturePose(0);
            return true;
        }
        catch (Exception ex)
        {
            ReturnToBody();
            Debug.LogException(ex);
            return false;
        }
    }

    public void TickPreparation(float dt)
    {
        if (!model.isPreparing || dt <= 0) return;
        if (!model.isRecording) { _body.Tick(dt); return; }
        float step = Mathf.Min(dt * model.selectedSpeed, SegmentLimit - model.recordDuration);
        _motor.Move(step, step / dt);
        model.recordDuration += step;
        model.cursor = _segment.startTime + model.recordDuration;
        _sampleClock += step;
        if (_sampleClock >= 1f / Mathf.Max(1, sampleRate) || model.recordDuration >= SegmentLimit)
        {
            CapturePose(model.recordDuration);
            _sampleClock = 0;
        }
        if (model.recordDuration >= SegmentLimit) SaveSegment();
    }

    private void CapturePose(float time)
    {
        var pose = new EchoFrame { time = time, position = _controlled.transform.position,
            rotation = _controlled.transform.rotation };
        if (model.frames.Count > 0 && Mathf.Approximately(model.frames[model.frames.Count - 1].time, time))
            model.frames[model.frames.Count - 1] = pose;
        else model.frames.Add(pose);
    }

    public bool SaveSegment()
    {
        if (!model.isPreparing || !model.isRecording) return false;
        CapturePose(model.recordDuration);
        _segment.duration = model.recordDuration;
        _segment.frames = model.frames.ToArray();
        model.segments.Add(_segment);
        ReturnToBody();
        return true;
    }

    public bool SetPreparationSpeed(float speed)
    {
        if (!model.isPreparing || (speed != .1f && speed != .3f && speed != 1f)) return false;
        model.selectedSpeed = speed;
        return true;
    }

    public void CyclePreparationSpeed() => SetPreparationSpeed(model.selectedSpeed == .1f ? .3f : model.selectedSpeed == .3f ? 1 : .1f);

    public bool SeekPreparation(float time)
    {
        if (!model.isPreparing || model.isRecording || float.IsNaN(time) || float.IsInfinity(time) || time < 0) return false;
        model.cursor = time;
        return true;
    }

    public bool DeleteSegment(string id)
    {
        if (!model.isPreparing || model.isRecording) return false;
        int index = model.segments.FindIndex(s => s.id == id);
        if (index < 0) return false;
        model.segments.RemoveAt(index);
        return true;
    }

    private GameObject MakeShadow(Transform parent)
    {
        var shadow = Instantiate(_visualPrefab, parent);
        foreach (var t in shadow.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        foreach (var renderer in shadow.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = _preparationMaterial;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return shadow;
    }

    private void ReturnToBody()
    {
        if (_camera != null)
        {
            _camera.SetParent(_cameraParent, false);
            _camera.localPosition = _cameraPosition;
            _camera.localRotation = _cameraRotation;
        }
        if (_controlled != null) { _controlled.SetActive(false); Destroy(_controlled); }
        if (_bodyShadow != null) { _bodyShadow.SetActive(false); Destroy(_bodyShadow); }
        _controlled = _bodyShadow = null;
        _motor = null; _camera = null; _segment = null;
        model.isRecording = false;
    }

    private void ClearPreparation()
    {
        ReturnToBody();
        model.isPreparing = false;
    }

    protected override void OnTeardown() => ClearPreparation();
}
