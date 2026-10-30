using UnityEngine;

/// <summary>A player shadow. Preparation shots are visual only; damage belongs to future combat.</summary>
public sealed class PlayerEchoActor : ShadowActor
{
    public int PresentedShotCount { get; private set; }
    public void ShowShot(EchoFireEvent shot, Material material, bool showTracer = true)
    {
        Vector3 end = shot.muzzlePosition + shot.fireDirection * 65;
        if (Physics.Raycast(shot.muzzlePosition, shot.fireDirection, out var hit, 65, ~(1 << 2), QueryTriggerInteraction.Ignore))
            end = hit.point;
        var trail = new GameObject("Echo shot - preview only");
        trail.transform.SetParent(transform, true);
        if (showTracer)
        {
            var line = trail.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.startWidth = .025f; line.endWidth = .008f;
            line.positionCount = 2; line.useWorldSpace = true;
            line.SetPosition(0, shot.muzzlePosition);
            line.SetPosition(1, end);
        }
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "Echo muzzle flash";
        flash.transform.SetParent(trail.transform, true);
        flash.transform.position = shot.muzzlePosition;
        flash.transform.localScale = Vector3.one * .065f;
        var collider = flash.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        var renderer = flash.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_Color", new Color(1, .85f, .3f, 1));
        renderer.SetPropertyBlock(properties);
        PresentedShotCount++;
        Destroy(trail, .1f);
    }
}
