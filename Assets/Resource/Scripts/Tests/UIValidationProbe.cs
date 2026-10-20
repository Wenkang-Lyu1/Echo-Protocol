#if UNITY_EDITOR
// Runtime assembly placement permits AddComponent during editor Play Mode.
public sealed class UIValidationProbe : UIViewBase
{
    public int initialized, widgets, first, opened, closed, disposed;
    protected override void _Initialized() { initialized++; }
    protected override void _UIWidget() { widgets++; }
    protected override void _FirstOpen() { first++; }
    protected override void _Open() { opened++; }
    protected override void _Close() { closed++; }
    protected override void _Dispose() { disposed++; }
    protected override void OnRefresh() { }
}
#endif
