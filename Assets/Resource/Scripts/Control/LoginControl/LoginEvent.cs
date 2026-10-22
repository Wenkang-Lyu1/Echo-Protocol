/// <summary>Login module notifications, using the existing event center identifiers.</summary>
public static class LoginEvent
{
    public const GameEvent OnChanged = GameEvent.Login_Changed;
    public const GameEvent OnLoginDone = GameEvent.Login_Done;
}
