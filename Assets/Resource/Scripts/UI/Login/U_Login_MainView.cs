using UnityEngine;
using UnityEngine.UI;

public sealed class U_Login_MainView : UIViewBase
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeSettingsButton;
    [SerializeField] private GameObject settingsRoot;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Toggle fullScreenToggle;
    [SerializeField] private Text statusText;
    [SerializeField] private UIModelPreview modelPreview;
    private LoginControl _control;

    protected override void _Initialized()
    {
        if (startButton == null || quitButton == null || settingsButton == null ||
            closeSettingsButton == null || settingsRoot == null || volumeSlider == null ||
            fullScreenToggle == null || statusText == null || modelPreview == null)
            throw new System.InvalidOperationException(name + ": 登录界面组件引用不完整");
        _control = LoginControl.Instance;
    }

    protected override void _UIWidget()
    {
        startButton.onClick.AddListener(OnStart);
        quitButton.onClick.AddListener(OnQuit);
        settingsButton.onClick.AddListener(OnSettings);
        closeSettingsButton.onClick.AddListener(OnCloseSettings);
        volumeSlider.onValueChanged.AddListener(OnVolume);
        fullScreenToggle.onValueChanged.AddListener(OnFullScreen);
    }

    protected override void _FirstOpen() { AddEventListener(); }

    private void AddEventListener()
    {
        Events.Listen(LoginEvent.OnChanged, OnLoginChanged);
    }

    private void OnLoginChanged(GameEventArgs args) => Refresh();

    protected override void _Open()
    {
        modelPreview.Show();
    }

    protected override void _Close()
    {
        settingsRoot.SetActive(false);
        modelPreview.Hide();
    }

    protected override void _Dispose()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStart);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuit);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
        if (closeSettingsButton != null) closeSettingsButton.onClick.RemoveListener(OnCloseSettings);
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(OnVolume);
        if (fullScreenToggle != null) fullScreenToggle.onValueChanged.RemoveListener(OnFullScreen);
        if (modelPreview != null) modelPreview.Release();
    }

    protected override void OnRefresh()
    {
        var data = _model as LoginModel;
        if (data == null) return;
        startButton.interactable = !data.isLoading;
        quitButton.interactable = !data.isLoading;
        settingsButton.interactable = !data.isLoading;
        settingsRoot.SetActive(data.settingsOpen && !data.isLoading);
        volumeSlider.SetValueWithoutNotify(data.volume);
        fullScreenToggle.SetIsOnWithoutNotify(data.fullScreen);
        statusText.text = data.status;
    }

    private void OnStart() => _control.StartGame();
    private void OnQuit() => _control.QuitGame();
    private void OnSettings() => _control.SetSettingsOpen(true);
    private void OnCloseSettings() => _control.SetSettingsOpen(false);
    private void OnVolume(float value) => _control.SetVolume(value);
    private void OnFullScreen(bool value) => _control.SetFullScreen(value);
}
