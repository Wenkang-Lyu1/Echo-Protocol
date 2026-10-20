using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ViewBase -- base class for every View.
//
// A View shows data; it never changes it. The data comes from the Control
// that owns this View, either wired in the Inspector or found automatically
// by name (PlayerView -> PlayerControl).
//
// The tree:
//   1) A child is attached with child.SetParent(parent).
//   2) Hiding a parent hides every child; shutting a parent down shuts
//      down every child and destroys their GameObjects.
//   3) Parent and child reach each other with GetChild<T>() / GetData<T>().
//
// Lifecycle, in order: Initialize -> Show -> Refresh -> Hide -> Shutdown.
//   Initialize : bind the Model, register in ViewRegistry.
//   Show/Hide  : toggle the GameObject, recurse into children.
//   Refresh    : push Model values into widgets (subclass writes OnRefresh).
//   Shutdown   : unsubscribe, unregister, destroy the GameObject.
// ============================================================
public abstract class ViewBase : MonoBehaviour, IView
{
    // Owned by the Control (or by a serialized override on a prefab).
    [SerializeField] protected ModelBase _model;

    private readonly List<IView> _children = new List<IView>();
    private IView _parent;

    // ---- IView ----
    public string ViewName => gameObject.name;
    public ViewState State { get; private set; } = ViewState.Created;
    public IView Parent => _parent;
    public IEnumerable<IView> Children => _children;

    // Data shared with this View's children (see DataContext).
    public DataContext Data { get; } = new DataContext();

    public void SetParent(IView parent)
    {
        if (ReferenceEquals(_parent, parent)) return;

        if (_parent is ViewBase oldParent)
            oldParent.RemoveChildInternal(this);

        _parent = parent;

        // Keep the Unity hierarchy in sync with the view tree.
        var parentTransform = parent as ViewBase;
        transform.SetParent(parentTransform != null ? parentTransform.transform : null, true);

        if (parent is ViewBase newParent)
            newParent.AddChildInternal(this);
    }

    // Children are added/removed only through SetParent, never directly.
    private void AddChildInternal(IView child)
    {
        if (child == null || _children.Contains(child)) return;
        _children.Add(child);
    }

    private void RemoveChildInternal(IView child) => _children.Remove(child);

    // ---- Lifecycle ----
    public virtual void Initialize()
    {
        if (State != ViewState.Created) return;

        if (_model == null)
        {
            // Convention: PlayerView reads PlayerControl's Model.
            var control = ControlRegistry.GetControl(ControlNameFor(GetType().Name));
            if (control != null && control.Model != null)
                Bind(control.Model);
        }

        ViewRegistry.Register(this);
        State = ViewState.Ready;
    }

    public virtual void Show()
    {
        if (State == ViewState.Destroyed) return;

        gameObject.SetActive(true);
        State = ViewState.Showing;
        Refresh();

        foreach (var child in Snapshot())
            child.Show();
    }

    public virtual void Hide()
    {
        if (State == ViewState.Destroyed) return;

        foreach (var child in Snapshot())
            child.Hide();

        gameObject.SetActive(false);
        State = ViewState.Ready;
    }

    public virtual void Refresh()
    {
        if (State != ViewState.Showing) return;

        OnRefresh();

        foreach (var child in Snapshot())
            child.Refresh();
    }

    public virtual void Shutdown()
    {
        if (State == ViewState.Destroyed) return;
        State = ViewState.Destroyed;

        foreach (var child in Snapshot())
            child.Shutdown();
        _children.Clear();

        if (_parent is ViewBase parent)
            parent.RemoveChildInternal(this);
        _parent = null;

        ViewRegistry.Unregister(this);
        Unsubscribe();

        if (gameObject != null)
            UnityEngine.Object.Destroy(gameObject);
    }

    // ---- Model binding ----
    // Called once by the Control; safe to call again with the same Model
    // (Initialize may already have resolved it by convention).
    public virtual void Bind(ModelBase model)
    {
        if (ReferenceEquals(_model, model)) return;

        if (_model != null) Unsubscribe(); // rebinding: drop the old subscriptions
        _model = model;
        Subscribe();
    }

    // The Control handed us a Model, so this View is ready to listen for changes.
    protected virtual void Subscribe() { }
    protected virtual void Unsubscribe() { }

    // Subclasses MUST implement this to push Model values into widgets.
    protected abstract void OnRefresh();

    // ---- Convenience accessors ----
    // Called as extension methods on purpose: a same-named instance method
    // would hide them and recurse into itself.
    protected T GetChild<T>() where T : class, IView => ViewExtensions.GetChild<T>(this);
    protected IView GetChild(string name) => ViewExtensions.GetChild(this, name);

    // ---- Shared data (see DataContext) ----
    public void SetData(string key, object value) => Data.Set(key, value);

    // Looks at this View first, then walks up to its parents.
    public T GetData<T>(string key) where T : class
    {
        for (IView view = this; view != null; view = view.Parent)
            if (view is ViewBase viewBase && viewBase.Data.Has(key))
                return viewBase.Data.Get<T>(key);
        return null;
    }

    // ---- Internals ----
    private IView[] Snapshot() => _children.ToArray();

    private static string ControlNameFor(string viewName)
    {
        const string suffix = "View";
        return viewName.EndsWith(suffix)
            ? viewName.Substring(0, viewName.Length - suffix.Length) + "Control"
            : viewName + "Control";
    }

    // Unity message: a GameObject destroyed from outside still has to unregister.
    private void OnDestroy()
    {
        if (State != ViewState.Destroyed)
            Shutdown();
    }
}
