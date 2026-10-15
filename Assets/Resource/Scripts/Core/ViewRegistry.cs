using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ViewRegistry -- global registry for all Views.
//
// Responsibilities:
//   1) Centrally manage all IView instances.
//   2) Lookup Views by name / type.
//   3) Maintain the View hierarchy.
//   4) Drive Refresh in a unified way.
// ============================================================
public class ViewRegistry
{
    private static readonly Dictionary<string, IView> _byName =
        new Dictionary<string, IView>();
    private static readonly Dictionary<Type, List<IView>> _byType =
        new Dictionary<Type, List<IView>>();
    private static readonly List<IView> _roots = new List<IView>();

    public static IEnumerable<IView> Roots => _roots;
    public static int Count => _byName.Count;

    public static void Register(IView view)
    {
        if (view == null) return;
        if (!_byName.ContainsKey(view.ViewName))
            _byName[view.ViewName] = view;

        var type = view.GetType();
        if (!_byType.ContainsKey(type))
            _byType[type] = new List<IView>();
        if (!_byType[type].Contains(view))
            _byType[type].Add(view);

        if (view.Parent == null && !_roots.Contains(view))
            _roots.Add(view);
    }

    public static void Unregister(IView view)
    {
        if (view == null) return;
        _byName.Remove(view.ViewName);
        var type = view.GetType();
        if (_byType.TryGetValue(type, out var list))
            list.Remove(view);
        _roots.Remove(view);
    }

    public static IView Get(string name)
    {
        _byName.TryGetValue(name, out var v);
        return v;
    }

    public static T Get<T>() where T : class, IView
    {
        if (_byType.TryGetValue(typeof(T), out var list) && list.Count > 0)
            return list[0] as T;
        return null;
    }

    public static IEnumerable<T> GetAll<T>() where T : class, IView
    {
        if (_byType.TryGetValue(typeof(T), out var list))
            foreach (var v in list) yield return v as T;
    }

    public static void RefreshAll()
    {
        foreach (var v in _byName.Values)
            v.Refresh();
    }

    public static void DestroyAll()
    {
        var snapshot = _roots.ToArray();
        foreach (var v in snapshot)
            v.Destroy();
        _byName.Clear();
        _byType.Clear();
        _roots.Clear();
    }
}
