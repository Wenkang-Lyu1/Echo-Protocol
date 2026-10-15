using System;
using UnityEngine;

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

public class GameManager : MonoSingleton<GameManager>
{
    public GameState State { get; private set; } = GameState.Boot;
    public float GameTime { get; private set; }

    public event Action<GameState, GameState> OnStateChanged;

    public bool IsPaused => State == GameState.Paused;

    public void SetState(GameState next)
    {
        if (State == next) return;
        var prev = State;
        State = next;

        switch (next)
        {
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
            case GameState.Paused:
                Time.timeScale = 0f;
                break;
            case GameState.GameOver:
            case GameState.Victory:
                Time.timeScale = 0f;
                break;
        }

        OnStateChanged?.Invoke(prev, next);
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Game_StateChanged,
            data = new StateChangeArgs<GameState>(prev, next)
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
