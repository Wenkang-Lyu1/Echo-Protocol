#if UNITY_EDITOR
// Runtime assembly placement permits AddComponent during editor Play Mode.
public sealed class UIValidationProbe : UIViewBase
{
    public int initialized, widgets, first, opened, closed, disposed, received;
    public bool disposeOnClose;
    protected override void _Initialized() { initialized++; }
    protected override void _UIWidget() { widgets++; }
    protected override void _FirstOpen() { first++; AddEventListener(); }
    private void AddEventListener() { Events.Listen(GameEvent.UI_Refresh, OnRefreshEvent); }
    private void OnRefreshEvent(GameEventArgs args) { received++; }
    protected override void _Open() { opened++; }
    protected override void _Close()
    {
        closed++;
        if (disposeOnClose) Dispose();
        EventCenter.Instance.Dispatch(GameEvent.UI_Refresh);
    }
    protected override void _Dispose() { disposed++; }
    protected override void OnRefresh() { }
}
#endif
