using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-2000)]
// Cross-module requests and notifications. Nested dispatch runs on later frames.
public class EventCenter : MonoSingleton<EventCenter>
{
    private sealed class Listeners
    {
        public Action<GameEventArgs> Callbacks;
        public Delegate[] Snapshot;
    }

    private readonly struct PendingEvent
    {
        public readonly GameEventArgs Args;
        public readonly int Frame;
        public PendingEvent(GameEventArgs args) { Args = args; Frame = Time.frameCount + 1; }
    }

    private readonly Dictionary<GameEvent, Listeners> _listeners = new Dictionary<GameEvent, Listeners>();
    private readonly Queue<PendingEvent> _deferred = new Queue<PendingEvent>();
    private bool _isDispatching;

    public bool HasListener(GameEvent evt)
    {
        return _listeners.ContainsKey(evt);
    }

    public void AddListener(GameEvent evt, Action<GameEventArgs> cb)
    {
        if (evt == GameEvent.None || cb == null) return;
        if (!_listeners.TryGetValue(evt, out var listeners))
            _listeners.Add(evt, listeners = new Listeners());
        listeners.Callbacks += cb;
        // Allocate a stable invocation snapshot only when subscriptions change.
        listeners.Snapshot = listeners.Callbacks.GetInvocationList();
    }

    public void RemoveListener(GameEvent evt, Action<GameEventArgs> cb)
    {
        if (cb == null || !_listeners.TryGetValue(evt, out var listeners)) return;
        listeners.Callbacks -= cb;
        if (listeners.Callbacks == null) _listeners.Remove(evt);
        else listeners.Snapshot = listeners.Callbacks.GetInvocationList();
    }

    public void Dispatch(GameEvent evt, object data = null) =>
        Dispatch(new GameEventArgs { eventType = evt, data = data });

    public void Dispatch(GameEventArgs args)
    {
        if (args == null || args.eventType == GameEvent.None) return;

        if (_isDispatching)
        {
            _deferred.Enqueue(new PendingEvent(args));
            return;
        }

        if (!_listeners.TryGetValue(args.eventType, out var listeners))
            return;

        var snapshot = listeners.Snapshot;
        _isDispatching = true;
        try
        {
            foreach (Action<GameEventArgs> callback in snapshot)
            {
                try { callback(args); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }
        finally
        {
            _isDispatching = false;
        }
    }

    private void Update()
    {
        int count = _deferred.Count;
        for (int i = 0; i < count && _deferred.Count > 0; i++)
        {
            if (_deferred.Peek().Frame > Time.frameCount) break;
            Dispatch(_deferred.Dequeue().Args);
        }
    }

    private void OnDestroy()
    {
        _listeners.Clear();
        _deferred.Clear();
    }
}
