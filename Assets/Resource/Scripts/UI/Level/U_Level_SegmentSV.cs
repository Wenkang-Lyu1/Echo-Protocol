using UnityEngine;
using UnityEngine.UI;

public sealed class U_Level_SegmentSV : UIViewBase
{
    [SerializeField] private Text label;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button selectButton;
    [SerializeField] private RectTransform segmentBar;
    private EchoControl _owner;
    private string _id;
    private int _slotIndex;
    public void Present(EchoControl owner, EchoSlotModel slot, EchoSegment segment, bool canDelete, float span)
    {
        _owner = owner; _id = segment?.id; _slotIndex = slot.slotIndex;
        bool recording = owner.model.recordingSlotIndex == slot.slotIndex;
        label.text = $"Echo {slot.slotIndex} · " + (recording ? "Recording" : segment == null ? "Empty" : $"{segment.duration:0.00}s") +
            (segment != null && owner.model.selectedSegmentId == segment.id ? " <" : "");
        float duration = recording ? owner.model.recordDuration : segment?.duration ?? 0;
        bool selected = segment != null && owner.model.selectedSegmentId == segment.id;
        segmentBar.gameObject.SetActive(duration > 0 && (recording || selected));
        segmentBar.anchorMin = new Vector2(0, .12f);
        segmentBar.anchorMax = new Vector2(recording ? duration / span : 1, .88f);
        segmentBar.offsetMin = Vector2.zero;
        segmentBar.offsetMax = Vector2.zero;
        deleteButton.interactable = canDelete && segment != null;
        if (selectButton != null) selectButton.interactable = canDelete && segment != null;
    }
    private void Delete() => _owner.DeleteSegment(_id);
    private void Select() { _owner.SelectPreparationSlot(_slotIndex); }
    protected override void _Initialized() { }
    protected override void _FirstOpen() { }
    protected override void _Open() { }
    protected override void _Close() { }
    protected override void OnRefresh() { }
    protected override void _UIWidget()
    {
        deleteButton.onClick.AddListener(Delete);
        if (selectButton != null) selectButton.onClick.AddListener(Select);
    }
    protected override void _Dispose()
    {
        deleteButton.onClick.RemoveListener(Delete);
        if (selectButton != null) selectButton.onClick.RemoveListener(Select);
    }
}
