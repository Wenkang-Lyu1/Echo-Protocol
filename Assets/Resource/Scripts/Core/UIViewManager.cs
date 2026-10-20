using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Owns new UI instances and the unscaled, cancellable ten-second recycle queue.</summary>
public sealed class UIViewManager : MonoSingleton<UIViewManager>
{
    public const float RecycleDelay = 10f;
    private readonly Dictionary<Type, UIViewBase> _views = new Dictionary<Type, UIViewBase>();
    private readonly Dictionary<UIViewBase, float> _pending = new Dictionary<UIViewBase, float>();
    private readonly List<UIViewBase> _expired = new List<UIViewBase>();

    public T Open<T>(string resourcePath, ModelBase model) where T : UIViewBase
    {
        EnsureEventSystem();
        if (!_views.TryGetValue(typeof(T), out var existing) || existing == null ||
            existing.State == ViewState.Destroyed)
        {
            var prefab = Resources.Load<T>(resourcePath);
            if (prefab == null)
                throw new InvalidOperationException("Missing UI prefab: Resources/" + resourcePath);
            // Inactive parent prevents OnEnable from running before binding/initialization.
            var staging = new GameObject("UI staging");
            staging.SetActive(false);
            var instance = Instantiate(prefab, staging.transform, false);
            instance.name = prefab.name;
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(transform, false);
            Destroy(staging);
            existing = instance;
            _views[typeof(T)] = existing;
        }
        existing.Bind(model);
        existing.Open();
        return (T)existing;
    }

    public void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var input = new GameObject("UI EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        input.transform.SetParent(transform, false);
    }

    public void ScheduleRecycle(UIViewBase view)
    {
        if (view != null && view.State == ViewState.Ready && !_pending.ContainsKey(view))
            _pending.Add(view, Time.realtimeSinceStartup + RecycleDelay);
    }

    public void CancelRecycle(UIViewBase view)
    {
        if (!ReferenceEquals(view, null)) _pending.Remove(view);
    }

    private void Update()
    {
        _expired.Clear();
        foreach (var item in _pending)
            if (item.Key == null || Time.realtimeSinceStartup >= item.Value)
                _expired.Add(item.Key);
        foreach (var view in _expired)
        {
            // Recheck: disposing a parent can cancel a child's entry in this snapshot.
            if (!_pending.Remove(view)) continue;
            if (view != null && view.State == ViewState.Ready) view.Dispose();
        }
    }

    private void OnDestroy()
    {
        foreach (var view in new List<UIViewBase>(_views.Values))
            if (view != null) view.Dispose();
        _pending.Clear();
        _views.Clear();
    }
}
