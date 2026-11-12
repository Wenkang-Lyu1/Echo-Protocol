using System.Collections.Generic;
// ============================================================
// IView -- what every View provides.
//
// A View is presentation only: it shows data, it never changes it.
// Views form a tree, and the tree is the only structure to learn:
//
//     PlayerHud (parent)
//      +-- AmmoView   (child)
//      +-- HpView     (child)
//
// Rules:
//   1) A child is attached with child.SetParent(parent); that is the only
//      way to change the tree.
//   2) Hiding a parent hides every child; shutting a parent down shuts
//      down every child.
//   3) Parent and child reach each other with GetChild<T>() / GetData<T>().
// ============================================================
public enum ViewState
{
    Created,   // built, not initialized yet
    Ready,     // initialized, hidden
    Showing,   // visible on screen
    Destroyed
}

public interface IView
{
    string ViewName { get; }
    ViewState State { get; }

    IView Parent { get; }
    IEnumerable<IView> Children { get; }
    void SetParent(IView parent);

    void Initialize();
    void Show();
    void Hide();
    void Refresh();
    void Shutdown();
}

public static class ViewExtensions
{
    // Depth-first search for the first child of the given type.
    public static T GetChild<T>(this IView view) where T : class, IView
    {
        if (view == null) return null;
        foreach (var child in view.Children)
        {
            if (child is T match) return match;
            var nested = child.GetChild<T>();
            if (nested != null) return nested;
        }
        return null;
    }

    // Depth-first search for the first child with the given GameObject name.
    public static IView GetChild(this IView view, string name)
    {
        if (view == null) return null;
        foreach (var child in view.Children)
        {
            if (child.ViewName == name) return child;
            var nested = child.GetChild(name);
            if (nested != null) return nested;
        }
        return null;
    }
}
