using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// Echo frame and timeline -- pure data.
// ============================================================
[Serializable]
public struct EchoFrame
{
    public float time;
    public Vector3 position;
    public Quaternion rotation;
    public Vector2 lookDelta;
    public EchoInputFlags flags;

    public bool IsMoving => (flags & EchoInputFlags.Move) != 0;
    public bool IsFiring => (flags & EchoInputFlags.Fire) != 0;
    public bool IsReloading => (flags & EchoInputFlags.Reload) != 0;
    public bool IsCrouching => (flags & EchoInputFlags.Crouch) != 0;
    public bool IsSprinting => (flags & EchoInputFlags.Sprint) != 0;
}

[Flags]
public enum EchoInputFlags : byte
{
    None = 0,
    Move = 1,
    Fire = 2,
    Reload = 4,
    Crouch = 8,
    Sprint = 16,
    Interact = 32
}

[Serializable]
public struct EchoFireEvent
{
    public float time;
    public Vector3 muzzlePosition;
    public Vector3 fireDirection;
}

[Serializable]
public class EchoModel : ModelBase
{
    public bool isPreparing;
    public float cursor;
    public float levelDuration = 240f;
    public int echoLimit = int.MaxValue;
    public int RemainingEchoes => Mathf.Max(0, echoLimit - segments.Count - (isRecording ? 1 : 0));
    public bool CanCreateEcho => isPreparing && !isRecording && RemainingEchoes > 0 && cursor < levelDuration;
    public float selectedSpeed = 1f;
    public bool isPreviewPlaying;
    public float EffectiveSpeed => isPreparing && (isRecording || isPreviewPlaying) ? selectedSpeed : 0f;
    public readonly List<EchoSegment> segments = new List<EchoSegment>();
    public bool isRecording;
    public bool isPlaying;
    public float recordDuration;
    public float playbackProgress;
    public List<EchoFrame> frames = new List<EchoFrame>();
    public List<EchoFireEvent> fireEvents = new List<EchoFireEvent>();
    public List<EchoPlayback> activeEchoes = new List<EchoPlayback>();
    public int maxConcurrentEchoes = 3;

    public int FrameCount => frames.Count;
    public float Duration => recordDuration;
    public int ActiveEchoCount => activeEchoes.Count;

    public override void Reset()
    {
        isPreparing = false;
        isPreviewPlaying = false;
        cursor = 0;
        selectedSpeed = 1;
        echoLimit = int.MaxValue;
        segments.Clear();
        isRecording = false;
        isPlaying = false;
        recordDuration = 0f;
        playbackProgress = 0f;
        frames.Clear();
        fireEvents.Clear();
        activeEchoes.Clear();
    }
}

[Serializable]
public sealed class EchoSegment
{
    public string id;
    public float startTime;
    public float duration;
    public EchoFrame[] frames;
    public EchoFireEvent[] shots = new EchoFireEvent[0];
}
