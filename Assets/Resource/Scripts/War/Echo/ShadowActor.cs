using UnityEngine;

/// <summary>Shared appearance lifecycle for player shadows and frozen enemy shadows.</summary>
public abstract class ShadowActor : CharacterActor
{
    private Renderer[] _shadowRenderers;
    private Material[][] _originalMaterials;
    public void ApplyShadowAppearance(Material material)
    {
        if (_shadowRenderers == null)
        {
            _shadowRenderers = GetComponentsInChildren<Renderer>(true);
            _originalMaterials = new Material[_shadowRenderers.Length][];
            for (int i = 0; i < _shadowRenderers.Length; i++) _originalMaterials[i] = _shadowRenderers[i].sharedMaterials;
        }
        for (int i = 0; i < _shadowRenderers.Length; i++)
        {
            var materials = new Material[_originalMaterials[i].Length];
            for (int j = 0; j < materials.Length; j++) materials[j] = material;
            _shadowRenderers[i].sharedMaterials = materials;
        }
    }
    public void RestoreAppearance()
    {
        if (_shadowRenderers == null) return;
        for (int i = 0; i < _shadowRenderers.Length; i++)
            if (_shadowRenderers[i] != null) _shadowRenderers[i].sharedMaterials = _originalMaterials[i];
    }
}
