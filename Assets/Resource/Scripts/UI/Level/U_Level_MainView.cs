using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed class U_Level_MainView : UIViewBase
{
    [SerializeField] private Text status;
    [SerializeField] private Button controlButton;
    [SerializeField] private Text controlLabel;
    [SerializeField] private Button speed01, speed03, speed1;
    [SerializeField] private InputField timeInput;
    [SerializeField] private Button seekButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private U_Level_SegmentSV rowTemplate;
    [SerializeField] private Transform rowsRoot;
    private LevelControl _owner;
    private readonly List<U_Level_SegmentSV> _rows = new List<U_Level_SegmentSV>();
    public void SetOwner(LevelControl owner) => _owner = owner;
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
        seekButton.onClick.AddListener(Seek);
        resumeButton.onClick.AddListener(Resume);
    }
    protected override void _Dispose()
    {
        controlButton.onClick.RemoveListener(Toggle);
        speed01.onClick.RemoveListener(Speed01);
        speed03.onClick.RemoveListener(Speed03);
        speed1.onClick.RemoveListener(Speed1);
        seekButton.onClick.RemoveListener(Seek);
        resumeButton.onClick.RemoveListener(Resume);
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
        _owner.SetPreparationCaptured(true);
    }
    private void Seek()
    {
        if (float.TryParse(timeInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float time) &&
            _owner.PreparationEcho.SeekPreparation(time))
            timeInput.text = time.ToString("0.00", CultureInfo.InvariantCulture);
    }
    protected override void OnRefresh()
    {
        var m = _model as EchoModel;
        if (m == null || _owner == null) return;
        status.text = $"回声时间 {m.cursor:F2}s   流速 {m.EffectiveSpeed:0.0}x\n本段 {m.recordDuration:F2} / 20s   已保存 {m.segments.Count} 段";
        controlLabel.text = m.isRecording ? "保存并返回本体（E）" : "创建并操控影子（E）";
        timeInput.interactable = seekButton.interactable = !m.isRecording;
        speed01.interactable = m.selectedSpeed != .1f;
        speed03.interactable = m.selectedSpeed != .3f;
        speed1.interactable = m.selectedSpeed != 1;
        while (_rows.Count < m.segments.Count)
        {
            var row = Instantiate(rowTemplate, rowsRoot);
            row.gameObject.SetActive(true);
            row.Open();
            _rows.Add(row);
        }
        for (int i = 0; i < _rows.Count; i++)
        {
            _rows[i].gameObject.SetActive(i < m.segments.Count);
            if (i < m.segments.Count) _rows[i].Present(_owner.PreparationEcho, m.segments[i], !m.isRecording);
        }
        resumeButton.gameObject.SetActive(!_owner.Captured);
    }
}
