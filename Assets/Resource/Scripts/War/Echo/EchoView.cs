using TMPro;
using UnityEngine;

// ============================================================
// EchoView -- visual presentation layer for the Echo system.
// Reads data through EchoControl.GetModel().
// Responsible for: REC indicator, echo count, countdown.
// ============================================================
public class EchoView : ViewBase
{
        [Header("REC Indicator")]
    [SerializeField] private GameObject recIndicator;
    [SerializeField] private TextMeshProUGUI recTimerText;
    [SerializeField] private UnityEngine.UI.Image recBg;
    [SerializeField] private Color recColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private Color playbackColor = new Color(0.3f, 0.75f, 1f);
    [SerializeField] private Color idleColor = new Color(0.3f, 0.6f, 1f);

        [Header("Echo Count")]
    [SerializeField] private TextMeshProUGUI echoCountText;

    EchoModel _echoModel;

    protected override void Subscribe()
    {
        EventCenter.Instance.AddListener(GameEvent.Echo_RecordStart, OnRecordStart);
        EventCenter.Instance.AddListener(GameEvent.Echo_RecordEnd, OnRecordEnd);
        EventCenter.Instance.AddListener(GameEvent.Echo_RecordTick, OnRecordTick);
        EventCenter.Instance.AddListener(GameEvent.Echo_PlaybackStart, OnPlaybackStart);
        EventCenter.Instance.AddListener(GameEvent.Echo_PlaybackEnd, OnPlaybackEnd);
        EventCenter.Instance.AddListener(GameEvent.Echo_CountChanged, OnCountChanged);
        EventCenter.Instance.AddListener(GameEvent.Echo_DeployFailed, OnDeployFailed);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Echo_RecordStart, OnRecordStart);
        EventCenter.Instance.RemoveListener(GameEvent.Echo_RecordEnd, OnRecordEnd);
        EventCenter.Instance.RemoveListener(GameEvent.Echo_RecordTick, OnRecordTick);
        EventCenter.Instance.RemoveListener(GameEvent.Echo_PlaybackStart, OnPlaybackStart);
        EventCenter.Instance.RemoveListener(GameEvent.Echo_PlaybackEnd, OnPlaybackEnd);
        EventCenter.Instance.RemoveListener(GameEvent.Echo_CountChanged, OnCountChanged);
        EventCenter.Instance.RemoveListener(GameEvent.Echo_DeployFailed, OnDeployFailed);
    }

    protected override void OnRefresh()
    {
        _echoModel = _model as EchoModel;
        SyncCount();
    }

    void Update()
    {
        if (_echoModel == null) return;

        if (_echoModel.isRecording && recTimerText != null)
            recTimerText.text = $"REC {_echoModel.recordDuration:F1}s";
    }

    void SyncCount()
    {
        if (echoCountText != null && _echoModel != null)
            echoCountText.text = $"ECHO {_echoModel.ActiveEchoCount}/{_echoModel.maxConcurrentEchoes}";
    }

    void SetState(bool recording, bool playing)
    {
        if (recIndicator != null) recIndicator.SetActive(recording || playing);
        if (recBg != null)
            recBg.color = recording ? recColor : (playing ? playbackColor : idleColor);
        if (recTimerText != null)
            recTimerText.text = recording ? "REC" : (playing ? "PLAY" : "IDLE");
    }

    void OnRecordStart(GameEventArgs args) => SetState(true, false);
    void OnRecordEnd(GameEventArgs args) => SetState(false, false);
    void OnRecordTick(GameEventArgs args) { }
    void OnPlaybackStart(GameEventArgs args) => SetState(false, true);
    void OnPlaybackEnd(GameEventArgs args) => SetState(false, false);
    void OnCountChanged(GameEventArgs args) => SyncCount();

    void OnDeployFailed(GameEventArgs args)
    {
        if (args.data is ValueArgs<string> s)
            EventCenter.Instance.Dispatch(new GameEventArgs
            {
                eventType = GameEvent.UI_Message,
                data = new ValueArgs<string>(s.value)
            });
    }
}
