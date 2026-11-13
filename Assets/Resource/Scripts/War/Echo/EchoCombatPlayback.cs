using System;
using UnityEngine;

public partial class EchoControl
{
    private bool _combatPlayback;
    private Func<string, bool> _combatRunning, _activationAllowed;
    private Action<string, PlayerEchoActor, EchoSegment> _initializeCombatActor;
    private Action<string, PlayerEchoActor, EchoSegment, EchoFireEvent> _combatShot;

    public void StartCombatPlayback(string runId, Action<string, PlayerEchoActor, EchoSegment, EchoFireEvent> onShot,
        Func<string, bool> running, Func<string, bool> activationAllowed,
        Action<string, PlayerEchoActor, EchoSegment> initializeActor)
    {
        SaveSegment(); ClearPreviews(); StopCombatPlayback();
        model.isPreparing = false; model.isRecording = false;
        model.skillRunId = runId;
        model.cooldownRemaining = 0;
        _combatShot = onShot; _combatRunning = running; _activationAllowed = activationAllowed;
        _initializeCombatActor = initializeActor;
        _combatPlayback = true;
    }

    public bool TryActivateSlot(int slotIndex) => TryActivateSlot(slotIndex, model.skillRunId);

    public bool TryActivateSlot(int slotIndex, string runId)
    {
        if (!_combatPlayback || runId != model.skillRunId || _activationAllowed == null || !_activationAllowed(runId) ||
            model.cooldownRemaining > 0) return false;
        var slot = model.slots.Find(s => s.slotIndex == slotIndex);
        var segment = slot == null ? null : model.segments.Find(s => s.id == slot.segmentId);
        if (!ValidCombatSegment(segment)) return false;
        GameObject visual = null;
        EchoPlaybackInstance instance = null;
        try
        {
            visual = MakeShadow(_visualRoot);
            var actor = visual.GetComponent<PlayerEchoActor>();
            instance = new EchoPlaybackInstance { instanceId = Guid.NewGuid().ToString("N"), runId = runId,
                slotIndex = slotIndex, segmentId = segment.id, segment = segment, actor = actor };
            visual.name = "Combat echo " + segment.id + " / " + instance.instanceId;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            PlaceCombatActor(actor, segment.frames, 0);
            _initializeCombatActor?.Invoke(runId, actor, segment);
            if (!_combatPlayback || runId != model.skillRunId || !_activationAllowed(runId))
                throw new InvalidOperationException("Echo activation is no longer valid.");
            model.playbackInstances.Add(instance);
        }
        catch (Exception error)
        {
            if (instance != null) model.playbackInstances.Remove(instance);
            if (visual != null) { visual.SetActive(false); Destroy(visual); }
            Debug.LogWarning("Echo activation failed: " + error.Message);
            return false;
        }
        model.cooldownRemaining = model.cooldownSeconds;
        model.isPlaying = true;
        // Include time zero once; the next tick starts at the next shot cursor.
        PlayInstance(instance, 0);
        return true;
    }

    private static bool ValidCombatSegment(EchoSegment segment)
    {
        if (segment == null || string.IsNullOrEmpty(segment.id) || segment.duration <= 0 ||
            float.IsNaN(segment.duration) || float.IsInfinity(segment.duration) || segment.frames == null || segment.frames.Length == 0) return false;
        float previous = -1;
        foreach (var frame in segment.frames)
        {
            if (float.IsNaN(frame.time) || frame.time < 0 || frame.time > segment.duration || frame.time < previous) return false;
            previous = frame.time;
        }
        previous = -1;
        foreach (var shot in segment.shots ?? Array.Empty<EchoFireEvent>())
        {
            if (float.IsNaN(shot.time) || shot.time < 0 || shot.time > segment.duration || shot.time < previous) return false;
            previous = shot.time;
        }
        return true;
    }

    public void TickCombatPlayback(float dt)
    {
        string run = model.skillRunId;
        if (!_combatPlayback || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt) || _combatRunning == null || !_combatRunning(run)) return;
        model.cooldownRemaining = Mathf.Max(0, model.cooldownRemaining - dt);
        foreach (var instance in model.playbackInstances.ToArray())
        {
            if (!_combatPlayback || model.skillRunId != run || !_combatRunning(run)) return;
            if (instance.runId != run || instance.actor == null) { ReleaseInstance(instance); continue; }
            PlayInstance(instance, dt);
            if (!_combatPlayback || model.skillRunId != run) return;
            if (instance.elapsed >= instance.segment.duration) ReleaseInstance(instance);
        }
        model.isPlaying = model.playbackInstances.Count > 0;
    }

    private void PlayInstance(EchoPlaybackInstance instance, float dt)
    {
        var segment = instance.segment;
        instance.elapsed = Mathf.Min(segment.duration, instance.elapsed + dt);
        PlaceCombatActor(instance.actor, segment.frames, instance.elapsed);
        while (segment.shots != null && instance.shotIndex < segment.shots.Length && segment.shots[instance.shotIndex].time <= instance.elapsed)
        {
            var shot = segment.shots[instance.shotIndex++];
            instance.actor.ShowShot(shot, _preparationMaterial, false);
            _combatShot?.Invoke(instance.runId, instance.actor, segment, shot);
            if (!_combatPlayback || instance.runId != model.skillRunId || _combatRunning == null || !_combatRunning(instance.runId)) return;
        }
    }

    private static void PlaceCombatActor(PlayerEchoActor actor, EchoFrame[] frames, float time)
    {
        int low = 0, high = frames.Length - 1;
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (frames[mid].time <= time) low = mid;
            else high = mid - 1;
        }
        var a = frames[low]; var b = frames[Mathf.Min(low + 1, frames.Length - 1)];
        float blend = b.time > a.time ? Mathf.InverseLerp(a.time, b.time, time) : 0;
        actor.SetPose(Vector3.Lerp(a.position, b.position, blend), Quaternion.Slerp(a.rotation, b.rotation, blend));
    }

    private void ReleaseInstance(EchoPlaybackInstance instance)
    {
        if (instance.actor != null) { instance.actor.gameObject.SetActive(false); Destroy(instance.actor.gameObject); }
        model.playbackInstances.Remove(instance);
    }

    public void StopCombatPlayback()
    {
        _combatPlayback = false;
        foreach (var instance in model.playbackInstances.ToArray()) ReleaseInstance(instance);
        _combatShot = null; _combatRunning = _activationAllowed = null; _initializeCombatActor = null;
        model.skillRunId = null;
        model.isPlaying = false;
    }
}
