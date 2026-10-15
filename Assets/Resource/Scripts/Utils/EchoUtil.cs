using System.Collections.Generic;
using UnityEngine;

// ============================================================
// EchoUtil -- calculation utility for the Echo system.
// Pure static, stateless.
// ============================================================
public static class EchoUtil
{
        // Sample one frame at 20 Hz.
    public static EchoFrame SampleFrame(PlayerModel player, Vector2 lookDelta, EchoInputFlags flags)
    {
        return new EchoFrame
        {
            time = Time.time,
            position = player.position,
            rotation = player.rotation,
            lookDelta = lookDelta,
            flags = flags
        };
    }

        // Linear-time interpolation sampling.
    public static bool SampleFrame(List<EchoFrame> frames, float t, out EchoFrame result)
    {
        result = default;
        if (frames == null || frames.Count == 0) return false;

        if (frames.Count == 1 || t <= 0f)
        {
            result = frames[0];
            return true;
        }
        if (t >= frames[frames.Count - 1].time)
        {
            result = frames[frames.Count - 1];
            return true;
        }

        var i = BinarySearch(frames, t);
        var a = frames[i];
        var b = frames[i + 1];
        var span = b.time - a.time;
        var k = span > Mathf.Epsilon ? Mathf.Clamp01((t - a.time) / span) : 0f;

        result = new EchoFrame
        {
            time = t,
            position = Vector3.Lerp(a.position, b.position, k),
            rotation = Quaternion.Slerp(a.rotation, b.rotation, k),
            lookDelta = Vector2.Lerp(a.lookDelta, b.lookDelta, k),
            flags = k < 0.5f ? a.flags : b.flags
        };
        return true;
    }

        // Deployment validity check.
    public static bool ValidateDeployPoint(Vector3 origin, Vector3 direction, float maxRange,
        float minRange, LayerMask groundMask, out Vector3 point)
    {
        point = Vector3.zero;
        var dir = direction.normalized;
        var start = origin + dir * minRange;

        if (Physics.Raycast(start, dir, out var hit, maxRange - minRange, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (Vector3.Angle(hit.normal, Vector3.up) > 45f)
                return false;
            point = hit.point + Vector3.up * 0.05f;
            return true;
        }
        return false;
    }

    private static int BinarySearch(List<EchoFrame> frames, float t)
    {
        var lo = 0;
        var hi = frames.Count - 2;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (frames[mid + 1].time < t) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}
