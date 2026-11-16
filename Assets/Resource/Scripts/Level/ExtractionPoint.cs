using UnityEngine;

public sealed class ExtractionPoint : MonoBehaviour
{
    [Min(.1f)] public float radius = 2;
    [Min(.1f)] public float heightTolerance = 1.25f;
    public Renderer barrier;
    public Material lockedMaterial, openMaterial;
    public TextMesh label;
    public string displayName = "EXTRACTION";

    public bool Contains(Vector3 position)
    {
        Vector3 delta = position - transform.position;
        return Mathf.Abs(delta.y) <= heightTolerance && new Vector2(delta.x, delta.z).sqrMagnitude <= radius * radius;
    }

    public void Present(bool open)
    {
        if (barrier != null) barrier.sharedMaterial = open ? openMaterial : lockedMaterial;
        if (label != null) label.text = displayName + (open ? "\nOPEN / ENTER TO EXTRACT" : "\nLOCKED");
    }
}
