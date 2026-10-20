// ============================================================
// IControl -- what every Control provides.
//
// A Control is the only place where game logic lives. It owns exactly one
// Model and is registered in ControlRegistry, which drives the lifecycle:
//
//     Initialize() -> Tick(dt) ... -> ResetState() -> Shutdown()
//
// Reset is called ResetState on purpose: "Reset" is a Unity editor message
// on MonoBehaviours, so a runtime reset must use another name.
// ============================================================
public interface IControl
{
    // The data this Control owns. Never null after Initialize().
    ModelBase Model { get; }

    bool IsInitialized { get; }

    void Initialize();
    void Tick(float deltaTime);
    void ResetState();
    void Shutdown();
}
