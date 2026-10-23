using UnityEngine;

// ============================================================
// MonoSingleton -- base class for global singletons.
//
// Rules:
//   1) One instance per type, reached through T.Instance.
//   2) If the scene already has one (see the Frame object in Scene_Start),
//      that instance wins; a second copy destroys its own GameObject.
//   3) Instance creates the object only when the scene has none, so a
//      prototype can be dropped on any GameObject without setup.
//   4) A root-level instance is kept across scene loads. An instance that
//      sits under a parent (Frame/...) does not need this: its root carries it.
//   5) After base.Awake(), check IsDuplicate before doing any work.
// ============================================================
public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    private static T _instance;
    private static bool _quitting;

    public static T Instance
    {
        get
        {
            if (_quitting) return null;
            if (_instance == null)
            {
                // AddComponent runs Awake immediately, which assigns _instance.
                var go = new GameObject(typeof(T).Name);
                _instance = go.AddComponent<T>();
            }
            return _instance;
        }
    }

    // True when this copy lost to an existing instance and is being destroyed.
    protected bool IsDuplicate { get; private set; }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            IsDuplicate = true;
            Destroy(gameObject);
            return;
        }

        _instance = this as T;

        // Only the outermost object is made persistent; children follow their root.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnApplicationQuit() { _quitting = true; }
}
