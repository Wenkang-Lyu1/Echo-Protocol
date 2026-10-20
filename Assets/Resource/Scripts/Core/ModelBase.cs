using System;

// ============================================================
// ModelBase -- base class for every Model.
//
// A Model is plain data: no MonoBehaviour, no Unity object references
// (store ids or paths instead), and [Serializable] so it can be saved.
//
// Reset() is safe to name that way because a Model is not a MonoBehaviour;
// Controls use ResetState() instead, see IControl.
// ============================================================
[Serializable]
public abstract class ModelBase
{
    // Restore default values. Subclasses override to reset their own fields.
    public virtual void Reset() { }
}
