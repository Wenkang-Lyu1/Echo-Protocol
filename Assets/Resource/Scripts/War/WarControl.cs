using System;
using UnityEngine;

// ============================================================
// WarControl -- top-level combat controller.
// Responsibilities:
//   1) Holds WarModel (persistent data).
//   2) Aggregates four sub-modules: Player / Weapon / Echo / Enemy.
//   3) Owns the combat flow: Prepare -> Start -> Pause -> End.
//   4) Only exposes Model access and flow APIs to the outside.
// ============================================================
public class WarControl : GlobalControl<WarControl, WarModel>
{
        [Header("Sub-module references (assign in Inspector or create in code)")]
    [SerializeField] protected PlayerControl playerControl;
    [SerializeField] protected WeaponControl weaponControl;
    [SerializeField] protected EchoControl echoControl;
    [SerializeField] protected EnemyControl enemyControl;

        // Sub-module accessors (other Controls reach them here).
    public PlayerControl Player => playerControl;
    public WeaponControl Weapon => weaponControl;
    public EchoControl Echo => echoControl;
    public EnemyControl Enemy => enemyControl;

    protected override void OnInitialize()
    {
        base.OnInitialize();

                // Ensure sub-modules exist.
        if (playerControl == null) playerControl = gameObject.AddComponent<PlayerControl>();
        if (weaponControl == null) weaponControl = gameObject.AddComponent<WeaponControl>();
        if (echoControl == null) echoControl = gameObject.AddComponent<EchoControl>();
        if (enemyControl == null) enemyControl = gameObject.AddComponent<EnemyControl>();

        playerControl.Initialize();
        weaponControl.Initialize();
        echoControl.Initialize();
        enemyControl.Initialize();

        BindEventListeners();
    }

    // Renamed to avoid hiding any future base-class member (CS0114).
    private void BindEventListeners()
    {
        EventCenter.Instance.AddListener(GameEvent.Enemy_Die, OnEnemyDie);
        EventCenter.Instance.AddListener(GameEvent.Player_Died, OnPlayerDie);
    }

    // 'override' removes CS0114 (hides inherited member).
    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Enemy_Die, OnEnemyDie);
        EventCenter.Instance.RemoveListener(GameEvent.Player_Died, OnPlayerDie);
    }

    public override void Tick(float deltaTime)
    {
        if (model.isPaused || model.state != WarState.Active) return;

        model.elapsedTime += deltaTime;

                // Notify UI to refresh elapsed time.
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.War_TimeChanged,
            data = new ValueArgs<float>(model.elapsedTime)
        });

        playerControl.Tick(deltaTime);
        weaponControl.Tick(deltaTime);
        echoControl.Tick(deltaTime);
        enemyControl.Tick(deltaTime);
    }

        // ---- Flow Interface ----
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
        playerControl?.Reset();
        weaponControl?.Reset();
        echoControl?.Reset();
        enemyControl?.Reset();
    }

        // ---- Event Handlers ----
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

    protected override void OnTeardown()
    {
        Unsubscribe();
        base.OnTeardown();
    }
}
