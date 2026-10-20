using UnityEngine;

// ============================================================
// WarControl -- top-level combat controller.
//
// It owns the combat flow (Prepare -> Active -> Paused -> Ended) and the
// four sub-modules that do the actual work:
//
//     WarControl          <- registered in ControlRegistry, ticks itself
//      +-- PlayerControl  <- sub-modules: created and ticked by WarControl
//      +-- WeaponControl
//      +-- EchoControl
//      +-- EnemyControl
//
// Sub-modules use "AutoRegister => false" so the registry does not tick
// them a second time.
// ============================================================
public class WarControl : GlobalControl<WarControl, WarModel>
{
    [Header("Sub-modules (leave empty to let them be created in code)")]
    [SerializeField] protected PlayerControl playerControl;
    [SerializeField] protected WeaponControl weaponControl;
    [SerializeField] protected EchoControl echoControl;
    [SerializeField] protected EnemyControl enemyControl;

    public PlayerControl Player => playerControl;
    public WeaponControl Weapon => weaponControl;
    public EchoControl Echo => echoControl;
    public EnemyControl Enemy => enemyControl;

    protected override void OnInitialize()
    {
        base.OnInitialize();

        if (playerControl == null) playerControl = gameObject.AddComponent<PlayerControl>();
        if (weaponControl == null) weaponControl = gameObject.AddComponent<WeaponControl>();
        if (echoControl == null) echoControl = gameObject.AddComponent<EchoControl>();
        if (enemyControl == null) enemyControl = gameObject.AddComponent<EnemyControl>();

        playerControl.Initialize();
        weaponControl.Initialize();
        echoControl.Initialize();
        enemyControl.Initialize();

        EventCenter.Instance.AddListener(GameEvent.Enemy_Die, OnEnemyDie);
        EventCenter.Instance.AddListener(GameEvent.Player_Died, OnPlayerDie);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Enemy_Die, OnEnemyDie);
        EventCenter.Instance.RemoveListener(GameEvent.Player_Died, OnPlayerDie);
    }

    public override void Tick(float deltaTime)
    {
        if (model.isPaused || model.state != WarState.Active) return;

        model.elapsedTime += deltaTime;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.War_TimeChanged,
            data = new ValueArgs<float>(model.elapsedTime)
        });

        // WarControl is the only driver of its sub-modules.
        playerControl.Tick(deltaTime);
        weaponControl.Tick(deltaTime);
        echoControl.Tick(deltaTime);
        enemyControl.Tick(deltaTime);
    }

    // ---- Flow ----
    public void StartWar()
    {
        if (model.state != WarState.Idle && model.state != WarState.Ended) return;

        model.state = WarState.Preparing;
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.War_Start,
            data = model
        });
        model.state = WarState.Active;
    }

    public void PauseWar()
    {
        model.isPaused = true;
        model.state = WarState.Paused;
        EventCenter.Instance.Dispatch(new GameEventArgs { eventType = GameEvent.War_Pause });
    }

    public void ResumeWar()
    {
        model.isPaused = false;
        model.state = WarState.Active;
        EventCenter.Instance.Dispatch(new GameEventArgs { eventType = GameEvent.War_Resume });
    }

    public void EndWar(bool victory)
    {
        model.state = WarState.Ended;
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.War_End,
            data = new ValueArgs<bool>(victory)
        });
    }

    public override void ResetState()
    {
        base.ResetState();
        playerControl?.ResetState();
        weaponControl?.ResetState();
        echoControl?.ResetState();
        enemyControl?.ResetState();
    }

    // ---- Event handlers ----
    private void OnEnemyDie(GameEventArgs args)
    {
        model.killCount++;
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.War_KillCountChanged,
            data = new IntPairArgs(model.killCount, WarVictoryUtil.GetRequiredKills(model))
        });

        if (WarVictoryUtil.CheckVictory(model))
            EndWar(true);
    }

    private void OnPlayerDie(GameEventArgs args)
    {
        EndWar(false);
    }
}
