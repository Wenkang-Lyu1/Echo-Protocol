using System;
using System.Collections.Generic;

/// <summary>Owns a small set of subscriptions. No Unity component or per-frame polling.</summary>
public sealed class EventScope : IDisposable
{
    private sealed class Subscription
    {
        public GameEvent Event;
        public Action<GameEventArgs> Callback;
        public Action<GameEventArgs> Handler;
        public bool Active = true;
    }

    private readonly EventCenter _center;
    private readonly Func<bool> _canInvoke;
    private readonly List<Subscription> _subscriptions = new List<Subscription>();
    private bool _disposed;
    public int Count => _subscriptions.Count;

    public EventScope(EventCenter center, Func<bool> canInvoke = null)
    {
        if (center == null) throw new ArgumentNullException(nameof(center));
        _center = center;
        _canInvoke = canInvoke;
    }

    public void Listen(GameEvent evt, Action<GameEventArgs> callback)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EventScope));
        if (evt == GameEvent.None || callback == null) return;
        if (_center == null) throw new InvalidOperationException("The event center has been destroyed.");
        foreach (var existing in _subscriptions)
            if (existing.Event == evt && existing.Callback == callback) return;

        var subscription = new Subscription { Event = evt, Callback = callback };
        subscription.Handler = args =>
        {
            if (!_disposed && subscription.Active && (_canInvoke == null || _canInvoke()))
                subscription.Callback(args);
        };
        _subscriptions.Add(subscription);
        _center.AddListener(evt, subscription.Handler);
    }

    public void Clear()
    {
        // Invalidate every wrapper before unregistering: an in-flight snapshot
        // may still contain these handlers after removal from the event center.
        foreach (var subscription in _subscriptions) subscription.Active = false;
        if (_center != null)
            foreach (var subscription in _subscriptions)
                _center.RemoveListener(subscription.Event, subscription.Handler);
        _subscriptions.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}
