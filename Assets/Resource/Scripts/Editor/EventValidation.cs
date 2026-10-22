#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Real-frame event tests, restricted to a disposable Unity project.</summary>
public static class EventValidation
{
    private static readonly List<EventScope> Scopes = new List<EventScope>();
    private static readonly List<string> Results = new List<string>();
    private static EventCenter _center;
    private static int _step, _a, _b, _c, _oldCalls, _newCalls, _exceptionLogs;
    private static double _timeout;

    public static void RunBatch()
    {
        if (!Path.GetFullPath(Application.dataPath).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("EventValidation requires a temporary project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        _step = 0; _a = _b = _c = -1; _oldCalls = _newCalls = _exceptionLogs = 0;
        Results.Clear(); Scopes.Clear();
        _timeout = EditorApplication.timeSinceStartup + 45;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static EventScope Scope()
    {
        var scope = new EventScope(_center); Scopes.Add(scope); return scope;
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception && message.Contains("Expected_EventValidation")) { _exceptionLogs++; return; }
        if (type == LogType.Exception || type == LogType.Error) Fail(new Exception(message + "\n" + trace));
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup > _timeout) throw new TimeoutException("Event validation timed out");
            if (_step == 0)
            {
                _center = EventCenter.Instance;
                CheckLegacyAndScopes();
                CheckReleaseDuringDispatch();
                CheckExceptionIsolation();
                CheckControlLifecycle();
                CheckUILifecycle();
                BeginDeferredTest();
                _step = 1;
            }
            else if (_step == 1 && _c >= 0)
            {
                Check(_b > _a && _c > _b, "Nested A/B/C execute on successively later frames while paused");
                Check(_oldCalls == 0 && _newCalls == 1, "Queued events use current subscribers, not captured old delegates");
                ClearScopes();
                var remaining = Scope();
                remaining.Listen(GameEvent.UI_Message, args => { });
                UnityEngine.Object.Destroy(_center.gameObject);
                _step = 2;
            }
            else if (_step == 2 && _center == null)
            {
                ClearScopes();
                Check(UnityEngine.Object.FindObjectsOfType<EventCenter>().Length == 0, "Scope disposal never recreates a destroyed event center");
                Finish();
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static void CheckLegacyAndScopes()
    {
        int received = 0;
        Action<GameEventArgs> callback = args => { if (args.data is ValueArgs<int> data) received += data.value; };
        _center.AddListener(GameEvent.None, callback);
        _center.AddListener(GameEvent.Level_Start, null);
        Check(!_center.HasListener(GameEvent.None) && !_center.HasListener(GameEvent.Level_Start), "Ignore empty event/handler");
        var scope = Scope();
        scope.Listen(LoginEvent.OnLoginDone, callback); scope.Listen(LoginEvent.OnLoginDone, callback);
        _center.Dispatch(new GameEventArgs { eventType = LoginEvent.OnLoginDone, data = new ValueArgs<int>(2) });
        Check(received == 2 && scope.Count == 1, "Module event alias, legacy args, payload and deduplication");
        scope.Clear();
        _center.Dispatch(LoginEvent.OnLoginDone, new ValueArgs<int>(5));
        Check(received == 2 && !_center.HasListener(LoginEvent.OnLoginDone), "Clear removes the final subscription");
        scope.Listen(LoginEvent.OnLoginDone, callback);
        _center.Dispatch(LoginEvent.OnLoginDone, new ValueArgs<int>(3));
        scope.Dispose(); scope.Dispose();
        Check(received == 5 && scope.Count == 0, "Clear supports reuse; Dispose is idempotent");
        bool rejected = false;
        try { scope.Listen(LoginEvent.OnLoginDone, callback); } catch (ObjectDisposedException) { rejected = true; }
        Check(rejected, "Disposed scopes reject new subscriptions");
        _center.AddListener(GameEvent.UI_Message, callback);
        _center.Dispatch(GameEvent.UI_Message, new ValueArgs<int>(1));
        _center.RemoveListener(GameEvent.UI_Message, callback);
        Check(received == 6 && !_center.HasListener(GameEvent.UI_Message), "Existing AddListener/RemoveListener remain compatible");
    }

    private static void CheckReleaseDuringDispatch()
    {
        int received = 0;
        var first = Scope(); var later = Scope();
        first.Listen(GameEvent.Enemy_Die, args => later.Dispose());
        later.Listen(GameEvent.Enemy_Die, args => received++);
        _center.Dispatch(GameEvent.Enemy_Die);
        Check(received == 0, "Disposed listener in an existing dispatch snapshot is skipped");
        first.Dispose();
    }

    private static void CheckExceptionIsolation()
    {
        bool reached = false;
        var scope = Scope();
        scope.Listen(GameEvent.UI_Message, args => throw new InvalidOperationException("Expected_EventValidation"));
        scope.Listen(GameEvent.UI_Message, args => reached = true);
        _center.Dispatch(GameEvent.UI_Message);
        Check(reached && _exceptionLogs == 1, "One throwing listener is logged without blocking the next");
        scope.Dispose();
    }

    private static void CheckControlLifecycle()
    {
        var control = EventValidationControl.Instance;
        control.Initialize(); control.Initialize();
        _center.Dispatch(GameEvent.Level_Start);
        Check(control.model.value == 1, "Control initializes subscriptions once");
        control.ResetState(); control.ResetState();
        _center.Dispatch(GameEvent.Level_Start);
        Check(control.model.value == 1, "Control reset preserves one subscription");
        control.AddValue(4);
        Check(control.GetModel().value == 5, "Global Control supports direct cross-module method calls");
        control.Shutdown(); control.Shutdown();
        Check(!_center.HasListener(GameEvent.Level_Start), "Control shutdown releases subscriptions");
        control.Initialize();
        _center.Dispatch(GameEvent.Level_Start);
        Check(control.model.value == 6, "Control can initialize a fresh scope after shutdown");
        control.Shutdown();
        UnityEngine.Object.Destroy(control.gameObject);
    }

    private static void CheckUILifecycle()
    {
        var parent = new GameObject("UI parent");
        var view = new GameObject("Event UI probe").AddComponent<UIValidationProbe>();
        view.transform.SetParent(parent.transform);
        view.Open(); view.Open();
        _center.Dispatch(GameEvent.UI_Refresh);
        Check(view.received == 1 && view.first == 1, "UI registers in FirstOpen once");
        view.Close();
        _center.Dispatch(GameEvent.UI_Refresh);
        Check(view.received == 1, "UI ignores events during Close and recycle waiting");
        view.Open();
        parent.SetActive(false); _center.Dispatch(GameEvent.UI_Refresh);
        Check(view.received == 1, "UI ignores events while its parent is inactive");
        parent.SetActive(true); _center.Dispatch(GameEvent.UI_Refresh);
        Check(view.received == 2 && view.first == 1, "UI resumes without duplicate subscriptions");
        view.Dispose(); view.Dispose();
        Check(view.disposed == 1 && !_center.HasListener(GameEvent.UI_Refresh), "UI disposal releases its scope exactly once");
        var recursive = new GameObject("Reentrant UI probe").AddComponent<UIValidationProbe>();
        recursive.Open(); recursive.disposeOnClose = true; recursive.Dispose();
        Check(recursive.closed == 1 && recursive.disposed == 1 && !_center.HasListener(GameEvent.UI_Refresh),
            "Dispose requested again from Close does not recurse or clean up twice");
        var closeDispose = new GameObject("Close and dispose UI probe").AddComponent<UIValidationProbe>();
        closeDispose.Open(); closeDispose.disposeOnClose = true; closeDispose.Close();
        Check(closeDispose.closed == 1 && closeDispose.disposed == 1 && !_center.HasListener(GameEvent.UI_Refresh),
            "Close requesting Dispose runs its close callback only once");
        UnityEngine.Object.Destroy(parent);
    }

    private static void BeginDeferredTest()
    {
        Time.timeScale = 0;
        var chain = Scope();
        chain.Listen(GameEvent.War_Start, args =>
        {
            _a = Time.frameCount;
            _center.Dispatch(GameEvent.War_End);
            _center.Dispatch(GameEvent.War_Resume);
        });
        chain.Listen(GameEvent.War_End, args => { _b = Time.frameCount; _center.Dispatch(GameEvent.War_Pause); });
        chain.Listen(GameEvent.War_Pause, args => _c = Time.frameCount);
        var old = Scope(); old.Listen(GameEvent.War_Resume, args => _oldCalls++);
        _center.Dispatch(GameEvent.War_Start);
        Check(_b == -1 && _c == -1, "Nested dispatch is not reentrant");
        old.Dispose();
        Scope().Listen(GameEvent.War_Resume, args => _newCalls++);
    }

    private static void Check(bool passed, string label)
    {
        if (!passed) throw new Exception("FAILED: " + label);
        Results.Add("PASS: " + label); Debug.Log(Results[Results.Count - 1]);
    }

    private static void ClearScopes()
    {
        foreach (var scope in Scopes) scope.Dispose(); Scopes.Clear();
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
        Time.timeScale = 1;
        Directory.CreateDirectory("Validation");
        File.WriteAllLines("Validation/EventResult.txt", Results);
        Debug.Log("EVENT_VALIDATION_PASSED"); EditorApplication.Exit(0);
    }

    private static void Fail(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
        ClearScopes(); Time.timeScale = 1;
        Debug.LogException(error); EditorApplication.Exit(1);
    }
}
#endif
