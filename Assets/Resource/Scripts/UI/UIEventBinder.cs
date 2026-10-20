using UnityEngine;

// ============================================================
// UIEventBinder bridges combat events to HUD updates.
// UIControl owns pause/result events; never subscribe to those a second time here.
// ============================================================
[DefaultExecutionOrder(-700)]
public class UIEventBinder : MonoSingleton<UIEventBinder>
{
    private WarView _warView;
    private EventCenter _events;

    protected override void Awake()
    {
        base.Awake();
        if (IsDuplicate) return;

        BindEvents();
    }

    private void Start()
    {
        _warView = FindObjectOfType<WarView>();
    }

    private void BindEvents()
    {
        _events = EventCenter.Instance;
        _events.AddListener(GameEvent.War_Start, OnWarStart);
        _events.AddListener(GameEvent.Player_Hurt, OnHudChanged);
        _events.AddListener(GameEvent.Weapon_AmmoChanged, OnHudChanged);
        _events.AddListener(GameEvent.Echo_CountChanged, OnHudChanged);
        _events.AddListener(GameEvent.War_KillCountChanged, OnHudChanged);
        _events.AddListener(GameEvent.Echo_DeployFailed, OnDeployFailed);
    }

    private void OnWarStart(GameEventArgs args)
    {
        UIControl.Instance.CloseAllExcept(UIPanelType.HUD);
        UIControl.Instance.ShowPanel(UIPanelType.HUD);
    }

    private void OnHudChanged(GameEventArgs args)
    {
        if (_warView == null) _warView = FindObjectOfType<WarView>();
        if (_warView != null) _warView.Refresh();
    }

    private void OnDeployFailed(GameEventArgs args)
    {
        if (args.data is ValueArgs<string> message)
            _events.Dispatch(new GameEventArgs
            {
                eventType = GameEvent.UI_Message,
                data = new ValueArgs<string>(message.value)
            });
    }

    private void OnDestroy()
    {
        if (_events == null) return;
        _events.RemoveListener(GameEvent.War_Start, OnWarStart);
        _events.RemoveListener(GameEvent.Player_Hurt, OnHudChanged);
        _events.RemoveListener(GameEvent.Weapon_AmmoChanged, OnHudChanged);
        _events.RemoveListener(GameEvent.Echo_CountChanged, OnHudChanged);
        _events.RemoveListener(GameEvent.War_KillCountChanged, OnHudChanged);
        _events.RemoveListener(GameEvent.Echo_DeployFailed, OnDeployFailed);
    }
}
