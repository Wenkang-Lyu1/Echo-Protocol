using System.Collections.Generic;
using UnityEngine;

public partial class EchoControl
{
    private readonly Dictionary<string, GameObject> _previews = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, int> _previewShotIndices = new Dictionary<string, int>();
    public float PreviewEnd => model.isRecording ? model.segmentLimitSeconds : model.SelectedSegment?.duration ?? 0;

    public bool SelectPreparationSlot(int slotIndex)
    {
        if (!model.isPreparing || model.isRecording) return false;
        var slot = model.slots.Find(s => s.slotIndex == slotIndex);
        if (slot == null || string.IsNullOrEmpty(slot.segmentId)) return false;
        ClearPreviews();
        model.selectedSegmentId = slot.segmentId;
        return SeekPreparation(0);
    }

    public bool TogglePreview()
    {
        if (!model.isPreparing || model.isRecording || model.SelectedSegment == null || PreviewEnd <= 0) return false;
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
        var segment = model.SelectedSegment;
        if (segment?.shots != null)
        {
            if (!_previewShotIndices.TryGetValue(segment.id, out int index)) index = 0;
            while (index < segment.shots.Length && segment.shots[index].time <= model.cursor)
                GetPreview(segment).GetComponent<PlayerEchoActor>().ShowShot(segment.shots[index++], _preparationMaterial);
            _previewShotIndices[segment.id] = index;
        }
        if (model.cursor >= PreviewEnd) model.isPreviewPlaying = false;
    }

    private void UpdatePreviewPoses()
    {
        foreach (var visual in _previews.Values) if (visual != null) visual.SetActive(false);
        var segment = model.SelectedSegment;
        if (segment?.frames == null || segment.frames.Length == 0) return;
        var preview = GetPreview(segment);
        preview.SetActive(true);
        PlaceCombatActor(preview.GetComponent<PlayerEchoActor>(), segment.frames, model.cursor);
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
        var segment = model.SelectedSegment;
        if (segment == null) return;
        int index = 0;
        if (segment.shots != null)
            while (index < segment.shots.Length && segment.shots[index].time < model.cursor) index++;
        _previewShotIndices[segment.id] = index;
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
