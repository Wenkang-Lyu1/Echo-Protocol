using UnityEditor;
using UnityEngine;

// ============================================================
// ScriptOrder -- centralized management of script execution order.
// Any script that accesses Instance inside Awake must be registered here.
// ============================================================
internal static class ScriptOrder
{
    [InitializeOnLoadMethod]
    private static void Apply()
    {
        Bind<EventCenter>(-2000);
        Bind<GameLauncher>(-1000);

        Bind<GameManager>(-900);
        Bind<InputManager>(-900);

        Bind<DamageSystem>(-850);
        Bind<AssetLoader>(-850);

        Bind<WarControl>(-800);
        Bind<PlayerControl>(-800);
        Bind<WeaponControl>(-800);
        Bind<EchoControl>(-800);
        Bind<EnemyControl>(-800);
        Bind<LevelControl>(-800);

        Bind<UIControl>(-700);
        Bind<UIEventBinder>(-700);

        Bind<PlayerView>(-500);
        Bind<WeaponView>(-500);
        Bind<EchoView>(-500);
        Bind<EnemyView>(-500);
        Bind<LevelView>(-500);
        Bind<WarView>(-500);
    }

    private static void Bind<T>(int order) where T : MonoBehaviour
    {
        var type = typeof(T);
        foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
        {
            if (script.GetClass() != type) continue;

            if (MonoImporter.GetExecutionOrder(script) != order)
                MonoImporter.SetExecutionOrder(script, order);
            return;
        }

        Debug.LogWarning($"[ScriptOrder] MonoScript not found for {type.FullName}.");
    }
}
