using UnityEngine;
using UnityEngine.UI;

public sealed class U_Tutorial_MainView : UIViewBase
{
    [SerializeField] private GameObject moveComplete;
    [SerializeField] private GameObject targetsComplete;
    [SerializeField] private GameObject echoComplete;
    [SerializeField] private Text echoCount;
    [SerializeField] private Text cooldownSeconds;
    [SerializeField] private GameObject cooldownStatus;
    [SerializeField] private GameObject readyStatus;
    [SerializeField] private Text recordedSeconds;
    [SerializeField] private Text targetCount;
    [SerializeField] private GameObject cursorMessage;
    [SerializeField] private GameObject completeMessage;
    [SerializeField] private Image crosshair;
    protected override void _Initialized() { }
    protected override void _UIWidget() { }
    protected override void _FirstOpen() { }
    protected override void _Open() { }
    protected override void _Close() { }
    protected override void _Dispose() { }
    protected override void OnRefresh()
    {
        var m = _model as TutorialModel;
        if (m == null) return;
        bool targetsDone = m.defeated == m.totalEnemies;
        bool echoCreated = m.echoesCreated > 0;
        bool coolingDown = m.echoCooldown > 0;
        moveComplete.SetActive(m.moved);
        targetsComplete.SetActive(targetsDone);
        echoComplete.SetActive(echoCreated);
        echoCount.text = m.activeEchoes.ToString();
        cooldownSeconds.text = m.echoCooldown.ToString("F1");
        cooldownSeconds.gameObject.SetActive(coolingDown);
        cooldownStatus.SetActive(coolingDown);
        readyStatus.SetActive(!coolingDown);
        recordedSeconds.text = m.recordedSeconds.ToString("F1");
        targetCount.text = m.defeated + " / " + m.totalEnemies;
        cursorMessage.SetActive(!m.captured);
        completeMessage.SetActive(m.captured && targetsDone && echoCreated && m.moved);
        crosshair.color = m.hitFeedback > 0 ? new Color(1, .55f, .22f) : new Color(.6f, 1, 1);
    }
}
