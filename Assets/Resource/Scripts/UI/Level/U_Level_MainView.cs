using UnityEngine;
using UnityEngine.UI;

public sealed class U_Level_MainView : UIViewBase
{
    [SerializeField] private Text status;
    [SerializeField] private Text echoSkillStatus;
    [SerializeField] private Text stageTitle;
    [SerializeField] private Text objectiveStatus;
    [SerializeField] private Image uploadProgress;
    [SerializeField] private Image purgeFlash;
    [SerializeField] private RectTransform informationPanel, preparationPanelFrame, combatPanelFrame;
    [SerializeField] private RectTransform preparationStatusFrame, combatStatusFrame;
    [SerializeField] private string levelTitle = "LEVEL 01";
    [SerializeField] private GameObject preparationHelp, combatHelp;
    [SerializeField] private Button controlButton;
    [SerializeField] private Text controlLabel;
    [SerializeField] private Button speed01, speed03, speed1;
    [SerializeField] private U_Level_TimelineSV timeline;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button startButton, restartButton, exitButton;
    [SerializeField] private Button nextLevelButton;
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
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(NextLevel);
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
        if (nextLevelButton != null) nextLevelButton.onClick.RemoveListener(NextLevel);
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
    private void NextLevel() => _owner.CompleteAndAdvance();
    protected override void OnRefresh()
    {
        var m = _model as EchoModel;
        if (m == null || _owner == null) return;
        var level = _owner.GetModel();
        bool preparing = level.phase == LevelPhase.EchoPreparation || level.phase == LevelPhase.Idle;
        bool ended = level.phase == LevelPhase.Ended;
        Mount(informationPanel, preparing ? preparationPanelFrame : combatPanelFrame);
        Mount(status.rectTransform, preparing ? preparationStatusFrame : combatStatusFrame);
        stageTitle.text = levelTitle + (preparing ? " / ECHO PREPARATION" : ended ? " / COMBAT ENDED" : GameManager.Instance.IsPaused ? " / PAUSED" : " / COMBAT");
        preparationHelp.SetActive(preparing); combatHelp.SetActive(!preparing);
        timeline.gameObject.SetActive(preparing);
        if (echoSkillStatus != null)
        {
            echoSkillStatus.gameObject.SetActive(!preparing && !ended);
            string slots = "";
            foreach (var slot in m.slots)
            {
                var segment = m.segments.Find(s => s.id == slot.segmentId);
                string state = segment == null ? "Empty" : m.cooldownRemaining > 0 ? "Cooldown" : "Ready";
                slots += $"Echo {slot.slotIndex} [{slot.slotIndex}]   {state}\n";
            }
            echoSkillStatus.text = slots + (m.cooldownRemaining > 0 ? $"Echo Cooldown   {m.cooldownRemaining:0.0}s" : "Echo Cooldown   Ready");
        }
        startButton.gameObject.SetActive(preparing);
        controlButton.gameObject.SetActive(preparing);
        speed01.gameObject.SetActive(preparing); speed03.gameObject.SetActive(preparing); speed1.gameObject.SetActive(preparing);
        resultPanel.SetActive(ended);
        if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(ended && level.result == LevelResult.Victory && !string.IsNullOrWhiteSpace(_owner.Configuration.nextSceneName));
        if (purgeFlash != null)
        {
            float strength = !preparing && !ended ? _owner.PurgePulseStrength : 0;
            purgeFlash.color = new Color(1, .18f, .08f, strength * .28f);
            purgeFlash.gameObject.SetActive(strength > 0);
        }
        if (objectiveStatus != null)
        {
            objectiveStatus.transform.parent.gameObject.SetActive(_owner.UsesNodeObjectives && !ended);
            objectiveStatus.text = preparing ? "Prepare echoes near the relays.\nEnter to begin / Stay in each relay ring.\nDamage or leaving the ring resets upload." : _owner.NodeObjectiveText().TrimStart();
            if (uploadProgress != null) uploadProgress.fillAmount = _owner.UploadProgressRatio;
        }
        if (ended)
            resultText.text = (level.result == LevelResult.Victory ? "VICTORY" : "DEFEAT") + $"\nEnemies defeated: {level.killCount}\nCombat time: {level.combatElapsed:0.0}s";
        if (!preparing)
        {
            float remaining = level.RemainingTime;
            status.text = $"Time {(int)(remaining / 60):00}:{(int)(remaining % 60):00}\nHP {_owner.CombatPlayer.Hp:0} / 100 · Enemies {level.aliveEnemyCount}";
            if (objectiveStatus == null) status.text += _owner.NodeObjectiveText();
            if (level.bossState == BossSpawnState.Spawning) status.text += "\nRetrying boss spawn...";
            else if (level.bossSpawned && _owner.BossHealth > 0) status.text += $"\nPurge Unit {_owner.BossHealth:0} / {_owner.Configuration.destructionBossHealth:0} · {_owner.BossActionText}";
            resumeButton.gameObject.SetActive(!ended && (!_owner.Captured || GameManager.Instance.IsPaused));
            return;
        }
        string clipState = m.isRecording ? $"Recording {m.recordDuration:F2} / {m.segmentLimitSeconds:0}s" :
            m.SelectedSegment != null ? $"Selected Clip {m.SelectedSegment.duration:F2}s" : "Select a clip to preview";
        status.text = $"Clip Time {m.cursor:F2}s   Speed {m.EffectiveSpeed:0.0}x\n{clipState}\nSlots {m.segments.Count} / {m.slots.Count} · Empty {m.RemainingEchoes}";
        if (!string.IsNullOrEmpty(level.runtimeError)) status.text += "\n" + level.runtimeError;
        controlButton.interactable = m.isRecording || m.CanCreateEcho;
        controlLabel.text = m.isRecording ? "Save Echo (E)" : m.RemainingEchoes == 0 ? "All Slots Full" : "Create Echo (E)";
        speed01.interactable = m.selectedSpeed != .1f;
        speed03.interactable = m.selectedSpeed != .3f;
        speed1.interactable = m.selectedSpeed != 1;
        timeline.Refresh();
        resumeButton.gameObject.SetActive(!_owner.Captured);
    }

    // Both layouts are authored in UGUI; switching phase only selects the appropriate mount.
    private static void Mount(RectTransform content, RectTransform frame)
    {
        if (content == null || frame == null || content.parent == frame) return;
        content.SetParent(frame, false); content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = content.offsetMax = Vector2.zero;
    }
}
