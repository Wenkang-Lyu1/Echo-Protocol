using UnityEngine;

/// <summary>Scene-owned upload tuning. Runtime progress lives in LevelModel.</summary>
public sealed class UploadNode : MonoBehaviour
{
    public string nodeId;
    public string displayName;
    [Min(.1f)] public float uploadSeconds = 5;
    [Min(.1f)] public float radius = 2.6f;
    [Min(.1f)] public float heightTolerance = 1.25f;
    public Renderer beacon;
    public Material availableMaterial, uploadingMaterial, completedMaterial, destroyedMaterial;
    public TextMesh label;
    private bool _cachedPose;
    private Quaternion _displayPose;

    public bool Contains(Vector3 position)
    {
        Vector3 delta = position - transform.position;
        return Mathf.Abs(delta.y) <= heightTolerance && new Vector2(delta.x, delta.z).sqrMagnitude <= radius * radius;
    }

    public void Present(UploadNodeState state, float progress)
    {
        if (beacon != null)
        {
            if (!_cachedPose) { _displayPose = beacon.transform.localRotation; _cachedPose = true; }
            beacon.transform.localRotation = state == UploadNodeState.Destroyed ? _displayPose * Quaternion.Euler(-32, 0, 20) : _displayPose;
        }
        if (beacon != null) beacon.sharedMaterial = state == UploadNodeState.Destroyed ? destroyedMaterial :
            state == UploadNodeState.Uploaded ? completedMaterial : state == UploadNodeState.Uploading ? uploadingMaterial : availableMaterial;
        if (label != null) label.text = displayName + "\n" + (state == UploadNodeState.Destroyed ? "OFFLINE" :
            state == UploadNodeState.Uploaded ? "ONLINE" : state == UploadNodeState.Uploading ? $"UPLOADING {progress / uploadSeconds:P0}" : $"HOLD AREA / {uploadSeconds:0.#}s");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * heightTolerance * .5f, new Vector3(radius * 2, heightTolerance, radius * 2));
    }
}
