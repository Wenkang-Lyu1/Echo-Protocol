using System;
using UnityEngine;

// ============================================================
// GlobalControl<T, M> -- base class for all global Control objects.
//
// Rules:
//   1) Control is a global singleton, accessed via XxxControl.Instance.
//   2) Control holds a unique Model (persistent data).
//   3) Control is responsible for Model creation / load / save / reset.
//   4) View does NOT inherit from this class; View is bound by Control
//      during the Initialize phase.
//   5) Adding a new Control only requires one registration line in
//      ControlRegistry.
//
// Lifecycle: Initialize -> Tick -> ResetState -> Shutdown
// ============================================================
public abstract class GlobalControl<T, M> : MonoSingleton<T>, IControl
    where T : GlobalControl<T, M>
    where M : ModelBase, new()
{
    [SerializeField] protected ViewBase view;
    [SerializeField] protected M overrideModel;

    public M model { get; protected set; }
    public ViewBase View => view;

    // ---- IControl ----
    public string ControlName => typeof(T).Name;
    public bool IsInitialized { get; protected set; }
    public bool HasModel => model != null;
    ModelBase IControl.GetModelUntyped() => model;

    protected bool _initialized;

    protected override void Awake()
    {
        base.Awake();
        EnsureModel();
        ControlRegistry.RegisterControl(this);
    }

    private void EnsureModel()
    {
        if (overrideModel != null)
            model = overrideModel;
        else if (model == null)
            model = new M();
    }

    // Lifecycle: Initialize -> Tick -> ResetState -> Shutdown
    public virtual void Initialize()
    {
        if (_initialized) return;
        EnsureModel();
        if (view != null)
        {
            view.Initialize();
            view.Bind(model);
        }
        OnInitialize();
        _initialized = true;
        IsInitialized = true;
    }

    // Subclasses override this instead of Initialize().
    protected virtual void OnInitialize() { }

    // Convenience template: subclasses call InitializeWith(this, () => { ... })
    // to keep the Init / Reset / OnDestroy pattern consistent and avoid
    // mismatched overrides (CS0115 / CS0534).
    protected void InitializeWith(MonoBehaviour host, Action body)
    {
        if (_initialized) return;
        body();
        _initialized = true;
        IsInitialized = true;
    }

    // Unified teardown hook. Override this instead of MonoBehaviour.OnDestroy
    // so Shutdown runs in the same order everywhere.
    protected virtual void OnTeardown() { }

    public virtual void Tick(float deltaTime) { }

    public virtual void ResetState()
    {
        if (model != null)
            model.Reset();
        view?.Refresh();
    }

    public virtual void Shutdown()
    {
        view?.DestroyView();
        Unsubscribe();
        _initialized = false;
        IsInitialized = false;
    }

    // IControl.Reset -> forwards to ResetState(). The interface name is
    // kept for contractual compatibility; the framework-internal name
    // remains ResetState to avoid clashing with ModelBase.Reset().
    public void Reset()
    {
        ResetState();
    }

    // The only entry point for View layer to obtain Model data.
    public M GetModel() => model;

    protected virtual void Unsubscribe() { }

    protected virtual void Update()
    {
        if (!_initialized) return;
        Tick(Time.deltaTime);
    }

    // Not 'override': MonoSingleton<T> already owns OnDestroy.
    // This is a Unity message hook, so it still fires on destruction.
    private void OnDestroy()
    {
        OnTeardown();
        ControlRegistry.UnregisterControl(this);
        Shutdown();
    }
}
