using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class U_Level_TimelineSV : UIViewBase
{
    [SerializeField] private Slider scrubber;
    [SerializeField] private Button playButton;
    [SerializeField] private Text playLabel;
    [SerializeField] private Text currentTime;
    [SerializeField] private Text emptyMessage;
    [SerializeField] private Text[] ticks;
    [SerializeField] private RectTransform rowsRoot;
    [SerializeField] private RectTransform playhead;
    [SerializeField] private U_Level_SegmentSV rowTemplate;
    private EchoControl _owner;
    private bool _refreshing;
    private readonly List<U_Level_SegmentSV> _rows = new List<U_Level_SegmentSV>();
    public void SetOwner(EchoControl owner) => _owner = owner;
    protected override void _Initialized() { }
    protected override void _FirstOpen() { }
    protected override void _Open() { }
    protected override void _Close() { }
    protected override void _UIWidget()
    {
        scrubber.onValueChanged.AddListener(Seek);
        playButton.onClick.AddListener(Play);
    }
    protected override void _Dispose()
    {
        scrubber.onValueChanged.RemoveListener(Seek);
        playButton.onClick.RemoveListener(Play);
    }
    private void Seek(float time)
    {
        if (!_refreshing) _owner.SeekPreparation(time);
    }
    private void Play() => _owner.TogglePreview();
    protected override void OnRefresh()
    {
        if (_owner == null) return;
        var m = _owner.GetModel();
        float span = _owner.PreviewEnd;
        _refreshing = true;
        try
        {
            scrubber.maxValue = Mathf.Max(.001f, span);
            scrubber.SetValueWithoutNotify(m.cursor);
        }
        finally { _refreshing = false; }
        scrubber.interactable = !m.isRecording && m.SelectedSegment != null;
        playButton.interactable = !m.isRecording && m.SelectedSegment != null && span > 0;
        playLabel.text = m.isPreviewPlaying ? "Pause Preview" : "Play Preview";
        currentTime.text = $"{m.cursor:0.00}s / {span:0.00}s";
        emptyMessage.gameObject.SetActive(false);
        for (int i = 0; i < ticks.Length; i++) ticks[i].text = (span * i / (ticks.Length - 1)).ToString("0.#") + "s";
        while (_rows.Count < m.slots.Count)
        {
            var row = Instantiate(rowTemplate, rowsRoot, false);
            row.gameObject.SetActive(true);
            row.Open();
            _rows.Add(row);
        }
        // Position lanes explicitly: an inactive template never participates in layout.
        // The content keeps a real width and height even with only one or two recordings.
        rowsRoot.sizeDelta = new Vector2(0, Mathf.Max(100, m.slots.Count * 48));
        for (int i = 0; i < _rows.Count; i++)
        {
            bool visible = i < m.slots.Count;
            _rows[i].gameObject.SetActive(visible);
            if (!visible) continue;
            var rect = (RectTransform)_rows[i].transform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -i * 48);
            rect.sizeDelta = new Vector2(0, 44);
            var slot = m.slots[i];
            _rows[i].Present(_owner, slot, m.segments.Find(s => s.id == slot.segmentId), !m.isRecording, m.segmentLimitSeconds);
        }
        float x = .2f + .68f * (span > 0 ? Mathf.Clamp01(m.cursor / span) : 0);
        playhead.anchorMin = new Vector2(x, 0); playhead.anchorMax = new Vector2(x, 1);
        playhead.anchoredPosition = Vector2.zero;
        playhead.SetAsLastSibling();
        playhead.gameObject.SetActive(false); // The scrubber handle marks the selected clip; rows are fixed slots.
    }
}
