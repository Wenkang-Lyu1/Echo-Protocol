using UnityEngine;
using UnityEngine.UI;

public sealed class U_Tutorial_MainView : UIViewBase
{
    [SerializeField] private Text objectives;
    [SerializeField] private Text echoStatus;
    [SerializeField] private Text targetStatus;
    [SerializeField] private Text message;
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
        objectives.text = (m.moved ? "✓" : "○") + "  WASD Move\n" +
            (m.defeated == m.totalEnemies ? "✓" : "○") + "  LMB Shoot training targets\n" +
            (m.echoesCreated > 0 ? "✓" : "○") + "  E Create an echo";
        echoStatus.text = "E  /  Echoes " + m.activeEchoes + "/3    ·    " +
            (m.echoCooldown > 0 ? m.echoCooldown.ToString("F1") + "s until ready" : "Ready") +
            "\nReplay the last " + m.recordedSeconds.ToString("F1") + "s of movement / shooting";
        targetStatus.text = "Targets   " + m.defeated + " / " + m.totalEnemies;
        message.text = !m.captured ? "Cursor released\nClick to resume" :
            m.defeated == m.totalEnemies && m.echoesCreated > 0 && m.moved ? "Training complete\nR Restart" : "";
        crosshair.color = m.hitFeedback > 0 ? new Color(1, .55f, .22f) : new Color(.6f, 1, 1);
    }
}
