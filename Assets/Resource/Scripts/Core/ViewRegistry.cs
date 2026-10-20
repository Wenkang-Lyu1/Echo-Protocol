using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================
// ViewRegistry -- optional lookup for Views.
//
// A View registers itself in Initialize() and unregisters in Shutdown(),
// so nothing here needs to be called by hand. Use it when one system has
// to find a View it does not own:
//
//     ViewRegistry.Get<WarView>()      // first view of that type
//     ViewRegistry.Get("HudView")      // by GameObject name
//
// Views with the same GameObject name keep the first one registered.
// ============================================================
public static class ViewRegistry
{
    private static readonly Dictionary<string, IView> _byName = new Dictionary<string, IView>();
    private static readonly Dictionary<Type, List<IView>> _byType = new Dictionary<Type, List<IView>>();

    public static int Count => _byName.Count;

    public static void Register(IView view)
    {
        if (view == null) return;

        if (!_byName.ContainsKey(view.ViewName))
            _byName[view.ViewName] = view;

        var type = view.GetType();
        if (!_byType.TryGetValue(type, out var list))
        {
            list = new List<IView>();
            _byType[type] = list;
        }
        if (!list.Contains(view))
            list.Add(view);
    }

    public static void Unregister(IView view)
    {
        if (view == null) return;

        if (_byName.TryGetValue(view.ViewName, out var named) && named == view)
            _byName.Remove(view.ViewName);

        if (_byType.TryGetValue(view.GetType(), out var list))
            list.Remove(view);
    }

    public static IView Get(string name)
    {
        _byName.TryGetValue(name, out var view);
        return view;
    }

    public static T Get<T>() where T : class, IView
    {
        return _byType.TryGetValue(typeof(T), out var list) && list.Count > 0 ? list[0] as T : null;
    }

    public static IEnumerable<T> GetAll<T>() where T : class, IView
    {
        if (!_byType.TryGetValue(typeof(T), out var list)) yield break;
        foreach (var view in list) yield return view as T;
    }

    // Shuts down every registered root; children follow their parent.
    public static void ShutdownAll()
    {
        foreach (var view in _byType.Values.ToArray())
            foreach (var root in view.ToArray())
                if (root.Parent == null)
                    root.Shutdown();

        _byName.Clear();
        _byType.Clear();
    }
}
