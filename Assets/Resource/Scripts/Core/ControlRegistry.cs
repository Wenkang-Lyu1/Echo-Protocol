using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ControlRegistry -- the one place that drives every Control.
//
// It exists so no Control has to write its own Update/init plumbing:
//   InitializeAll -> TickAll (every frame) -> ResetAll -> ShutdownAll
//
// Adding a Control: make it inherit GlobalControl<T, M>. Its Awake
// registers it automatically; nothing else to edit.
//
// Optional: [RegisterControl(ControlPhase.Input)] declares when it runs
// during InitializeAll. Controls without it run last.
//
// Sub-modules owned by another Control (PlayerControl under WarControl)
// opt out with "protected override bool AutoRegister => false", so the
// registry never ticks them a second time.
// ============================================================
[DefaultExecutionOrder(-1500)]
public class ControlRegistry : MonoSingleton<ControlRegistry>
{
    private readonly Dictionary<Type, IControl> _byType = new Dictionary<Type, IControl>();
    private readonly Dictionary<string, IControl> _byName = new Dictionary<string, IControl>();
    private readonly List<IControl> _ordered = new List<IControl>();

    private bool _initialized;

    // ---- Registration ----
    public void Register(IControl control)
    {
        if (control == null) return;

        var type = control.GetType();
        if (!_byType.ContainsKey(type)) _byType[type] = control;
        if (!_byName.ContainsKey(type.Name)) _byName[type.Name] = control;

        InsertOrdered(control);

        // Late registration after the global init pass: initialize right away.
        if (_initialized && !control.IsInitialized)
        {
            try { control.Initialize(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    public void Unregister(IControl control)
    {
        if (control == null) return;

        var type = control.GetType();
        if (_byType.TryGetValue(type, out var byType) && byType == control) _byType.Remove(type);
        if (_byName.TryGetValue(type.Name, out var byName) && byName == control) _byName.Remove(type.Name);
        _ordered.Remove(control);
    }

    private void InsertOrdered(IControl control)
    {
        if (_ordered.Contains(control)) return;

        var order = OrderOf(control);
        var index = _ordered.FindIndex(existing => OrderOf(existing) > order);
        _ordered.Insert(index < 0 ? _ordered.Count : index, control);
    }

    private static int OrderOf(IControl control)
    {
        var attr = (RegisterControlAttribute)Attribute.GetCustomAttribute(
            control.GetType(), typeof(RegisterControlAttribute));
        return attr == null ? int.MaxValue : (int)attr.Phase + attr.Order;
    }

    // ---- Lookup ----
    public T Get<T>() where T : class, IControl => Get(typeof(T)) as T;

    public IControl Get(Type type) => _byType.TryGetValue(type, out var control) ? control : null;

    public IControl Get(string name)
    {
        // Names are type names, e.g. "PlayerControl".
        _byName.TryGetValue(name, out var control);
        return control;
    }

    public IEnumerable<IControl> All => _ordered;

    // ---- Static helpers (so callers never write ControlRegistry.Instance) ----
    public static void RegisterControl(IControl control)
    {
        if (Instance != null) Instance.Register(control);
    }

    public static void UnregisterControl(IControl control)
    {
        if (Instance != null) Instance.Unregister(control);
    }

    public static T GetControl<T>() where T : class, IControl
    {
        return Instance != null ? Instance.Get<T>() : null;
    }

    public static IControl GetControl(string name)
    {
        return Instance != null ? Instance.Get(name) : null;
    }

    // ---- Driver ----
    public void InitializeAll()
    {
        if (_initialized) return;

        // Snapshot: a Control may register another one while initializing.
        foreach (var control in _ordered.ToArray())
        {
            try { control.Initialize(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        _initialized = true;
    }

    public void TickAll(float deltaTime)
    {
        foreach (var control in _ordered)
        {
            if (!control.IsInitialized) continue;
            try { control.Tick(deltaTime); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    public void ResetAll()
    {
        foreach (var control in _ordered)
        {
            try { control.ResetState(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    public void ShutdownAll()
    {
        foreach (var control in _ordered)
        {
            try { control.Shutdown(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        _byType.Clear();
        _byName.Clear();
        _ordered.Clear();
        _initialized = false;
    }

    private void Update()
    {
        if (!_initialized) return;
        TickAll(Time.deltaTime);
    }
}

// ============================================================
// RegisterControl -- declares where a Control runs during InitializeAll.
// Ordered by phase first, then by "order" (lower runs earlier).
// ============================================================
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RegisterControlAttribute : Attribute
{
    public ControlPhase Phase { get; }
    public int Order { get; }

    public RegisterControlAttribute(ControlPhase phase = ControlPhase.Gameplay, int order = 0)
    {
        Phase = phase;
        Order = order;
    }
}

public enum ControlPhase
{
    Core = 0,        // EventCenter / GameManager
    Input = 100,     // InputManager
    Framework = 200, // DamageSystem / AssetLoader
    Gameplay = 300,  // War / Level
    UI = 400         // UIControl
}
