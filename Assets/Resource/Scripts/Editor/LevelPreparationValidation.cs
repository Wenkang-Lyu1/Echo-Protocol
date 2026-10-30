#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Build + runtime regression in a disposable project, leaving the active editor alone.</summary>
public static class LevelPreparationValidation
{
    private static double _deadline;
    private static int _frames;
    private static bool _reloaded;
    private static string _previousRun;
    public static void RunBatch()
    {
        if (!Path.GetFullPath(Application.dataPath).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use an isolated project under the OS temporary directory.");
        try
        {
            LevelPreparationBuilder.Build();
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            _frames = 0;
            _reloaded = false;
            _deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
            EditorApplication.isPlaying = true;
        }
        catch (Exception ex) { Fail(ex); }
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) Fail(new Exception(message + "\n" + stack));
    }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Debug.Log("PREPARATION_CHECK: " + description);
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Preparation validation timed out.");
            var level = UnityEngine.Object.FindObjectOfType<LevelControl>();
            if (level == null || !level.IsInitialized || ++_frames < 5) return;
            if (_reloaded)
            {
                Check(level.model.runId != _previousRun && level.model.phase == LevelPhase.EchoPreparation,
                    "Reload with an initialized Frame creates a fresh preparation run");
                Check(UnityEngine.Object.FindObjectsOfType<PlayerControl>().Length == 1 &&
                    UnityEngine.Object.FindObjectsOfType<Camera>().Length == 1,
                    "Late initialization keeps exactly one player and camera");
                Check(level.PreparationEcho.GetModel().segments.Count == 0 &&
                    UnityEngine.Object.FindObjectsOfType<PreparationEnemy>().All(e => e.IsFrozen),
                    "Reload clears old recordings and freezes the new enemy set");
                Debug.Log("PREPARATION_VALIDATION_OK");
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                EditorApplication.Exit(0);
                return;
            }
            var echo = level.PreparationEcho;
            var data = echo.GetModel();
            var player = UnityEngine.Object.FindObjectOfType<PlayerControl>();
            var cameraParent = player.CameraPivot.parent;
            var enemies = UnityEngine.Object.FindObjectsOfType<PreparationEnemy>();
            var positions = enemies.Select(e => e.transform.position).ToArray();
            Check(UnityEngine.Object.FindObjectsOfType<MeshCollider>().All(c => c.sharedMesh != null), "Environment collision meshes resolve");
            Check(Physics.Raycast(player.transform.position + Vector3.up, Vector3.down, 2, ~(1 << 2)), "Spawn has supporting ground collision");
            Check(UnityEngine.Object.FindObjectOfType<TutorialControl>() == null && UnityEngine.Object.FindObjectOfType<WarControl>() == null,
                "Formal scene has a single LevelControl driver and no tutorial/war driver");
            Check(level.model.phase == LevelPhase.EchoPreparation && !string.IsNullOrEmpty(level.model.runId), "New run enters preparation");
            Check(data.cursor == 0 && data.EffectiveSpeed == 0 && data.segments.Count == 0, "Initial cursor, speed and segment reset");
            Check(enemies.Length == 4 && enemies.All(e => e.IsFrozen), "All four enemies frozen");
            Check(enemies.All(e => e.GetComponentsInChildren<Behaviour>().Where(b => b != e).All(b => !b.enabled)), "Enemy animation and autonomous components disabled");
            Check(level.model.enemyInstanceIds.Distinct().Count() == 4 && !level.model.bossSpawned, "Enemy identities and boss state initialized");
            Check(data.levelDuration == 240 && level.model.durationSeconds == 240, "Level 1 has a 240 second timeline independent of segment length");
            Check(data.echoLimit == level.Configuration.echoLimit, "Preparation reads the configured echo quota");
            // This suite stress-tests long/overlapping timelines; quota boundaries are checked separately.
            data.echoLimit = 1000;
            ValidateInputActions();
            level.SetPreparationCaptured(true);
            SetInput("MoveInput", Vector2.up);
            var bodyStart = player.transform.position;
            echo.Tick(.2f);
            Check(player.transform.position.z > bodyStart.z + .8f && data.cursor == 0, "Body moves while echo cursor stays stopped");
            var body = player.transform.position;
            Check(echo.ToggleControl(), "E creates controlled echo");
            Check(player.CameraPivot.parent != cameraParent, "Camera handed to controlled echo");
            SetInput("CursorTogglePressed", true); level.Tick(0);
            SetInput("CursorTogglePressed", false); level.Tick(1);
            Check(!level.Captured && Cursor.visible && data.cursor == 0 && data.fireEvents.Count == 0, "Right click releases mouse and stops recording input");
            SetInput("CursorTogglePressed", true); level.Tick(0);
            SetInput("CursorTogglePressed", false);
            Check(level.Captured && !Cursor.visible, "Right click recaptures mouse while gameplay actions were disabled");
            SetInput("PausePressed", true); level.Tick(0); SetInput("PausePressed", false);
            Check(level.Captured, "Escape no longer switches preparation mouse mode");
            SetInput("MoveInput", Vector2.up);
            echo.SetPreparationSpeed(.1f);
            SetInput("FireHeld", true);
            echo.Tick(1);
            var firstShot = data.fireEvents.Single();
            var shotViewport = Camera.main.WorldToViewportPoint(firstShot.muzzlePosition);
            Check(shotViewport.x > .5f && shotViewport.x < 1 && shotViewport.y > .38f && shotViewport.z > Camera.main.nearClipPlane,
                "Shot starts at a visible offset muzzle above the timeline rather than on the camera axis");
            CaptureShooting(UnityEngine.Object.FindObjectOfType<U_Level_MainView>(), firstShot);
            Check(Mathf.Abs(data.cursor - .1f) < .001f && Mathf.Abs(data.recordDuration - .1f) < .001f, "0.1x advances local cursor and duration");
            Check(Vector3.Distance(player.transform.position, body) < .001f, "Body stays in place while echo moves");
            var moving = GameObject.Find("Controlled echo");
            Check(moving != null && moving.transform.position.z > body.z + .4f && moving.transform.position.z < body.z + .6f, "Echo movement uses local speed");
            echo.SetPreparationSpeed(.3f); echo.Tick(1);
            Check(Mathf.Abs(data.cursor - .4f) < .001f, "0.3x local time");
            echo.SetPreparationSpeed(1); echo.Tick(.6f);
            SetInput("MoveInput", Vector2.zero);
            SetInput("FireHeld", false);
            Check(echo.ToggleControl() && data.segments.Count == 1 && !data.isRecording, "E saves independent segment and returns to body");
            Check(player.CameraPivot.parent == cameraParent && player.transform.position == body, "Camera and stationary body restored");
            var first = data.segments[0];
            Check(first.frames[0].time == 0 && first.frames[0].position == body && Mathf.Abs(first.duration - 1) < .001f, "Segment starts at body pose with relative sample times");
            Check(first.shots.Length == 3 && first.shots.All(shot => shot.fireDirection.sqrMagnitude > .99f), "Controlled shadow records accepted shots with original world origin and direction");
            Check(enemies.All(enemy => enemy.GetComponentsInChildren<Renderer>().All(renderer => renderer.sharedMaterial.color.r > renderer.sharedMaterial.color.b)), "Enemy shadows use warm color distinct from cyan player shadows");
            Check(echo.SeekPreparation(0) && player.transform.position == body, "Seeking preserves body location");
            Check(echo.ToggleControl(), "Create second segment at same timeline time");
            echo.Tick(.5f); echo.SaveSegment();
            Check(data.segments.Count == 2 && data.segments[0].id != data.segments[1].id && data.segments[0].startTime == data.segments[1].startTime,
                "Same start time creates two independent segments without overwriting");
            Check(first.frames[first.frames.Length - 1].time > .99f, "Later recording does not mutate first snapshot");
            var hud = UnityEngine.Object.FindObjectOfType<U_Level_MainView>();
            level.SetPreparationCaptured(false); hud.Refresh();
            Canvas.ForceUpdateCanvases();
            var rows = hud.GetComponentsInChildren<U_Level_SegmentSV>();
            Check(hud.GetComponentsInChildren<InputField>(true).Length == 0 && rows.Length == 2, "Timeline shows both recorded segments and has no time input field");
            foreach (var row in rows)
            {
                var rect = (RectTransform)row.transform;
                var viewport = (RectTransform)rect.parent.parent;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, rect);
                Check(rect.rect.width > 500 && rect.rect.height > 20 && bounds.max.y > viewport.rect.yMin && bounds.min.y < viewport.rect.yMax,
                    "Recorded lane has visible dimensions and intersects timeline viewport");
            }
            echo.SeekPreparation(0);
            Check(echo.TogglePreview(), "Timeline playback starts");
            level.Tick(.25f);
            Check(Mathf.Abs(data.cursor - .25f) < .001f && player.transform.position == body, "Preview advances while mouse is released and leaves body in place");
            var ghosts = UnityEngine.Object.FindObjectsOfType<PlayerEchoActor>().Where(actor => actor.name.StartsWith("Echo preview ")).ToArray();
            Check(ghosts.Length == 2 && ghosts.All(actor => actor.gameObject.activeInHierarchy), "Overlapping segments preview simultaneously");
            Check(ghosts.Sum(actor => actor.PresentedShotCount) == 1, "Preview presents the recorded shot once");
            Check(echo.TogglePreview() && !data.isPreviewPlaying, "Timeline pause works");
            echo.SeekPreparation(.3f);
            Check(ghosts.Sum(actor => actor.PresentedShotCount) == 1, "Scrubbing previews position without firing shots");
            hud.Refresh(); Canvas.ForceUpdateCanvases(); Capture(hud, player);
            echo.TogglePreview(); level.Tick(data.levelDuration);
            Check(!data.isPreviewPlaying && data.cursor == echo.PreviewEnd, "Preview stops at the 240 second level timeline end");
            Check(ghosts.Sum(actor => actor.PresentedShotCount) == 3 && enemies.All(enemy => enemy.IsFrozen) && level.model.killCount == 0,
                "Playback reproduces each recorded shot while enemies remain frozen and combat state unchanged");
            Check(echo.DeleteSegment(first.id) && data.segments.Count == 1, "Delete by unique segment ID");
            Check(!echo.SeekPreparation(240.01f) && echo.SeekPreparation(240) && !echo.ToggleControl(), "Timeline bounds reject recording past the level end");
            Check(!echo.SeekPreparation(float.NaN) && !echo.SeekPreparation(float.PositiveInfinity) && !echo.SeekPreparation(-1), "Invalid seek input rejected");
            Check(!echo.SetPreparationSpeed(2), "Unsupported speed rejected");
            echo.SeekPreparation(5); echo.ToggleControl(); echo.SetPreparationSpeed(.1f);
            echo.Tick(199);
            Check(data.isRecording && Mathf.Abs(data.recordDuration - 19.9f) < .002f, "20 second limit uses logical time at 0.1x");
            echo.Tick(2);
            Check(!data.isRecording && data.segments.Last().duration == 20 && data.cursor == 25, "Automatically saves exactly 20 logical seconds and returns");
            Check(Time.timeScale == 1 && level.model.combatElapsed == 0 && level.model.currentWave == 0, "Editing leaves global time, combat clock and waves untouched");
            for (int i = 0; i < enemies.Length; i++) Check(enemies[i].transform.position == positions[i], "Enemy remains at initial position " + i);
            level.SetPreparationCaptured(false); hud.Refresh();
            Check(hud.GetComponentsInChildren<U_Level_SegmentSV>().Length == data.segments.Count, "Timeline lanes refresh after deletion and new recording");
            var ruler = hud.GetComponentInChildren<U_Level_TimelineSV>().GetComponentInChildren<Slider>();
            Check(ruler.maxValue == 240, "Timeline ruler remains 240 seconds after recording and deletion");
            for (int i = 0; i < 13; i++)
            {
                echo.SeekPreparation(0); Check(echo.ToggleControl(), "Create independent twenty second segment " + i);
                echo.SetPreparationSpeed(1); echo.Tick(20);
            }
            Check(data.segments.Sum(segment => segment.duration) > 240 && data.segments.All(segment => segment.duration <= 20),
                "Overlapping segment durations can total more than 240 seconds; twenty seconds is a per-segment limit");
            echo.SeekPreparation(239); echo.ToggleControl(); echo.Tick(2);
            Check(!data.isRecording && data.cursor == 240 && data.segments.Last().duration == 1,
                "Final segment ends at the level boundary without extending the 240 second timeline");
            string oldId = level.model.runId;
            echo.SeekPreparation(0); echo.ToggleControl();
            level.RestartPreparation();
            Check(level.model.runId != oldId && data.segments.Count == 0 && data.cursor == 0 && !data.isRecording, "Restart creates new run and clears active recording and segments");
            Check(player.CameraPivot.parent == cameraParent && enemies.All(e => e.IsFrozen), "Restart restores camera then freezes enemies again");
            level.RestartPreparation();
            Check(data.cursor == 0 && data.segments.Count == 0, "Repeated reset is safe");
            _previousRun = level.model.runId;
            _reloaded = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene("Scene_Level1");
        }
        catch (Exception ex) { Fail(ex); }
    }
    private static void Capture(U_Level_MainView hud, PlayerControl player)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = Camera.main;
        var savedPosition = camera.transform.position;
        var savedRotation = camera.transform.rotation;
        camera.transform.position = new Vector3(-4, 2.5f, -8);
        camera.transform.LookAt(new Vector3(0, 1, 5));
        var canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .08f;
        var target = new RenderTexture(1600, 900, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var picture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); picture.Apply();
        Directory.CreateDirectory("Validation");
        File.WriteAllBytes("Validation/EchoTimeline.png", picture.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(picture); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
    }
    public struct FakeContext { }
    public sealed class FakeAction
    {
        public event Action<FakeContext> started, performed, canceled;
        public void Press() { started?.Invoke(default); performed?.Invoke(default); }
        public void Release() => canceled?.Invoke(default);
    }
    public sealed class FakeActions
    {
        public readonly FakeAction fire = new FakeAction();
        public FakeAction FindAction(string name, bool throwIfNotFound = false) => name == "Fire" ? fire : null;
        public void Enable() { }
        public void Disable() { }
    }
    public sealed class FakePlayerInput
    {
        public FakeActions actions { get; } = new FakeActions();
    }
    private static void ValidateInputActions()
    {
        var input = InputManager.Instance;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = typeof(InputManager).GetField("_playerInput", flags);
        var original = field.GetValue(input);
        var disable = typeof(InputManager).GetMethod("OnDisable", flags);
        var enable = typeof(InputManager).GetMethod("OnEnable", flags);
        disable.Invoke(input, null);
        try
        {
            var mapped = new FakePlayerInput();
            field.SetValue(input, mapped); enable.Invoke(input, null);
            mapped.actions.fire.Press();
            Check(input.FireHeld, "Configured Fire action remains held after started and performed with value-type callback");
            mapped.actions.fire.Release();
            Check(!input.FireHeld, "Configured Fire action clears only on canceled");
            disable.Invoke(input, null); enable.Invoke(input, null);
            mapped.actions.fire.Press();
            Check(input.FireHeld, "Input action subscriptions can be rebound without stale handlers");
        }
        finally { disable.Invoke(input, null); field.SetValue(input, original); enable.Invoke(input, null); }
    }
    private static void CaptureShooting(U_Level_MainView hud, EchoFireEvent shot)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        hud.Refresh();
        var camera = Camera.main;
        var canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .08f;
        var target = new RenderTexture(1600, 900, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var picture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); picture.Apply();
        var point = camera.WorldToViewportPoint(shot.muzzlePosition);
        var color = picture.GetPixel(Mathf.RoundToInt(point.x * 1600), Mathf.RoundToInt(point.y * 900));
        Directory.CreateDirectory("Validation");
        File.WriteAllBytes("Validation/EchoShooting.png", picture.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(picture); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        Check(color.r > .4f && color.g > .3f && color.r > color.b * 1.4f, "First-person render shows a visible warm muzzle flash outside HUD panels");
    }
    private static void SetInput(string name, object value) => typeof(InputManager).GetProperty(name).SetValue(InputManager.Instance, value);
    private static void Fail(Exception ex)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        Debug.LogError("PREPARATION_VALIDATION_FAILED: " + ex);
        EditorApplication.Exit(1);
    }
}
#endif
