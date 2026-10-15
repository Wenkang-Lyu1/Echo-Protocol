using System;
using UnityEngine;

// ============================================================
// DataContext -- data-passing mechanism between parent and child Views.
//
// Scenario:
//   MainView
//     ├─ CityView     (needs to display city data)
//     ├─ RoleHeadView (needs to display the avatar)
//     └─ BagView      (needs to display inventory data)
//
// Parent View pushes data downward via SetData.
// Child View reads data via GetData<T> (looks up, then down).
// ============================================================

public interface IDataContext
{
    object GetData(string key);
    T GetData<T>(string key) where T : class;
    void SetData(string key, object value);
    bool HasData(string key);
    void ClearData();
}

public class DataContext : IDataContext
{
    private readonly System.Collections.Generic.Dictionary<string, object> _data =
        new System.Collections.Generic.Dictionary<string, object>();

    public object GetData(string key)
    {
        _data.TryGetValue(key, out var v);
        return v;
    }

    public T GetData<T>(string key) where T : class
    {
        return GetData(key) as T;
    }

    public void SetData(string key, object value)
    {
        if (value == null) _data.Remove(key);
        else _data[key] = value;
    }

    public bool HasData(string key) => _data.ContainsKey(key);

    public void ClearData() => _data.Clear();
}

// ---- ViewBase extension: built-in DataContext ----
public static class ViewDataContextExtensions
{
    private static readonly System.Collections.Generic.Dictionary<IView, DataContext> _contexts =
        new System.Collections.Generic.Dictionary<IView, DataContext>();

    public static DataContext GetDataContext(this IView view)
    {
        if (view == null) return null;
        if (!_contexts.TryGetValue(view, out var ctx))
        {
            ctx = new DataContext();
            _contexts[view] = ctx;
        }
        return ctx;
    }

    public static void SetData<T>(this IView view, string key, T value)
    {
        view.GetDataContext().SetData(key, value);
    }

    public static T GetData<T>(this IView view, string key) where T : class
    {
                // Check self first, then walk up the parent chain.
        var ctx = view.GetDataContext();
        if (ctx.HasData(key)) return ctx.GetData<T>(key);

        if (view.Parent != null)
            return view.Parent.GetData<T>(key);

        return null;
    }

    public static T GetChildData<T>(this IView view, string childName, string key) where T : class
    {
        var child = view.GetChild(childName);
        return child != null ? child.GetData<T>(key) : null;
    }
}
