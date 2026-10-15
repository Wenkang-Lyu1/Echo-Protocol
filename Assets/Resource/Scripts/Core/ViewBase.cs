using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================
// ViewBase -- base class for all View objects (with parent-child
// lifecycle management).
//
// Hierarchy rules:
//   1) A View can host child Views (MainView -> CityView / RoleHeadView ...)
//   2) Hiding the parent View recursively hides all child Views.
//   3) Destroying the parent View destroys child Views together with
//      their GameObjects.
//   4) Parent and child access each other's data via GetChild / GetChild<T>.
//
// Data access rules:
//   View reads data ONLY through Control.GetModel()
//   AView reads AModel  -> AControl.Instance.GetModel()
//   AView reads BModel  -> BControl.Instance.GetModel().GetXxx()
//   Business calculation -> BUtil.GetXxx()
// ============================================================
public abstract class ViewBase : MonoBehaviour, IView
{
    [SerializeField] protected ModelBase _model;
    [SerializeField] protected bool destroyOnHide = false;

    protected readonly List<IView> _children = new List<IView>();
    protected IView _parent;

    // ---- IView ----
    public string ViewName => gameObject.name;
    public ViewState State { get; protected set; } = ViewState.Created;
    public IView Parent => _parent;
    public IEnumerable<IView> Children => _children;

    public void SetParent(IView parent)
    {
        if (_parent != null && _parent != parent)
            _parent.RemoveChild(this);
        _parent = parent;
        if (_parent != null && !_parent.Children.Any(c => c == this))
            _parent.AddChild(this);

        transform.SetParent(parent != null ? ((ViewBase)parent).transform : null,
            true);
    }

    public void AddChild(IView child)
    {
        if (child == null || _children.Contains(child)) return;
        _children.Add(child);
        child.SetParent(this);
    }

    public void RemoveChild(IView child)
    {
        if (child == null) return;
        _children.Remove(child);
    }

    // ---- Lifecycle ----
    public virtual void Initialize()
    {
        if (State != ViewState.Created) return;
        State = ViewState.Initializing;

        if (_model != null)
            Bind(_model);
        else
        {
            // Try to auto-resolve Model from the Control sharing this View's name.
            var ctrlName = _model != null
                ? _model.GetType().Name.Replace("Model", "Control")
                : "";
            var ctrl = ControlRegistry.GetControl(ctrlName);
            if (ctrl is IControl control && control.HasModel)
                Bind(control.GetModelUntyped());
        }

        State = ViewState.Ready;
        ViewRegistry.Register(this);
    }

    public virtual void Show()
    {
        if (State == ViewState.Destroyed) return;
        State = ViewState.Showing;
        gameObject.SetActive(true);
        Refresh();

        foreach (var c in _children)
            if (c.State == ViewState.Ready || c.State == ViewState.Hiding)
                c.Show();
    }

    public virtual void Hide()
    {
        if (State == ViewState.Destroyed) return;
        State = ViewState.Hiding;

        // Recursively hide all child Views.
        foreach (var c in _children)
            c.Hide();

        gameObject.SetActive(false);
        State = ViewState.Ready;
    }

    public virtual void Refresh()
    {
        if (State != ViewState.Showing) return;
        RefreshData();
        foreach (var c in _children)
            c.Refresh();
    }

    // IView.Destroy -> delegates to DestroyView() so the interface
    // contract is satisfied while the real teardown logic stays in one place.
    public void Destroy()
    {
        DestroyView();
    }

    // Default implementation: delegates to the mandatory OnRefresh().
    // Subclasses may override RefreshData directly, but the recommended
    // path is to override OnRefresh().
    public virtual void RefreshData()
    {
        OnRefresh();
    }

    public virtual void DestroyView()
    {
        if (State == ViewState.Destroyed) return;
        State = ViewState.Destroyed;

        // Recursively destroy all child Views.
        var snapshot = _children.ToArray();
        foreach (var c in snapshot)
            c.Destroy();

        _children.Clear();
        if (_parent != null) _parent.RemoveChild(this);
        _parent = null;

        ViewRegistry.Unregister(this);
        Unsubscribe();

        if (gameObject != null)
            UnityEngine.Object.Destroy(gameObject);
    }

    // Unity message (not 'override'): MonoBehaviour.OnDestroy is not virtual here,
    // and marking it override would hide the base message. This simply forwards to
    // the framework teardown path.
    private void OnDestroy()
    {
        if (State != ViewState.Destroyed)
            DestroyView();
    }

    // ---- Data binding (implemented by subclasses) ----
    public virtual void Bind(ModelBase model)
    {
        _model = model;
        Subscribe();
        State = ViewState.Ready;
    }

    // Subclasses MUST override this to push Model data into UI widgets.
    protected abstract void OnRefresh();
    protected virtual void Subscribe() { }
    protected virtual void Unsubscribe() { }

    // ---- Convenience accessors ----
    protected M GetModel<M>() where M : ModelBase => _model as M;

    protected T GetChild<T>() where T : class, IView => this.GetChild<T>();
    protected IView GetChild(string name) => this.GetChild(name);
}
