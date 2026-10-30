using UnityEngine;
using UnityEngine.UI;

public sealed class U_Level_SegmentSV : UIViewBase
{
    [SerializeField] private Text label;
    [SerializeField] private Button deleteButton;
    [SerializeField] private RectTransform segmentBar;
    private EchoControl _owner;
    private string _id;
    public void Present(EchoControl owner, EchoSegment segment, bool canDelete, float span, int index)
    {
        _owner = owner; _id = segment.id;
        label.text = $"Echo {index} · {segment.startTime:0.00}–{segment.startTime + segment.duration:0.00}s";
        segmentBar.anchorMin = new Vector2(segment.startTime / span, .12f);
        segmentBar.anchorMax = new Vector2((segment.startTime + segment.duration) / span, .88f);
        segmentBar.offsetMin = Vector2.zero;
        segmentBar.offsetMax = new Vector2(Mathf.Max(0, 4 - segment.duration / span * ((RectTransform)segmentBar.parent).rect.width), 0);
        deleteButton.interactable = canDelete;
    }
    private void Delete() => _owner.DeleteSegment(_id);
    protected override void _Initialized() { }
    protected override void _FirstOpen() { }
    protected override void _Open() { }
    protected override void _Close() { }
    protected override void OnRefresh() { }
    protected override void _UIWidget() => deleteButton.onClick.AddListener(Delete);
    protected override void _Dispose() => deleteButton.onClick.RemoveListener(Delete);
}
