#if UNITY_EDITOR
public sealed class EventValidationModel : ModelBase
{
    public int value;
    public override void Reset() { value = 0; }
}

public sealed class EventValidationControl : GlobalControl<EventValidationControl, EventValidationModel>
{
    protected override bool AutoRegister => false;
    protected override void OnInitialize() { AddEventListener(); }
    private void AddEventListener() { Events.Listen(GameEvent.Level_Start, OnLevelStart); }
    private void OnLevelStart(GameEventArgs args) { AddValue(1); }
    public void AddValue(int value) { model.value += value; }
}
#endif
