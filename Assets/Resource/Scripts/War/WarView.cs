using TMPro;
using UnityEngine;

// ============================================================
// WarView -- top-level HUD presentation layer for combat.
// Reads WarModel (read-only) through WarControl.GetModel().
// Must never alter any logic.
// ============================================================
public class WarView : ViewBase
{
    [SerializeField] private TextMeshProUGUI killText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private GameObject victoryBanner;
    [SerializeField] private GameObject defeatBanner;

    WarModel _warModel;

    protected override void Subscribe()
    {
        EventCenter.Instance.AddListener(GameEvent.War_KillCountChanged, OnKillChanged);
        EventCenter.Instance.AddListener(GameEvent.War_TimeChanged, OnTimeChanged);
        EventCenter.Instance.AddListener(GameEvent.War_End, OnWarEnd);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.War_KillCountChanged, OnKillChanged);
        EventCenter.Instance.RemoveListener(GameEvent.War_TimeChanged, OnTimeChanged);
        EventCenter.Instance.RemoveListener(GameEvent.War_End, OnWarEnd);
    }

    protected override void OnRefresh()
    {
        _warModel = _model as WarModel;
        if (_warModel == null) return;

        if (killText != null)
            killText.text = $"KILLS {_warModel.killCount}";
        if (timerText != null)
            timerText.text = $"TIME {_warModel.elapsedTime:F1}";
        if (stateText != null)
            stateText.text = _warModel.state.ToString().ToUpper();
    }

    void OnKillChanged(GameEventArgs args)
    {
        if (killText != null && args.data is IntPairArgs p)
            killText.text = $"KILLS {p.current}/{p.total}";
    }

    void OnTimeChanged(GameEventArgs args)
    {
        if (timerText != null && args.data is ValueArgs<float> t)
            timerText.text = $"TIME {t.value:F1}";
    }

    void OnWarEnd(GameEventArgs args)
    {
        if (args.data is ValueArgs<bool> v)
        {
            if (victoryBanner != null) victoryBanner.SetActive(v.value);
            if (defeatBanner != null) defeatBanner.SetActive(!v.value);
        }
    }
}
