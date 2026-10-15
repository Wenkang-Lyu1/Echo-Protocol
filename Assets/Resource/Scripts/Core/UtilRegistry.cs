using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// ============================================================
// UtilRegistry -- global registry for all Util utility classes.
//
// Responsibilities:
//   1) Centrally manage all static Util classes.
//   2) Adding a new Util only requires one registration line (or an attribute).
//   3) Provide lookup by type / name.
//   4) Support batch invocation (e.g. OnSceneLoaded broadcast to all Utils).
//
// Note: Util itself is a static class; the registry is only used for:
//   - categorization and introspection
//   - unified init / cleanup hooks
//   - editor tooling lists
//
// Usage example:
//   [RegisterUtil("Damage", UtilCategory.Combat)]
//   public static class DamageUtil { }
// ============================================================

#region Registration Attribute
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class RegisterUtilAttribute : Attribute
{
    public string DisplayName { get; }
    public UtilCategory Category { get; }

    public RegisterUtilAttribute(string displayName,
        UtilCategory category = UtilCategory.Misc)
    {
        DisplayName = displayName;
        Category = category;
    }
}

public enum UtilCategory
{
    Core,
    Combat,
    Movement,
    AI,
    UI,
    Audio,
    Save,
    Debug,
    Misc
}
#endregion

public class UtilRegistry
{
    private static readonly Dictionary<Type, UtilMeta> _utils =
        new Dictionary<Type, UtilMeta>();
    private static readonly Dictionary<string, UtilMeta> _byName =
        new Dictionary<string, UtilMeta>();

    private static bool _scanned;

        // ---- Register / Unregister ----
    public static void Register<T>(string displayName = null,
        UtilCategory category = UtilCategory.Misc) where T : class
    {
        var type = typeof(T);
        var meta = new UtilMeta
        {
            Type = type,
            DisplayName = displayName ?? type.Name,
            Category = category
        };

        if (!_utils.ContainsKey(type))
            _utils[type] = meta;
        if (!_byName.ContainsKey(meta.DisplayName))
            _byName[meta.DisplayName] = meta;

        ScanAttributes();
    }

    public static void Unregister<T>() where T : class
    {
        var type = typeof(T);
        if (_utils.TryGetValue(type, out var meta))
        {
            _utils.Remove(type);
            _byName.Remove(meta.DisplayName);
        }
    }

        // Scan all static classes decorated with [RegisterUtil] via reflection.
    public static void ScanAttributes()
    {
        if (_scanned) return;
        _scanned = true;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in asm.GetTypes())
            {
                if (!type.IsClass || !type.IsAbstract || !type.IsSealed)
                                        continue; // Only process static classes.

                var attr = (RegisterUtilAttribute)type
                    .GetCustomAttributes(typeof(RegisterUtilAttribute), false)
                    .FirstOrDefault();
                if (attr == null) continue;

                Register(type, attr.DisplayName, attr.Category);
            }
        }
    }

    private static void Register(Type type, string displayName, UtilCategory category)
    {
        var meta = new UtilMeta
        {
            Type = type,
            DisplayName = displayName,
            Category = category
        };
        if (!_utils.ContainsKey(type))
            _utils[type] = meta;
        if (!_byName.ContainsKey(displayName))
            _byName[displayName] = meta;
    }

        // ---- Lookup ----
    public static T Get<T>() where T : class
    {
        if (_utils.TryGetValue(typeof(T), out var m))
            return m.Type as T;
        return null;
    }

    public static UtilMeta GetMeta<T>() where T : class
    {
        _utils.TryGetValue(typeof(T), out var m);
        return m;
    }

    public static IEnumerable<UtilMeta> All => _utils.Values;

    public static IEnumerable<UtilMeta> GetByCategory(UtilCategory category)
    {
        foreach (var m in _utils.Values)
            if (m.Category == category)
                yield return m;
    }

    public static bool IsRegistered<T>() where T : class
        => _utils.ContainsKey(typeof(T));

        // ---- Batch Hooks ----
    public static void OnSceneLoaded(string sceneName)
    {
        foreach (var m in _utils.Values)
        {
            var method = m.Type.GetMethod("OnSceneLoaded",
                BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, new object[] { sceneName });
        }
    }

    public static void OnApplicationQuit()
    {
        foreach (var m in _utils.Values)
        {
            var method = m.Type.GetMethod("OnApplicationQuit",
                BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, null);
        }
    }
}

public struct UtilMeta
{
    public Type Type;
    public string DisplayName;
    public UtilCategory Category;

    public bool IsStatic => Type != null && Type.IsAbstract && Type.IsSealed;
}
