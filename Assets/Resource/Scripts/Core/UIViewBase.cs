using UnityEngine;

/// <summary>UI lifecycle adapter. Existing gameplay ViewBase subclasses remain unchanged.</summary>
public abstract class UIViewBase : ViewBase
{
    private bool _initialized;
    private bool _widgetsBound;
    private bool _firstOpened;
    private bool _disposing;

    public void Initialized()
    {
        if (_initialized || State == ViewState.Destroyed) return;
        _Initialized();
        _initialized = true;
    }

    public void UIWidget()
    {
        if (_widgetsBound || State == ViewState.Destroyed) return;
        Initialized();
        _UIWidget();
        _widgetsBound = true;
    }

    public void FirstOpen()
    {
        if (_firstOpened || State == ViewState.Destroyed) return;
        Initialize();
        _FirstOpen();
        _firstOpened = true;
    }

    public void Open() => Show();
    public void Close() => Hide();
    public void Dispose() => Shutdown();

    public sealed override void Initialize()
    {
        if (State == ViewState.Destroyed) return;
        Initialized();
        UIWidget();
        base.Initialize();
    }

    public sealed override void Show()
    {
        if (_disposing || State == ViewState.Destroyed) return;
        UIViewManager.Instance?.CancelRecycle(this);
        if (State == ViewState.Showing) { Refresh(); return; }
        Initialize();
        FirstOpen();
        gameObject.SetActive(true);
        _Open();
        base.Show();
    }

    public sealed override void Hide()
    {
        if (_disposing || State != ViewState.Showing) return;
        try { _Close(); }
        finally
        {
            base.Hide();
            UIViewManager.Instance?.ScheduleRecycle(this);
        }
    }

    public sealed override void Shutdown()
    {
        if (_disposing || State == ViewState.Destroyed) return;
        if (State == ViewState.Showing) Hide();
        _disposing = true;
        UIViewManager.Instance?.CancelRecycle(this);
        try { _Dispose(); }
        finally { base.Shutdown(); }
    }

    protected abstract void _Initialized();
    protected abstract void _UIWidget();
    protected abstract void _FirstOpen();
    protected abstract void _Open();
    protected abstract void _Close();
    protected abstract void _Dispose();
}
