using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public class GameLauncher : MonoBehaviour
{
    [SerializeField] private string bootLevelName = "Level01";

    private void Awake()
    {
        // Guarantee the registry exists before anything else runs.
        // Individual Controls self-register in their own Awake; the registry
        // drives their Initialize() in order during Start().
        var _ = ControlRegistry.Instance;
        var _2 = EventCenter.Instance;
    }

    private void Start()
    {
        // All Controls are initialized in registration order by ControlRegistry.
        // Do NOT call Initialize() manually here — that is the registry's job.
        ControlRegistry.Instance.InitializeAll();

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Game_Start,
            data = new ValueArgs<string>(bootLevelName)
        });
    }
}
