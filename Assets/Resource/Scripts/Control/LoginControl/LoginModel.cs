using System;

[Serializable]
public sealed class LoginModel : ModelBase
{
    public bool isLoading;
    public bool settingsOpen;
    public float volume = 1f;
    public bool fullScreen;
    public string status = "";

    public override void Reset()
    {
        isLoading = false;
        settingsOpen = false;
        status = "";
    }
}
