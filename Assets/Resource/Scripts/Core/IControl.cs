using UnityEngine;

// ============================================================
// IControl -- interface that every Control must implement.
// Managed centrally by ControlRegistry.
// ============================================================
public interface IControl
{
    string ControlName { get; }
    bool IsInitialized { get; }
    bool HasModel { get; }

    void Initialize();
    void Tick(float deltaTime);
    void Reset();
    void Shutdown();

        // Entry point for View layer to read data; type-safe access is provided
    ModelBase GetModelUntyped();
}
