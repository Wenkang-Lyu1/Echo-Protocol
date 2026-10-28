using UnityEngine;
using UnityEngine.UI;

public sealed class U_Level_SegmentSV : UIViewBase
{
    [SerializeField] private Text label;
    [SerializeField] private Button deleteButton;
    private EchoControl _owner;
    private string _id;
    public void Present(EchoControl owner, EchoSegment segment, bool canDelete)
    {
        _owner = owner; _id = segment.id;
        label.text = $"{segment.startTime:F2}s → {segment.startTime + segment.duration:F2}s   ({segment.duration:F2}s)";
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
