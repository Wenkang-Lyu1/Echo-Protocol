using UnityEngine;
using UnityEngine.UI;

/// <summary>Isolated model camera composited over a UGUI background. Owns all preview resources.</summary>
public sealed class UIModelPreview : MonoBehaviour
{
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private RawImage output;
    [SerializeField, Range(0, 31)] private int previewLayer = 31;
    [SerializeField] private Vector3 modelEuler = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector2Int resolution = new Vector2Int(1000, 1100);
    private GameObject _stage;
    private RenderTexture _texture;

    public void Show()
    {
        if (_stage != null) { _stage.SetActive(true); return; }
        if (modelPrefab == null || output == null)
        {
            Debug.LogError(name + ": 主角预制体或 RawImage 未配置", this);
            return;
        }
        _stage = new GameObject("Login character preview");
        _stage.transform.SetParent(transform, false);
        _stage.transform.position = new Vector3(10000f, 10000f, 10000f);
        var character = Instantiate(modelPrefab, _stage.transform, false);
        character.transform.localRotation = Quaternion.Euler(modelEuler);
        foreach (var node in character.GetComponentsInChildren<Transform>(true))
            node.gameObject.layer = previewLayer;
        var renderers = character.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Release(); return; }
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

        _texture = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32);
        _texture.name = "Login character RT";
        _texture.Create();
        output.texture = _texture;
        var camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(_stage.transform, false);
        camera.transform.position = bounds.center + new Vector3(0f, 0f, -bounds.size.magnitude * 2f - 1f);
        camera.transform.LookAt(bounds.center);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x * resolution.y / resolution.x) * 1.08f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = bounds.size.magnitude * 5f + 10f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << previewLayer;
        camera.targetTexture = _texture;
        camera.allowHDR = false;
        camera.allowMSAA = false;
    }

    public void Hide() { if (_stage != null) _stage.SetActive(false); }

    public void Release()
    {
        if (output != null) output.texture = null;
        if (_stage != null) Destroy(_stage);
        _stage = null;
        if (_texture != null) { _texture.Release(); Destroy(_texture); }
        _texture = null;
    }

    private void OnDestroy() => Release();
}
