using System;
using UnityEngine;

// ============================================================
// ModelBase -- base class for all Models.
// Conventions:
//   1) Model is pure data; it must not reference View / Control / MonoBehaviour.
//   2) Model is serializable for easy save/load.
//   3) Model must not hold Unity object references other than value types (use ID / path instead).
// ============================================================
[Serializable]
public abstract class ModelBase
{
    public virtual void Reset()
    {
                // Reflectively reset all fields to their default values (can be overridden by subclasses).
    }
}
