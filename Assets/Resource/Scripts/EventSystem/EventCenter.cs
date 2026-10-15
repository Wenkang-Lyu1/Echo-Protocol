using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-2000)]
public class EventCenter : MonoSingleton<EventCenter>
{
    private readonly Dictionary<GameEvent, Action<GameEventArgs>> _listeners =
        new Dictionary<GameEvent, Action<GameEventArgs>>();

    private readonly List<GameEventArgs> _deferred = new List<GameEventArgs>();
    private bool _isDispatching;

    public bool HasListener(GameEvent evt)
    {
        return _listeners.TryGetValue(evt, out var cb) && cb != null;
    }

    public void AddListener(GameEvent evt, Action<GameEventArgs> cb)
    {
        if (!_listeners.ContainsKey(evt))
            _listeners[evt] = null;
        _listeners[evt] += cb;
    }

    public void RemoveListener(GameEvent evt, Action<GameEventArgs> cb)
    {
        if (!_listeners.ContainsKey(evt)) return;
        _listeners[evt] -= cb;
    }

    public void Dispatch(GameEventArgs args)
    {
        if (args == null || args.eventType == GameEvent.None) return;

        if (_isDispatching)
        {
            _deferred.Add(args);
            return;
        }

        if (!_listeners.TryGetValue(args.eventType, out var cb) || cb == null)
            return;

        _isDispatching = true;
        try
        {
            cb.Invoke(args);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            _isDispatching = false;
        }

        if (_deferred.Count > 0)
            StartCoroutine(FlushDeferred());
    }

    private IEnumerator FlushDeferred()
    {
        yield return null;
        while (_deferred.Count > 0)
        {
            var snapshot = _deferred.ToArray();
            _deferred.Clear();
            foreach (var a in snapshot)
                Dispatch(a);
        }
    }
}
