using System;
using UnityEngine;

// ============================================================
// GameManager -- the game flow as one small state machine.
//
//     Boot -> MainMenu -> Loading -> Playing <-> Paused -> GameOver / Victory
//
// Every change is announced with Game_StateChanged, so UI and Controls can
// react without polling. Pausing also stops Time.timeScale here, which is
// the only place allowed to touch it.
// ============================================================
public enum GameState
{
    Boot,
    MainMenu,
    Loading,
    Playing,
    Paused,
    GameOver,
    Victory
}

[DefaultExecutionOrder(-900)]
public class GameManager : MonoSingleton<GameManager>
{
    public GameState State { get; private set; } = GameState.Boot;
    public float GameTime { get; private set; }

    public event Action<GameState, GameState> OnStateChanged;

    public bool IsPaused => State == GameState.Paused;

    public void SetState(GameState next)
    {
        if (State == next) return;

        var previous = State;
        State = next;

        switch (next)
        {
            case GameState.MainMenu:
            case GameState.Loading:
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
            case GameState.Paused:
            case GameState.GameOver:
            case GameState.Victory:
                Time.timeScale = 0f;
                break;
        }

        OnStateChanged?.Invoke(previous, next);

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Game_StateChanged,
            data = new StateChangeArgs<GameState>(previous, next)
        });
    }

    public void RequestPause() { if (State == GameState.Playing) SetState(GameState.Paused); }
    public void RequestResume() { if (State == GameState.Paused) SetState(GameState.Playing); }

    public void RequestGameOver()
    {
        if (State != GameState.Playing) return;
        SetState(GameState.GameOver);
        EventCenter.Instance.Dispatch(new GameEventArgs { eventType = GameEvent.Game_GameOver });
    }

    public void RequestVictory()
    {
        if (State != GameState.Playing) return;
        SetState(GameState.Victory);
        EventCenter.Instance.Dispatch(new GameEventArgs { eventType = GameEvent.Game_Victory });
    }

    private void Update()
    {
        if (State == GameState.Playing)
            GameTime += Time.unscaledDeltaTime;
    }
}
