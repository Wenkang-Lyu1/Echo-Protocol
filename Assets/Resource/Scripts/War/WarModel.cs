using System;
using UnityEngine;

[Serializable]
public class WarModel : ModelBase
{
    public WarState state = WarState.Idle;
    public int killCount;
    public float elapsedTime;
    public bool isPaused;

    public override void Reset()
    {
        state = WarState.Idle;
        killCount = 0;
        elapsedTime = 0f;
        isPaused = false;
    }
}

public enum WarState
{
    Idle,
    Preparing,
    Active,
    Paused,
    Ended
}
