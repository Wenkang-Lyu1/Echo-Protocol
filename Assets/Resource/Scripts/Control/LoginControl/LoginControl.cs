using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RegisterControl(ControlPhase.UI)]
public sealed class LoginControl : GlobalControl<LoginControl, LoginModel>
{
    [Tooltip("Must be enabled in Build Settings. Empty keeps the login screen open with a message.")]
    [SerializeField] private string gameplayScene = "Scene_Level1";
    public event Action Changed;
    private const string VolumeKey = "EchoProtocol.MasterVolume";
    private const string FullScreenKey = "EchoProtocol.FullScreen";

    protected override void OnInitialize()
    {
        model.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        model.fullScreen = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) != 0;
        AudioListener.volume = model.volume;
        Screen.fullScreen = model.fullScreen;
    }

    public void OpenLogin()
    {
        Initialize();
        model.Reset();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameManager.Instance.SetState(GameState.MainMenu);
        view = UIViewManager.Instance.Open<U_Login_MainView>("UI/Login/U_Login_MainView", model);
        Notify();
    }

    public void StartGame()
    {
        if (model.isLoading) return;
        if (string.IsNullOrWhiteSpace(gameplayScene) || !Application.CanStreamedLevelBeLoaded(gameplayScene))
        {
            model.status = "Set a gameplay scene in LoginControl and add it to Build Settings";
            Notify();
            return;
        }
        model.settingsOpen = false;
        model.isLoading = true;
        model.status = "Loading...";
        Notify();
        StartCoroutine(LoadGame());
    }

    private IEnumerator LoadGame()
    {
        var flow = GameManager.Instance;
        flow.SetState(GameState.Loading);
        AsyncOperation loading = null;
        try { loading = SceneManager.LoadSceneAsync(gameplayScene); }
        catch (Exception error) { Debug.LogException(error); }
        if (loading == null)
        {
            model.isLoading = false;
            model.status = "Failed to load the scene. Check Build Settings";
            flow.SetState(GameState.MainMenu);
            Notify();
            yield break;
        }
        while (!loading.isDone) yield return null;
        model.isLoading = false;
        if (view is UIViewBase ui) ui.Close();
        flow.SetState(GameState.Playing);
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Game_Start,
            data = new ValueArgs<string>(gameplayScene)
        });
        EventCenter.Instance.Dispatch(LoginEvent.OnLoginDone, new ValueArgs<string>(gameplayScene));
    }

    public void SetSettingsOpen(bool open)
    {
        if (model.isLoading) return;
        model.settingsOpen = open;
        if (!open) PlayerPrefs.Save();
        Notify();
    }

    public void SetVolume(float value)
    {
        model.volume = Mathf.Clamp01(value);
        AudioListener.volume = model.volume;
        PlayerPrefs.SetFloat(VolumeKey, model.volume);
        Notify();
    }

    public void SetFullScreen(bool value)
    {
        model.fullScreen = value;
        Screen.fullScreen = value;
        PlayerPrefs.SetInt(FullScreenKey, value ? 1 : 0);
        Notify();
    }

    public void QuitGame()
    {
        if (model.isLoading) return;
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Notify()
    {
        Changed?.Invoke();
        EventCenter.Instance.Dispatch(LoginEvent.OnChanged);
    }
}
