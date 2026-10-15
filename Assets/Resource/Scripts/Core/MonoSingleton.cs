using UnityEngine;

public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _shuttingDown;

    public static T Instance
    {
        get
        {
            if (_shuttingDown) return null;
            lock (_lock)
            {
                if (_instance == null)
                {
                    var go = new GameObject(typeof(T).Name);
                    _instance = go.AddComponent<T>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this as T;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnApplicationQuit() { _shuttingDown = true; }
}
