using UnityEngine;

/// <summary>UI lifecycle adapter. Existing gameplay ViewBase subclasses remain unchanged.</summary>
public abstract class UIViewBase : ViewBase
{
    private bool _initialized;
    private bool _widgetsBound;
    private bool _firstOpened;
    private bool _disposing;
    private bool _closing;
    private EventScope _events;
    protected EventScope Events
    {
        get
        {
            if (_disposing || State == ViewState.Destroyed)
                throw new System.ObjectDisposedException(GetType().Name);
            return _events ?? (_events = new EventScope(EventCenter.Instance, CanReceiveEvents));
        }
    }

    private bool CanReceiveEvents() => this != null && !_closing && !_disposing &&
        State == ViewState.Showing && gameObject.activeInHierarchy;

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
        if (_disposing || _closing || State != ViewState.Showing) return;
        _closing = true;
        try { _Close(); }
        finally
        {
            try
            {
                base.Hide();
                UIViewManager.Instance?.ScheduleRecycle(this);
            }
            finally { _closing = false; }
        }
    }

    public sealed override void Shutdown()
    {
        if (_disposing || State == ViewState.Destroyed) return;
        // Mark disposal before user code: _Close may itself request Dispose.
        _disposing = true;
        try
        {
            if (State == ViewState.Showing)
            {
                bool closeAlreadyRunning = _closing;
                _closing = true;
                try { if (!closeAlreadyRunning) _Close(); }
                finally
                {
                    try { base.Hide(); }
                    finally { _closing = false; }
                }
            }
        }
        finally
        {
            _events?.Dispose();
            UIViewManager.Instance?.CancelRecycle(this);
            try { _Dispose(); }
            finally { base.Shutdown(); }
        }
    }

    protected abstract void _Initialized();
    protected abstract void _UIWidget();
    protected abstract void _FirstOpen();
    protected abstract void _Open();
    protected abstract void _Close();
    protected abstract void _Dispose();
}
