using System.Collections.Generic;
using UnityEngine;

public partial class EchoControl
{
    private readonly Dictionary<string, GameObject> _previews = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, int> _previewShotIndices = new Dictionary<string, int>();
    public float PreviewEnd
    {
        get => model.levelDuration;
    }

    public bool TogglePreview()
    {
        if (!model.isPreparing || model.isRecording || model.segments.Count == 0) return false;
        model.isPreviewPlaying = !model.isPreviewPlaying;
        if (model.isPreviewPlaying && model.cursor >= PreviewEnd)
        {
            model.cursor = 0;
            ResetPreviewShotCursors();
        }
        if (model.isPreviewPlaying && _previewShotIndices.Count == 0) ResetPreviewShotCursors();
        UpdatePreviewPoses();
        return true;
    }

    private void TickPreview(float dt)
    {
        model.cursor = Mathf.Min(PreviewEnd, model.cursor + dt * model.selectedSpeed);
        UpdatePreviewPoses();
        foreach (var segment in model.segments)
        {
            if (segment.shots == null) continue;
            if (!_previewShotIndices.TryGetValue(segment.id, out int index)) index = 0;
            while (index < segment.shots.Length && segment.startTime + segment.shots[index].time <= model.cursor)
            {
                var visual = GetPreview(segment);
                visual.SetActive(model.cursor <= segment.startTime + segment.duration);
                visual.GetComponent<PlayerEchoActor>().ShowShot(segment.shots[index], _preparationMaterial);
                index++;
            }
            _previewShotIndices[segment.id] = index;
        }
        if (model.cursor >= PreviewEnd) model.isPreviewPlaying = false;
    }

    private void UpdatePreviewPoses()
    {
        foreach (var segment in model.segments)
        {
            float localTime = model.cursor - segment.startTime;
            bool visible = localTime >= 0 && localTime <= segment.duration && segment.frames != null && segment.frames.Length > 0;
            _previews.TryGetValue(segment.id, out var visual);
            if (visual == null && visible) visual = GetPreview(segment);
            if (visual == null) continue;
            visual.SetActive(visible);
            if (!visible) continue;
            var frames = segment.frames;
            int low = 0, high = frames.Length - 1;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (frames[mid].time <= localTime) low = mid;
                else high = mid - 1;
            }
            var a = frames[low];
            var b = frames[Mathf.Min(low + 1, frames.Length - 1)];
            float blend = b.time > a.time ? Mathf.InverseLerp(a.time, b.time, localTime) : 0;
            visual.GetComponent<PlayerEchoActor>().SetPose(Vector3.Lerp(a.position, b.position, blend), Quaternion.Slerp(a.rotation, b.rotation, blend));
        }
    }

    private GameObject GetPreview(EchoSegment segment)
    {
        if (_previews.TryGetValue(segment.id, out var visual)) return visual;
        visual = MakeShadow(_visualRoot);
        visual.name = "Echo preview " + segment.id;
        _previews.Add(segment.id, visual);
        return visual;
    }

    private void ResetPreviewShotCursors()
    {
        _previewShotIndices.Clear();
        foreach (var segment in model.segments)
        {
            int index = 0;
            if (segment.shots != null)
                while (index < segment.shots.Length && segment.startTime + segment.shots[index].time < model.cursor) index++;
            _previewShotIndices[segment.id] = index;
        }
    }

    private void ClearPreviews()
    {
        foreach (var visual in _previews.Values)
            if (visual != null) { visual.SetActive(false); Destroy(visual); }
        _previews.Clear();
        _previewShotIndices.Clear();
        model.isPreviewPlaying = false;
    }
}
