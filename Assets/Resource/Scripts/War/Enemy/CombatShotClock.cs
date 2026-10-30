using System;

/// <summary>Bounded catch-up; callers reset the accumulator when blocked, paused, or without a target.</summary>
public static class CombatShotClock
{
    public static int Advance(ref double accumulator, float dt, float interval)
    {
        accumulator += dt;
        int due = (int)Math.Floor((accumulator + 1e-8) / interval);
        if (due > 8) { accumulator = 0; return 8; }
        accumulator -= due * (double)interval;
        return due;
    }
}
