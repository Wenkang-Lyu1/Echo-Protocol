using UnityEngine;

// ============================================================
// UIEventBinder -- the single bridge between flow events and UI.
// UIManager only manages the panel stack; this class translates GameEvents into UI calls.
// ============================================================
public class UIEventBinder : MonoSingleton<UIEventBinder>
{
    private WarView _warView;

    protected override void Awake()
    {
        base.Awake();
        BindEvents();
    }

    private void Start()
    {
        _warView = FindObjectOfType<WarView>();
    }

    private void BindEvents()
    {
        var ec = EventCenter.Instance;

                // ---- Combat Flow ----
        ec.AddListener(GameEvent.War_Start, _ =>
        {
            UIControl.Instance.CloseAllExcept(UIPanelType.HUD);
            UIControl.Instance.ShowPanel(UIPanelType.HUD);
        });

        ec.AddListener(GameEvent.Game_TogglePause, _ =>
        {
            if (GameManager.Instance.State == GameState.Playing)
                UIControl.Instance.ShowPanel(UIPanelType.Pause);
            else if (GameManager.Instance.State == GameState.Paused)
                UIControl.Instance.ClosePanel(UIPanelType.Pause);
        });

        ec.AddListener(GameEvent.Game_GameOver, _ =>
            UIControl.Instance.ShowPanel(UIPanelType.Result));

        ec.AddListener(GameEvent.Game_Victory, _ =>
            UIControl.Instance.ShowPanel(UIPanelType.Result));

                // ---- Player ----
        ec.AddListener(GameEvent.Player_Hurt, e =>
        {
            if (e.data is ValueArgs<float> hp && _warView != null)
                _warView.Refresh();
        });

                // ---- Weapon ----
        ec.AddListener(GameEvent.Weapon_AmmoChanged, _ =>
        {
            if (_warView != null) _warView.Refresh();
        });

        // ---- Echo ----
        ec.AddListener(GameEvent.Echo_CountChanged, _ =>
        {
            if (_warView != null) _warView.Refresh();
        });

        ec.AddListener(GameEvent.Echo_DeployFailed, e =>
        {
            if (e.data is ValueArgs<string> s)
                EventCenter.Instance.Dispatch(new GameEventArgs
                {
                    eventType = GameEvent.UI_Message,
                    data = new ValueArgs<string>(s.value)
                });
        });

                // ---- Enemy ----
        ec.AddListener(GameEvent.War_KillCountChanged, _ =>
        {
            if (_warView != null) _warView.Refresh();
        });
    }
}
