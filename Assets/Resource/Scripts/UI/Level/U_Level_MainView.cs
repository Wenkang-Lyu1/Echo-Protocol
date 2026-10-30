using UnityEngine;
using UnityEngine.UI;

public sealed class U_Level_MainView : UIViewBase
{
    [SerializeField] private Text status;
    [SerializeField] private Text stageTitle;
    [SerializeField] private GameObject preparationHelp, combatHelp;
    [SerializeField] private Button controlButton;
    [SerializeField] private Text controlLabel;
    [SerializeField] private Button speed01, speed03, speed1;
    [SerializeField] private U_Level_TimelineSV timeline;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button startButton, restartButton, exitButton;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultText;
    private LevelControl _owner;
    public void SetOwner(LevelControl owner)
    {
        _owner = owner;
        timeline.SetOwner(owner.PreparationEcho);
        timeline.Open();
    }
    protected override void _Initialized() { }
    protected override void _FirstOpen() { }
    protected override void _Open() { }
    protected override void _Close() { }
    protected override void _UIWidget()
    {
        controlButton.onClick.AddListener(Toggle);
        speed01.onClick.AddListener(Speed01);
        speed03.onClick.AddListener(Speed03);
        speed1.onClick.AddListener(Speed1);
        resumeButton.onClick.AddListener(Resume);
        startButton.onClick.AddListener(StartCombat);
        restartButton.onClick.AddListener(Restart);
        exitButton.onClick.AddListener(Exit);
    }
    protected override void _Dispose()
    {
        controlButton.onClick.RemoveListener(Toggle);
        speed01.onClick.RemoveListener(Speed01);
        speed03.onClick.RemoveListener(Speed03);
        speed1.onClick.RemoveListener(Speed1);
        resumeButton.onClick.RemoveListener(Resume);
        startButton.onClick.RemoveListener(StartCombat);
        restartButton.onClick.RemoveListener(Restart);
        exitButton.onClick.RemoveListener(Exit);
    }
    private void Toggle()
    {
        if (_owner.PreparationEcho.ToggleControl()) Resume();
    }
    private void Speed01() => _owner.PreparationEcho.SetPreparationSpeed(.1f);
    private void Speed03() => _owner.PreparationEcho.SetPreparationSpeed(.3f);
    private void Speed1() => _owner.PreparationEcho.SetPreparationSpeed(1);
    private void Resume()
    {
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        if (GameManager.Instance.IsPaused) GameManager.Instance.RequestResume();
        _owner.SetPreparationCaptured(true);
    }
    private void StartCombat() => _owner.StartCombat();
    private void Restart() => _owner.RestartPreparation();
    private void Exit() => _owner.ExitLevel();
    protected override void OnRefresh()
    {
        var m = _model as EchoModel;
        if (m == null || _owner == null) return;
        var level = _owner.GetModel();
        bool preparing = level.phase == LevelPhase.EchoPreparation || level.phase == LevelPhase.Idle;
        bool ended = level.phase == LevelPhase.Ended;
        stageTitle.text = preparing ? "LEVEL 01 / ECHO PREPARATION" : ended ? "LEVEL 01 / COMBAT ENDED" : GameManager.Instance.IsPaused ? "LEVEL 01 / PAUSED" : "LEVEL 01 / COMBAT";
        preparationHelp.SetActive(preparing); combatHelp.SetActive(!preparing);
        timeline.gameObject.SetActive(preparing);
        startButton.gameObject.SetActive(preparing);
        controlButton.gameObject.SetActive(preparing);
        speed01.gameObject.SetActive(preparing); speed03.gameObject.SetActive(preparing); speed1.gameObject.SetActive(preparing);
        resultPanel.SetActive(ended);
        if (ended)
            resultText.text = (level.result == LevelResult.Victory ? "VICTORY" : "DEFEAT") + $"\nEnemies defeated: {level.killCount}\nCombat time: {level.combatElapsed:0.0}s";
        if (!preparing)
        {
            float remaining = level.RemainingTime;
            status.text = $"Time {(int)(remaining / 60):00}:{(int)(remaining % 60):00}\nHP {_owner.CombatPlayer.Hp:0} / 100 · Enemies {level.aliveEnemyCount}";
            if (level.bossState == BossSpawnState.Spawning) status.text += "\nRetrying boss spawn...";
            else if (level.bossSpawned && _owner.BossHealth > 0) status.text += $"\nDestruction Boss {_owner.BossHealth:0} / 10000";
            resumeButton.gameObject.SetActive(!ended && (!_owner.Captured || GameManager.Instance.IsPaused));
            return;
        }
        status.text = $"Echo Time {m.cursor:F2}s   Speed {m.EffectiveSpeed:0.0}x\nClip {m.recordDuration:F2} / 20s\nEchoes {m.segments.Count} / {m.echoLimit} · Left {m.RemainingEchoes}";
        if (!string.IsNullOrEmpty(level.runtimeError)) status.text += "\n" + level.runtimeError;
        controlButton.interactable = m.isRecording || m.CanCreateEcho;
        controlLabel.text = m.isRecording ? "Save Echo (E)" : m.RemainingEchoes == 0 ? "No Echo Uses Left" : "Create Echo (E)";
        speed01.interactable = m.selectedSpeed != .1f;
        speed03.interactable = m.selectedSpeed != .3f;
        speed1.interactable = m.selectedSpeed != 1;
        timeline.Refresh();
        resumeButton.gameObject.SetActive(!_owner.Captured);
    }
}
