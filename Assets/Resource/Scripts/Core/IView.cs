using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// IView -- lifecycle interface for Views with a parent-child hierarchy.
// Rules:
//   1) Views form a tree structure.
//   2) Hiding the parent recursively hides all child Views.
//   3) Parent and child access each other via GetChild / GetChild<T>.
//   4) View is presentation-only and must never modify Model.
// ============================================================

public enum ViewState
{
    None,
    Created,
    Initializing,
    Ready,
    Showing,
    Hiding,
    Destroyed
}

public interface IView
{
    string ViewName { get; }
    ViewState State { get; }
    IView Parent { get; }
    IEnumerable<IView> Children { get; }

    void SetParent(IView parent);
    void AddChild(IView child);
    void RemoveChild(IView child);

    void Initialize();
    void Show();
    void Hide();
    void Refresh();
    void Destroy();

    // Forces every View to implement its own data-refresh logic.
    // Concrete Views override OnRefresh(); Refresh() calls it internally.
    void RefreshData();
}

public static class ViewExtensions
{
    public static T GetChild<T>(this IView view) where T : class, IView
    {
        if (view == null) return null;
        foreach (var c in view.Children)
        {
            if (c is T t) return t;
            var found = c.GetChild<T>();
            if (found != null) return found;
        }
        return null;
    }

    public static IView GetChild(this IView view, string name)
    {
        if (view == null) return null;
        foreach (var c in view.Children)
        {
            if (c.ViewName == name) return c;
            var found = c.GetChild(name);
            if (found != null) return found;
        }
        return null;
    }
}
