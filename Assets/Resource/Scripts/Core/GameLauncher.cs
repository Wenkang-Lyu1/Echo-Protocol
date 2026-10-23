using UnityEngine;

// ============================================================
// GameLauncher -- the framework entry point. It lives on the Frame object
// of Scene_Start, next to ControlRegistry.
//
// It does three things:
//   1) Keeps the whole Frame tree alive across scene loads, so the global
//      singletons living under Frame (EventCenter, GameManager, ...) do not
//      have to care about persistence themselves.
//   2) Starts every Control, in ControlPhase order, once the scene is up.
//   3) Announces the boot level with Game_Start. Nothing listens yet in a
//      fresh project - wire a Control to it when the first level exists.
//
// Scene_Start is only needed once: the tree is persistent afterwards.
// ============================================================
[DefaultExecutionOrder(-1000)]
public class GameLauncher : MonoBehaviour
{
    [SerializeField] private string bootLevelName = "Level01";
    [SerializeField] private bool showLoginOnBoot = true;

    private void Awake()
    {
        // transform.root is the Frame object, so every child singleton under
        // it survives a scene change without its own DontDestroyOnLoad.
        DontDestroyOnLoad(transform.root.gameObject);
    }

    private void Start()
    {
        // Controls registered themselves in their Awake; the registry is the
        // only thing that calls Initialize() on them, in phase order.
        ControlRegistry.Instance.InitializeAll();

        if (showLoginOnBoot)
        {
            LoginControl.Instance.OpenLogin();
            return;
        }

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Game_Start,
            data = new ValueArgs<string>(bootLevelName)
        });
    }
}
