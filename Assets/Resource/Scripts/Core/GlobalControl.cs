using System;
using UnityEngine;

// ============================================================
// GlobalControl<T, M> -- base class for every Control.
//
// A Control is a global singleton (XxxControl.Instance) that owns one Model
// and all the logic for it. Views only read the Model; they never write it.
//
// What you get for free:
//   1) The instance is created on demand and registered in ControlRegistry.
//   2) The Model is created in Awake (M : ModelBase, new()).
//   3) The linked View is initialized and bound in Initialize().
//   4) Tick / ResetState / Shutdown are driven by ControlRegistry, in
//      ControlPhase order, so a Control never writes its own Update().
//
// Override points, in lifecycle order:
//   OnInitialize()  -> start listening to events here
//   Tick(dt)        -> per-frame work
//   ResetState()    -> back to a clean state (call base first)
//   Unsubscribe()   -> stop listening (called on reset/shutdown)
//   OnTeardown()    -> extra cleanup before Shutdown
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
    ModelBase IControl.Model => model;
    public bool IsInitialized { get; private set; }

    // Sub-modules (PlayerControl under WarControl) are owned and ticked by
    // their parent, so they stay out of ControlRegistry. See WarControl.
    protected virtual bool AutoRegister => true;

    protected override void Awake()
    {
        base.Awake();
        if (IsDuplicate) return;

        EnsureModel();
        if (AutoRegister) ControlRegistry.RegisterControl(this);
    }

    private void EnsureModel()
    {
        if (overrideModel != null) model = overrideModel;
        else if (model == null) model = new M();
    }

    public virtual void Initialize()
    {
        if (IsInitialized) return;

        EnsureModel();

        if (view != null)
        {
            view.Initialize();
            view.Bind(model);
        }

        OnInitialize();
        IsInitialized = true;
    }

    // Subclasses override this instead of Initialize().
    protected virtual void OnInitialize() { }

    public virtual void Tick(float deltaTime) { }

    public virtual void ResetState()
    {
        model?.Reset();
        view?.Refresh();
    }

    public virtual void Shutdown()
    {
        view?.Shutdown();
        Unsubscribe();
        IsInitialized = false;
    }

    // The one way a View gets data: XxxControl.Instance.GetModel().
    public M GetModel() => model;

    protected virtual void Unsubscribe() { }

    // Not 'override': MonoSingleton<T> keeps its own OnDestroy.
    private void OnDestroy()
    {
        if (IsDuplicate) return;

        OnTeardown();
        ControlRegistry.UnregisterControl(this);
        Shutdown();
    }

    protected virtual void OnTeardown() { }
}
