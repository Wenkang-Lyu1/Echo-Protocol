using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// UIControl -- UI panel stack manager.
// Responsibilities:
//   1) Holds UIModel (current panel / stack).
//   2) Manages panel show / hide and the back-stack.
//   3) Cooperates with GameManager (pause / resume).
// ============================================================
public class UIControl : GlobalControl<UIControl, UIModel>
{
    [SerializeField] private UIPanel[] panels;

    private readonly Dictionary<UIPanelType, UIPanel> _registry =
        new Dictionary<UIPanelType, UIPanel>();

    protected override void OnInitialize()
    {
        base.OnInitialize();
        foreach (var p in panels)
        {
            if (p == null || p.Type == UIPanelType.None) continue;
            _registry[p.Type] = p;
        }

        EventCenter.Instance.AddListener(GameEvent.Game_TogglePause, OnTogglePause);
        EventCenter.Instance.AddListener(GameEvent.Game_GameOver, OnGameOver);
        EventCenter.Instance.AddListener(GameEvent.Game_Victory, OnVictory);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Game_TogglePause, OnTogglePause);
        EventCenter.Instance.RemoveListener(GameEvent.Game_GameOver, OnGameOver);
        EventCenter.Instance.RemoveListener(GameEvent.Game_Victory, OnVictory);
    }

    public bool IsOpen(UIPanelType type)
    {
        return _registry.TryGetValue(type, out var p) && p != null && p.IsVisible;
    }

    public void ShowPanel(UIPanelType type, bool remember = true)
    {
        if (!_registry.TryGetValue(type, out var p) || p == null) return;

        if (remember && type != UIPanelType.HUD && !model.panelStack.Contains(type))
            model.panelStack.Push(type);

        p.Show();
        p.OnFocusGained();
        model.currentPanel = type;
        model.isAnyPanelOpen = true;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.UI_OpenPanel,
            data = new ValueArgs<UIPanelType>(type)
        });

                // Resume gameplay when HUD is closed.
        if (type == UIPanelType.Pause)
            GameManager.Instance?.RequestPause();
    }

    public void ClosePanel(UIPanelType type)
    {
        if (!_registry.TryGetValue(type, out var p) || p == null) return;
        p.Hide();
        p.OnFocusLost();

        var stack = new Stack<UIPanelType>(model.panelStack);
        model.panelStack.Clear();
        foreach (var t in stack)
            if (t != type) model.panelStack.Push(t);

        model.currentPanel = model.panelStack.Count > 0
            ? model.panelStack.Peek()
            : UIPanelType.None;
        model.isAnyPanelOpen = model.currentPanel != UIPanelType.None;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.UI_ClosePanel,
            data = new ValueArgs<UIPanelType>(type)
        });

        if (type == UIPanelType.Pause)
            GameManager.Instance?.RequestResume();
    }

    public void CloseTop()
    {
        if (model.panelStack.Count == 0) return;
        ClosePanel(model.panelStack.Pop());
    }

    public void CloseAllExcept(UIPanelType keep)
    {
        var copy = new Stack<UIPanelType>(model.panelStack);
        model.panelStack.Clear();
        foreach (var t in copy)
        {
            if (t == keep) continue;
            if (_registry.TryGetValue(t, out var p) && p != null) p.Hide();
        }
        model.currentPanel = keep;
    }

    private void OnTogglePause(GameEventArgs args)
    {
        if (GameManager.Instance.State == GameState.Playing)
            ShowPanel(UIPanelType.Pause);
        else if (GameManager.Instance.State == GameState.Paused)
            ClosePanel(UIPanelType.Pause);
    }

    private void OnGameOver(GameEventArgs args) => ShowPanel(UIPanelType.Result);
    private void OnVictory(GameEventArgs args) => ShowPanel(UIPanelType.Result);

    public T GetPanel<T>(UIPanelType type) where T : UIPanel
    {
        return _registry.TryGetValue(type, out var p) ? p as T : null;
    }
}
