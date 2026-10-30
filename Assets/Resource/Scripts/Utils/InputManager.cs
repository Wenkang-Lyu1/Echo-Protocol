using System;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// InputManager -- the only place that talks to the Input System.
//
// The rest of the codebase reads MoveInput / LookInput / FireHeld ... and
// never touches InputActionMap or CallbackContext.
//
// The Input System package is resolved by reflection, so a project without
// it still compiles and simply runs with no input (no errors, no #if soup).
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
    public string echoSpeed = "EchoSpeed";
    public string sprint = "Sprint";
    public string crouch = "Crouch";
    public string pause = "Pause";
    public string startCombat = "StartCombat";
}

[DefaultExecutionOrder(-900)]
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
    public bool EchoPressed { get; private set; }
    public bool EchoSpeedPressed { get; private set; }
    public bool PausePressed { get; private set; }
    public bool RestartPressed { get; private set; }
    public bool FirePressed { get; private set; }
    public bool CursorTogglePressed { get; private set; }
    public bool StartCombatPressed { get; private set; }
    private bool _gameplayEnabled = true;
    private bool _pendingEcho, _pendingEchoSpeed;
    private bool _pendingStartCombat;
    public float LookSensitivity => lookSensitivity;

    // Reflection handles so we never reference the package types directly.
    private object _playerInput;
    private object _actionMap;
    private MethodInfo _readValueVector2;
    private MethodInfo _readValueAsButton;
    private readonly List<Action> _unbindActions = new List<Action>();

    protected override void Awake()
    {
        base.Awake();
        if (IsDuplicate) return;

        _playerInput = ResolveComponentByTypeName("UnityEngine.InputSystem.PlayerInput, Unity.InputSystem");
        if (_playerInput == null)
            _playerInput = AddComponentIfMissing("UnityEngine.InputSystem.PlayerInput, Unity.InputSystem");

        var ctxType = Type.GetType("UnityEngine.InputSystem.InputAction+CallbackContext, Unity.InputSystem");
        if (ctxType != null)
        {
            foreach (var method in ctxType.GetMethods())
                if (method.Name == "ReadValue" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0)
                    _readValueVector2 = method.MakeGenericMethod(typeof(Vector2));
            _readValueAsButton = ctxType.GetMethod("ReadValueAsButton");
        }
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
        if (profile == null) profile = ScriptableObject.CreateInstance<InputProfile>();

        Bind(profile.move,
            c => MoveInput = ReadVector2(c),
            c => MoveInput = ReadVector2(c),
            c => MoveInput = Vector2.zero);
        Bind(profile.look,
            c => LookInput = ReadVector2(c),
            c => LookInput = ReadVector2(c),
            c => LookInput = Vector2.zero);
        Bind(profile.fire,
            c => FireHeld = true,
            c => FireHeld = true,
            c => FireHeld = false);
        Bind(profile.aim,
            c => AimHeld = true,
            c => AimHeld = true,
            c => AimHeld = false);
        Bind(profile.sprint,
            c => SprintHeld = true,
            c => SprintHeld = true,
            c => SprintHeld = false);
        Bind(profile.crouch,
            c => CrouchHeld = true,
            c => CrouchHeld = true,
            c => CrouchHeld = false);
        Bind(profile.reload,
            null, null,
            c => EventCenter.Instance.Dispatch(new GameEventArgs
            {
                eventType = GameEvent.Weapon_Reload
            }));
        Bind(profile.record,
            null, c => _pendingEcho = true,
            c =>
            {
                EventCenter.Instance.Dispatch(new GameEventArgs
                {
                    eventType = ReadAsButton(c)
                        ? GameEvent.Echo_RecordStart
                        : GameEvent.Echo_RecordEnd
                });
            });
        Bind(profile.echoSpeed, null, c => _pendingEchoSpeed = true, null);
        Bind(profile.startCombat, null, c => _pendingStartCombat = true, null);
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
        var result = _readValueVector2.Invoke(ctx, null);
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
        bool withThrowArgument = findMethod == null;
        if (withThrowArgument) findMethod = _actionMap.GetType().GetMethod("FindAction", new[] { typeof(string), typeof(bool) });
        if (findMethod == null) return;
        var action = findMethod.Invoke(_actionMap, withThrowArgument ? new object[] { name, false } : new object[] { name });
        if (action == null) return;

        var startedEvent = action.GetType().GetEvent("started");
        var performedEvent = action.GetType().GetEvent("performed");
        var canceledEvent = action.GetType().GetEvent("canceled");

        Attach(action, startedEvent, started);
        Attach(action, performedEvent, performed);
        Attach(action, canceledEvent, canceled);
    }

    private void Attach(object action, EventInfo signal, Action<object> callback)
    {
        if (signal == null || callback == null) return;
        var argumentType = signal.EventHandlerType.GetMethod("Invoke").GetParameters()[0].ParameterType;
        var parameter = Expression.Parameter(argumentType, "context");
        var handler = Expression.Lambda(signal.EventHandlerType,
            Expression.Invoke(Expression.Constant(callback), Expression.Convert(parameter, typeof(object))), parameter).Compile();
        signal.AddEventHandler(action, handler);
        _unbindActions.Add(() => signal.RemoveEventHandler(action, handler));
    }

    private void OnDisable()
    {
        foreach (var unbind in _unbindActions) unbind();
        _unbindActions.Clear();
        _actionMap = null;
        MoveInput = LookInput = Vector2.zero;
        FireHeld = AimHeld = SprintHeld = CrouchHeld = false;
    }

    public void EnableGameplay(bool enable)
    {
        _gameplayEnabled = enable;
        if (!enable)
        {
            MoveInput = LookInput = Vector2.zero;
            FireHeld = AimHeld = SprintHeld = CrouchHeld = false;
        }
        if (_playerInput == null) return;
        var actionsProp = _playerInput.GetType().GetProperty("actions");
        if (actionsProp == null) return;
        var actions = actionsProp.GetValue(_playerInput, null);
        if (actions == null) return;

        var method = actions.GetType().GetMethod(enable ? "Enable" : "Disable");
        method?.Invoke(actions, null);
    }

    private void Update()
    {
        EchoPressed = _gameplayEnabled && _pendingEcho;
        EchoSpeedPressed = _gameplayEnabled && _pendingEchoSpeed;
        _pendingEcho = _pendingEchoSpeed = false;
        StartCombatPressed = _pendingStartCombat;
        _pendingStartCombat = false;
        // Cursor control must remain readable while gameplay actions are disabled.
        CursorTogglePressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        CursorTogglePressed = Input.GetMouseButtonDown(1);
        StartCombatPressed |= Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        PausePressed = Input.GetKeyDown(KeyCode.Escape);
        RestartPressed = Input.GetKeyDown(KeyCode.R);
        FirePressed = Input.GetMouseButtonDown(0);
        EchoPressed |= _gameplayEnabled && Input.GetKeyDown(KeyCode.E);
        EchoSpeedPressed |= _gameplayEnabled && Input.GetKeyDown(KeyCode.T);
        // This project ships without an Input System action asset. Keep keyboard
        // input usable while preserving the configured action-map path.
        if (_actionMap == null)
        {
            MoveInput = _gameplayEnabled ? new Vector2(
                (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)) : Vector2.zero;
            LookInput = _gameplayEnabled ? new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * .04f : Vector2.zero;
            FireHeld = _gameplayEnabled && Input.GetMouseButton(0);
            AimHeld = _gameplayEnabled && Input.GetMouseButton(1);
            SprintHeld = _gameplayEnabled && Input.GetKey(KeyCode.LeftShift);
            CrouchHeld = _gameplayEnabled && Input.GetKey(KeyCode.LeftControl);
        }
#endif
#if !ENABLE_LEGACY_INPUT_MANAGER
        var mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem") ??
            Type.GetType("UnityEngine.InputSystem.Mouse, UnityEngine.InputSystem");
        var mouse = mouseType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        var button = mouse != null ? mouseType.GetProperty("rightButton")?.GetValue(mouse) : null;
        if (button != null)
            CursorTogglePressed = (bool)button.GetType().GetProperty("wasPressedThisFrame").GetValue(button);
#endif
    }

}
