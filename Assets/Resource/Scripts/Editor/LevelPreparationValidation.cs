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
            level.SetPreparationCaptured(true);
            SetInput("MoveInput", Vector2.up);
            var bodyStart = player.transform.position;
            echo.Tick(.2f);
            Check(player.transform.position.z > bodyStart.z + .8f && data.cursor == 0, "Body moves while echo cursor stays stopped");
            var body = player.transform.position;
            Check(echo.ToggleControl(), "E creates controlled echo");
            Check(player.CameraPivot.parent != cameraParent, "Camera handed to controlled echo");
            echo.SetPreparationSpeed(.1f);
            echo.Tick(1);
            Check(Mathf.Abs(data.cursor - .1f) < .001f && Mathf.Abs(data.recordDuration - .1f) < .001f, "0.1x advances local cursor and duration");
            Check(Vector3.Distance(player.transform.position, body) < .001f, "Body stays in place while echo moves");
            var moving = GameObject.Find("Controlled echo");
            Check(moving != null && moving.transform.position.z > body.z + .4f && moving.transform.position.z < body.z + .6f, "Echo movement uses local speed");
            echo.SetPreparationSpeed(.3f); echo.Tick(1);
            Check(Mathf.Abs(data.cursor - .4f) < .001f, "0.3x local time");
            echo.SetPreparationSpeed(1); echo.Tick(.6f);
            SetInput("MoveInput", Vector2.zero);
            Check(echo.ToggleControl() && data.segments.Count == 1 && !data.isRecording, "E saves independent segment and returns to body");
            Check(player.CameraPivot.parent == cameraParent && player.transform.position == body, "Camera and stationary body restored");
            var first = data.segments[0];
            Check(first.frames[0].time == 0 && first.frames[0].position == body && Mathf.Abs(first.duration - 1) < .001f, "Segment starts at body pose with relative sample times");
            Check(echo.SeekPreparation(0) && player.transform.position == body, "Seeking preserves body location");
            Check(echo.ToggleControl(), "Create second segment at same timeline time");
            echo.Tick(.5f); echo.SaveSegment();
            Check(data.segments.Count == 2 && data.segments[0].id != data.segments[1].id && data.segments[0].startTime == data.segments[1].startTime,
                "Same start time creates two independent segments without overwriting");
            Check(first.frames[first.frames.Length - 1].time > .99f, "Later recording does not mutate first snapshot");
            Check(echo.DeleteSegment(first.id) && data.segments.Count == 1, "Delete by unique segment ID");
            Check(!echo.SeekPreparation(float.NaN) && !echo.SeekPreparation(float.PositiveInfinity) && !echo.SeekPreparation(-1), "Invalid seek input rejected");
            Check(!echo.SetPreparationSpeed(2), "Unsupported speed rejected");
            echo.SeekPreparation(5); echo.ToggleControl(); echo.SetPreparationSpeed(.1f);
            echo.Tick(199);
            Check(data.isRecording && Mathf.Abs(data.recordDuration - 19.9f) < .002f, "20 second limit uses logical time at 0.1x");
            echo.Tick(2);
            Check(!data.isRecording && data.segments.Last().duration == 20 && data.cursor == 25, "Automatically saves exactly 20 logical seconds and returns");
            Check(Time.timeScale == 1 && level.model.combatElapsed == 0 && level.model.currentWave == 0, "Editing leaves global time, combat clock and waves untouched");
            for (int i = 0; i < enemies.Length; i++) Check(enemies[i].transform.position == positions[i], "Enemy remains at initial position " + i);
            var hud = UnityEngine.Object.FindObjectOfType<U_Level_MainView>();
            level.SetPreparationCaptured(false); hud.Refresh();
            Check(hud.GetComponentsInChildren<InputField>().Length == 1 && hud.GetComponentsInChildren<U_Level_SegmentSV>().Length == data.segments.Count,
                "UGUI timeline editor and dynamic segment rows present");
            string oldId = level.model.runId;
            echo.ToggleControl();
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
