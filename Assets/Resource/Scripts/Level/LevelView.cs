using TMPro;
using UnityEngine;

// ============================================================
// LevelView -- presentation layer for the level objective.
// Reads data through LevelControl.GetModel().
// ============================================================
public class LevelView : ViewBase
{
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private UnityEngine.UI.Slider progressBar;

    LevelModel _levelModel;

    protected override void Subscribe()
    {
        EventCenter.Instance.AddListener(GameEvent.Level_WaveStart, OnWaveStart);
        EventCenter.Instance.AddListener(GameEvent.Level_ObjectiveUpdated, OnObjectiveUpdated);
        EventCenter.Instance.AddListener(GameEvent.Level_Complete, OnComplete);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Level_WaveStart, OnWaveStart);
        EventCenter.Instance.RemoveListener(GameEvent.Level_ObjectiveUpdated, OnObjectiveUpdated);
        EventCenter.Instance.RemoveListener(GameEvent.Level_Complete, OnComplete);
    }

    protected override void OnRefresh()
    {
        _levelModel = _model as LevelModel;
        SyncObjective();
    }

    void SyncObjective()
    {
        if (_levelModel == null) return;
        if (objectiveText != null)
        {
            var txt = !string.IsNullOrEmpty(_levelModel.objectiveText)
                ? _levelModel.objectiveText
                                : $"Enemies: {_levelModel.killCount}/{_levelModel.killRequirement}";
            objectiveText.text = txt;
        }
    }

    void OnWaveStart(GameEventArgs args)
    {
        if (waveText != null && args.data is ValueArgs<int> w)
            waveText.text = $"WAVE {w.value + 1}";
    }

    void OnObjectiveUpdated(GameEventArgs args)
    {
        SyncObjective();
        if (args.data is IntPairArgs p && progressBar != null)
        {
            progressBar.value = p.total > 0
                ? Mathf.Clamp01((float)p.current / p.total)
                : 0f;
        }
    }

    void OnComplete(GameEventArgs args)
    {
                if (objectiveText != null) objectiveText.text = "Objective Complete";
    }
}
