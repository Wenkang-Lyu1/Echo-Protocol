using System;
using System.Collections.Generic;
using UnityEngine;

public partial class EchoControl
{
    private sealed class CombatTrack
    {
        public EchoSegment segment;
        public PlayerEchoActor actor;
        public int shotIndex;
        public bool finished;
    }
    private readonly List<CombatTrack> _combatTracks = new List<CombatTrack>();
    private bool _combatPlayback;
    private string _combatRunId;
    private Action<string, PlayerEchoActor, EchoSegment, EchoFireEvent> _combatShot;

    public void StartCombatPlayback(string runId, Action<string, PlayerEchoActor, EchoSegment, EchoFireEvent> onShot)
    {
        SaveSegment(); ClearPreviews(); StopCombatPlayback();
        model.isPreparing = false; model.isRecording = false;
        _combatRunId = runId; _combatShot = onShot; _combatPlayback = true;
        foreach (var segment in model.segments) _combatTracks.Add(new CombatTrack { segment = segment });
        model.isPlaying = _combatTracks.Count > 0;
        TickCombatPlayback(0);
    }

    public void TickCombatPlayback(float absoluteTime)
    {
        if (!_combatPlayback) return;
        bool playing = false;
        foreach (var track in _combatTracks)
        {
            if (track.finished || absoluteTime < track.segment.startTime) { playing |= !track.finished; continue; }
            var segment = track.segment;
            if (track.actor == null)
            {
                var visual = MakeShadow(_visualRoot);
                visual.name = "Combat echo " + segment.id;
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                track.actor = visual.GetComponent<PlayerEchoActor>();
            }
            float localTime = Mathf.Clamp(absoluteTime - segment.startTime, 0, segment.duration);
            PlaceCombatActor(track.actor, segment.frames, localTime);
            while (segment.shots != null && track.shotIndex < segment.shots.Length && segment.shots[track.shotIndex].time <= localTime)
            {
                var shot = segment.shots[track.shotIndex++];
                track.actor.ShowShot(shot, _preparationMaterial, false);
                _combatShot?.Invoke(_combatRunId, track.actor, segment, shot);
                if (!_combatPlayback) return;
            }
            if (absoluteTime >= segment.startTime + segment.duration)
            {
                track.finished = true;
                track.actor.gameObject.SetActive(false); Destroy(track.actor.gameObject); track.actor = null;
            }
            else playing = true;
        }
        model.isPlaying = playing;
    }

    private static void PlaceCombatActor(PlayerEchoActor actor, EchoFrame[] frames, float time)
    {
        if (frames == null || frames.Length == 0) return;
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

    public void StopCombatPlayback()
    {
        foreach (var track in _combatTracks)
            if (track.actor != null) { track.actor.gameObject.SetActive(false); Destroy(track.actor.gameObject); }
        _combatTracks.Clear(); _combatShot = null; _combatRunId = null; _combatPlayback = false;
        model.isPlaying = false;
    }
}
