using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================
// ControlRegistry -- global registry for all Control instances.
//
// Responsibilities:
//   1) Centrally manage all IControl instances.
//   2) Adding a new Control only requires one registration line here.
//   3) Drive Tick / Reset / Shutdown for all Controls in a unified way.
//   4) Provide lookup by type / name.
//   5) Manage initialization order.
//
// Usage example:
//   // Adding a new Control only needs:
//   [RegisterControl(typeof(MyControl), ControlPhase.Gameplay, 100)]
//   public class MyControl : GlobalControl<MyControl, MyModel> { }
// ============================================================

#region Registration Attribute
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class RegisterControlAttribute : Attribute
{
    public Type ControlType { get; }
    public ControlPhase Phase { get; }
    public int Order { get; }

    public RegisterControlAttribute(Type controlType,
        ControlPhase phase = ControlPhase.Gameplay, int order = 0)
    {
        ControlType = controlType;
        Phase = phase;
        Order = order;
    }
}

public enum ControlPhase
{
        Core = 0,        // EventCenter / GameManager / ...
    Input = 100,     // InputManager
    Framework = 200, // DamageSystem / AssetLoader
    Gameplay = 300,  // War / Level / UI
    UI = 400,        // UIControl / UIEventBinder
    View = 500       // Views
}
#endregion

[DefaultExecutionOrder(-1500)]
public class ControlRegistry : MonoSingleton<ControlRegistry>
{
    private readonly Dictionary<Type, IControl> _byType =
        new Dictionary<Type, IControl>();
    private readonly Dictionary<string, IControl> _byName =
        new Dictionary<string, IControl>();
    private readonly List<IControl> _ordered =
        new List<IControl>();

    private bool _initialized;

        // ---- Register / Unregister ----
    public void Register(IControl control)
    {
        if (control == null) return;

        // Late-registration safety: if the global init pass has already run,
        // initialize this Control immediately so it does not stay dormant.
        var wasInitialized = _initialized;
        var type = control.GetType();
        if (!_byType.ContainsKey(type))
            _byType[type] = control;
        if (!_byName.ContainsKey(control.ControlName))
            _byName[control.ControlName] = control;

        InsertOrdered(control);

        if (wasInitialized && !control.IsInitialized)
        {
            try { control.Initialize(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    public void Unregister(IControl control)
    {
        if (control == null) return;
        var type = control.GetType();
        if (_byType.TryGetValue(type, out var c) && c == control)
            _byType.Remove(type);
        if (_byName.TryGetValue(control.ControlName, out var cn) && cn == control)
            _byName.Remove(control.ControlName);
        _ordered.Remove(control);
    }

    private void InsertOrdered(IControl control)
    {
        var attr = (RegisterControlAttribute)control.GetType()
            .GetCustomAttributes(typeof(RegisterControlAttribute), false)
            .FirstOrDefault();
        var order = attr != null ? (int)attr.Phase + attr.Order : int.MaxValue;

        var i = 0;
        while (i < _ordered.Count)
        {
            var other = (RegisterControlAttribute)_ordered[i].GetType()
                .GetCustomAttributes(typeof(RegisterControlAttribute), false)
                .FirstOrDefault();
            var otherOrder = other != null ? (int)other.Phase + other.Order : int.MaxValue;
            if (otherOrder > order) break;
            i++;
        }
        if (!_ordered.Contains(control))
            _ordered.Insert(i, control);
    }

        // ---- Lookup ----
    public T Get<T>() where T : class, IControl
    {
        if (_byType.TryGetValue(typeof(T), out var c))
            return c as T;
        return null;
    }

    public IControl Get(string name)
    {
        _byName.TryGetValue(name, out var c);
        return c;
    }

    public IControl Get(Type type)
    {
        _byType.TryGetValue(type, out var c);
        return c;
    }

    public IEnumerable<IControl> All => _ordered;

    public bool IsRegistered<T>() where T : IControl => _byType.ContainsKey(typeof(T));

        // ---- Static convenience wrappers ----
    // These exist so callers do not need to write ControlRegistry.Instance,
    // and to avoid CS0120 (instance method called like a static one).
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

    // ---- Lifecycle Driver ----
    public void InitializeAll()
    {
        if (_initialized) return;
        foreach (var c in _ordered)
        {
            try { c.Initialize(); }
            catch (Exception e) { Debug.LogException(e); }
        }
        _initialized = true;
    }

    public void TickAll(float deltaTime)
    {
        foreach (var c in _ordered)
        {
            if (!c.IsInitialized) continue;
            try { c.Tick(deltaTime); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    public void ResetAll()
    {
        foreach (var c in _ordered)
        {
            try { c.Reset(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    public void ShutdownAll()
    {
        foreach (var c in _ordered)
        {
            try { c.Shutdown(); }
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
