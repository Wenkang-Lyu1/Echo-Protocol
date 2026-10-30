using UnityEngine;

public enum ProjectileSource { Player, Echo, Enemy, Boss }

public readonly struct ProjectileOwner
{
    public readonly string runId;
    public readonly int instanceId;
    public readonly ProjectileSource kind;
    public readonly string segmentId;
    public readonly GameObject source;
    public ProjectileOwner(string run, GameObject actor, ProjectileSource type, string segment = null)
    {
        runId = run; source = actor; instanceId = actor != null ? actor.GetInstanceID() : 0;
        kind = type; segmentId = segment;
    }
    public bool IsPlayerSide => kind == ProjectileSource.Player || kind == ProjectileSource.Echo;
}

public sealed class ProjectileModel
{
    public ProjectileOwner owner;
    public GameObject hitTarget;
    public Vector3 direction;
    public Vector3 hitPoint;
    public float damage, speed, remainingRange;
    public bool finished;
    public bool hitReported;
}

public readonly struct ProjectileHitArgs
{
    public readonly ProjectileOwner owner;
    public readonly GameObject target;
    public readonly int targetId;
    public readonly Vector3 point;
    public ProjectileHitArgs(ProjectileOwner source, GameObject hit, Vector3 position)
    { owner = source; target = hit; targetId = hit != null ? hit.GetInstanceID() : 0; point = position; }
}
