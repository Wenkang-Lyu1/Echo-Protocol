using UnityEngine;

public class GameEventArgs
{
    public GameEvent eventType;
    public object data;

    public T Get<T>()
    {
        if (data is T t) return t;
        return default;
    }
}

// ---- Generic event arguments ----
public readonly struct StateChangeArgs<T>
{
    public readonly T previous;
    public readonly T current;
    public StateChangeArgs(T p, T c) { previous = p; current = c; }
}

public readonly struct ValueArgs<T>
{
    public readonly T value;
    public ValueArgs(T v) { value = v; }
}

public readonly struct IntPairArgs
{
    public readonly int current;
    public readonly int total;
    public IntPairArgs(int c, int t) { current = c; total = t; }
}

public readonly struct FloatPairArgs
{
    public readonly float current;
    public readonly float total;
    public FloatPairArgs(float c, float t) { current = c; total = t; }
}

public readonly struct HitArgs
{
    public readonly GameObject target;
    public readonly GameObject source;
    public readonly float damage;
    public readonly bool isEcho;
    public HitArgs(GameObject t, GameObject s, float d, bool e) { target = t; source = s; damage = d; isEcho = e; }
}
