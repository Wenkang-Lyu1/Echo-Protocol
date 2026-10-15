using System;
using System.Reflection;
using UnityEngine;

// ============================================================
// InputManager -- wraps Unity's Input System so the rest of the
// codebase never touches InputActionMap / CallbackContext directly.
//
// Compatibility: the Input System package was renamed several times
// across Unity versions:
//   - Unity 2019.3+ : UnityEngine.InputSystem (package)
//   - older projects : the same types may live in a different asmdef
//
// To avoid CS0234 / CS0246 we resolve the two key types by reflection
// and only bind when they are actually present. If the Input System
// is missing entirely the manager simply stays inert (no errors).
// ============================================================

[CreateAssetMenu(menuName = "EchoProtocol/InputProfile")]
public class InputProfile : ScriptableObject
{
    public string move = "Move";
    public string look = "Look";
    public string fire = "Fire";
    public string aim = "Aim";
    public string reload = "Reload";
    public string record = "Record";
    public string sprint = "Sprint";
    public string crouch = "Crouch";
    public string pause = "Pause";
}

public class InputManager : MonoSingleton<InputManager>
{
    [SerializeField] private InputProfile profile;
    [SerializeField] private float lookSensitivity = 0.5f;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool FireHeld { get; private set; }
    public bool AimHeld { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CrouchHeld { get; private set; }
    public float LookSensitivity => lookSensitivity;

    // Reflection handles so we never reference the package types directly.
    private object _playerInput;
    private object _actionMap;
    private MethodInfo _readValueVector2;
    private MethodInfo _readValueAsButton;

    private static readonly string[] _inputTypeNames =
    {
        "UnityEngine.InputSystem.PlayerInput, UnityEngine.InputSystem",
        "UnityEngine.InputSystem.UI.InputSystemUIInputModule, UnityEngine.InputSystem.UI",
    };

    protected override void Awake()
    {
        base.Awake();
        _playerInput = ResolveComponentByTypeName("UnityEngine.InputSystem.PlayerInput, UnityEngine.InputSystem");
        if (_playerInput == null)
            _playerInput = AddComponentIfMissing("UnityEngine.InputSystem.PlayerInput, UnityEngine.InputSystem");

        var ctxType = Type.GetType("UnityEngine.InputSystem.InputAction+CallbackContext, UnityEngine.InputSystem");
        if (ctxType != null)
        {
            _readValueVector2 = ctxType.GetMethod("ReadValue",
                new[] { typeof(Vector2) });
            _readValueAsButton = ctxType.GetMethod("ReadValueAsButton");
        }
    }

    private void OnDestroy()
    {
    }

    private object ResolveComponentByTypeName(string typeName)
    {
        var type = Type.GetType(typeName);
        if (type == null) return null;
        var comp = gameObject.GetComponent(type);
        return comp;
    }

    private object AddComponentIfMissing(string typeName)
    {
        var type = Type.GetType(typeName);
        if (type == null || !type.IsSubclassOf(typeof(Component))) return null;
        return gameObject.AddComponent(type);
    }

    private void OnEnable()
    {
        if (_playerInput == null) return;

        var actionsProp = _playerInput.GetType().GetProperty("actions");
        if (actionsProp == null) return;
        _actionMap = actionsProp.GetValue(_playerInput, null);
        if (_actionMap == null) return;

        Bind(profile.move,
            c => MoveInput = ReadVector2(c),
            null,
            c => MoveInput = Vector2.zero);
        Bind(profile.look,
            c => LookInput = ReadVector2(c),
            null,
            c => LookInput = Vector2.zero);
        Bind(profile.fire,
            c => FireHeld = true,
            c => FireHeld = false,
            null);
        Bind(profile.aim,
            c => AimHeld = true,
            c => AimHeld = false,
            null);
        Bind(profile.sprint,
            c => SprintHeld = true,
            c => SprintHeld = false,
            null);
        Bind(profile.crouch,
            c => CrouchHeld = true,
            c => CrouchHeld = false,
            null);
        Bind(profile.reload,
            null, null,
            c => EventCenter.Instance.Dispatch(new GameEventArgs
            {
                eventType = GameEvent.Weapon_Reload
            }));
        Bind(profile.record,
            null, null,
            c =>
            {
                EventCenter.Instance.Dispatch(new GameEventArgs
                {
                    eventType = ReadAsButton(c)
                        ? GameEvent.Echo_RecordStart
                        : GameEvent.Echo_RecordEnd
                });
            });
        Bind(profile.pause,
            null, null,
            c => EventCenter.Instance.Dispatch(new GameEventArgs
            {
                eventType = GameEvent.Game_TogglePause
            }));
    }

    private Vector2 ReadVector2(object ctx)
    {
        if (_readValueVector2 == null || ctx == null) return Vector2.zero;
        var result = _readValueVector2.Invoke(ctx, new object[] { default(Vector2) });
        return result is Vector2 v ? v : Vector2.zero;
    }

    private bool ReadAsButton(object ctx)
    {
        if (_readValueAsButton == null || ctx == null) return false;
        var result = _readValueAsButton.Invoke(ctx, null);
        return result is bool b && b;
    }

    private void Bind(string name,
        Action<object> started,
        Action<object> performed,
        Action<object> canceled)
    {
        if (string.IsNullOrEmpty(name) || _actionMap == null) return;

        var findMethod = _actionMap.GetType().GetMethod("FindAction",
            new[] { typeof(string) });
        if (findMethod == null) return;
        var action = findMethod.Invoke(_actionMap, new object[] { name });
        if (action == null) return;

        var startedEvent = action.GetType().GetEvent("started");
        var performedEvent = action.GetType().GetEvent("performed");
        var canceledEvent = action.GetType().GetEvent("canceled");

        if (started != null && startedEvent != null)
            startedEvent.AddEventHandler(action,
                Delegate.CreateDelegate(startedEvent.EventHandlerType,
                    started.Target, started.Method));
        if (performed != null && performedEvent != null)
            performedEvent.AddEventHandler(action,
                Delegate.CreateDelegate(performedEvent.EventHandlerType,
                    performed.Target, performed.Method));
        if (canceled != null && canceledEvent != null)
            canceledEvent.AddEventHandler(action,
                Delegate.CreateDelegate(canceledEvent.EventHandlerType,
                    canceled.Target, canceled.Method));
    }

    public void EnableGameplay(bool enable)
    {
        if (_playerInput == null) return;
        var actionsProp = _playerInput.GetType().GetProperty("actions");
        if (actionsProp == null) return;
        var actions = actionsProp.GetValue(_playerInput, null);
        if (actions == null) return;

        var method = actions.GetType().GetMethod(enable ? "Enable" : "Disable");
        method?.Invoke(actions, null);
    }
}
