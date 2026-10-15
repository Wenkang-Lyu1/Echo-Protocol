// Animation state token used by Echo entities (recording ghost, playback ghost).
// Implemented as an instance token (not a static class) so it can be passed as a
// generic type argument to AnimDriver.Play<T>() without triggering CS0718.
public sealed class EchoAnimState : AnimDriver.IAnimStateToken
{
    public string Name { get; }

    private EchoAnimState(string name) { Name = name; }

    public static readonly EchoAnimState Idle = new EchoAnimState("Idle");
    public static readonly EchoAnimState Recording = new EchoAnimState("Recording");
    public static readonly EchoAnimState Playback = new EchoAnimState("Playback");
    public static readonly EchoAnimState Fire = new EchoAnimState("Fire");
    public static readonly EchoAnimState Reload = new EchoAnimState("Reload");
    public static readonly EchoAnimState Crouch = new EchoAnimState("Crouch");
    public static readonly EchoAnimState Die = new EchoAnimState("Die");
    
}