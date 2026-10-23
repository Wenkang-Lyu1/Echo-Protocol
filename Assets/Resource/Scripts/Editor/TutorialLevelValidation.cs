#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Runtime smoke test restricted to a disposable project. Also generates the final scene.</summary>
public static class TutorialLevelValidation
{
    private static int _step;
    private static double _until, _timeout;
    private static TutorialControl _control;
    private static Vector3 _initial;
    public static void RunBatch()
    {
        if (!Path.GetFullPath(Application.dataPath).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use a disposable project under the OS temporary directory.");
        try
        {
            TutorialLevelBuilder.Build();
            EditorSceneManager.OpenScene(TutorialLevelBuilder.ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            _step = 0; _timeout = EditorApplication.timeSinceStartup + 90;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
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
        Debug.Log("LEVEL1_CHECK: " + description);
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup > _timeout) throw new TimeoutException("Level 1 validation timed out.");
            if (_step == 0)
            {
                _control = UnityEngine.Object.FindObjectOfType<TutorialControl>();
                if (_control == null || !_control.IsInitialized) return;
                _control.SetCaptured(true);
                Check(_control.GetModel().totalEnemies == 4, "Four Atsuko targets bound");
                Check(UnityEngine.Object.FindObjectsOfType<Camera>().Length == 1, "One first person camera");
                Check(PlayerControl.Instance.gameObject.layer == 2, "Player collider excluded from all shot raycasts");
                foreach (var animation in UnityEngine.Object.FindObjectsOfType<Animation>())
                    Check(animation.clip != null && animation.clip.name == "Normal_Idle" && animation.isPlaying, "Standing idle animation plays on " + animation.name);
                Check(UnityEngine.Object.FindObjectsOfType<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null)), "Materials resolve");
                _initial = PlayerControl.Instance.transform.position;
                _until = EditorApplication.timeSinceStartup + .5;
                _step = 1;
            }
            else if (_step == 1 && EditorApplication.timeSinceStartup >= _until)
            {
                Check(PlayerControl.Instance.GetComponent<CharacterController>().isGrounded, "Player grounded on Kenney floor");
                Check(Mathf.Abs(PlayerControl.Instance.transform.position.y - _initial.y) < .2f, "Floor supports player");
                typeof(InputManager).GetProperty("MoveInput").SetValue(InputManager.Instance, Vector2.up);
                PlayerControl.Instance.Tick(.2f);
                Check(PlayerControl.Instance.transform.position.z > _initial.z + .9f, "Forward movement follows input and delta time");
                typeof(InputManager).GetProperty("MoveInput").SetValue(InputManager.Instance, Vector2.zero);
                var enemy = UnityEngine.Object.FindObjectsOfType<TutorialEnemyView>().OrderBy(e => e.transform.position.z).First();
                Camera.main.transform.LookAt(enemy.transform.position + Vector3.up * .95f);
                Physics.SyncTransforms();
                Check(_control.Fire(), "Primary fire accepted");
                Check(Mathf.Approximately(enemy.Hp, 40), "Hitscan applies 20 damage to target");
                Check(!_control.Fire(), "Fire interval enforced");
                _until = EditorApplication.timeSinceStartup + .25;
                _step = 2;
            }
            else if (_step == 2 && EditorApplication.timeSinceStartup >= _until)
            {
                Check(_control.CreateEcho(), "E action creates Akane echo from recording");
                Check(!_control.CreateEcho(), "Echo cooldown enforced");
                Check(_control.GetModel().activeEchoes == 1, "Echo count updates");
                _until = EditorApplication.timeSinceStartup + 2;
                _step = 3;
            }
            else if (_step == 3 && EditorApplication.timeSinceStartup >= _until)
            {
                Check(_control.GetModel().activeEchoes == 0, "Echo expires and cleans up");
                var first = UnityEngine.Object.FindObjectsOfType<TutorialEnemyView>().OrderBy(e => e.transform.position.z).First();
                Check(Mathf.Approximately(first.Hp, 20), "Echo replays the recorded shot and applies damage once");
                foreach (var target in UnityEngine.Object.FindObjectsOfType<TutorialEnemyView>())
                { target.ApplyDamage(100); Check(target.IsDead && !target.GetComponent<Collider>().enabled, "Defeated target disables collision"); }
                Check(_control.GetModel().defeated == 4, "Target progress reaches four");
                _control.RestartTrial();
                Check(_control.GetModel().defeated == 0 && _control.GetModel().activeEchoes == 0, "R action clears progress and echoes");
                Check(UnityEngine.Object.FindObjectsOfType<TutorialEnemyView>().All(e => e.Hp == 60 && e.GetComponent<Collider>().enabled), "R action restores targets");
                _control.SetCaptured(false);
                Check(!_control.Fire() && !_control.CreateEcho(), "Released cursor blocks shooting and echo creation");
                _control.SetCaptured(true);
                Capture("Level1");
                _control.Tick(.1f);
                Check(_control.CreateEcho(), "Fresh recording creates a visible echo after reset");
                UnityEngine.Object.FindObjectOfType<U_Tutorial_MainView>().Refresh();
                Camera.main.transform.position = _initial + new Vector3(-3, 1.8f, -1.5f);
                Camera.main.transform.LookAt(_initial + Vector3.up);
                Capture("Level1-Echo");
                Camera.main.transform.position = new Vector3(-2, 1.6f, 2.5f);
                Camera.main.transform.LookAt(new Vector3(-2, .9f, 5));
                Capture("Level1-Enemy");
                Directory.CreateDirectory("Validation");
                File.WriteAllText("Validation/Level1.txt", "PASS: Unity " + Application.unityVersion + "\nScene built; four Atsuko Normal_Idle targets; Akane first-person camera; valid materials; player grounded; hitscan damage and cooldown; echo snapshot/deployment/cooldown/expiry; target defeat; reset; cursor capture.\n");
                EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }
        catch (Exception ex) { Fail(ex); }
    }
    private static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Directory.CreateDirectory("Validation");
        var camera = Camera.main;
        var canvas = UnityEngine.Object.FindObjectOfType<U_Tutorial_MainView>().GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .08f;
        Canvas.ForceUpdateCanvases();
        var target = new RenderTexture(1600, 900, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes("Validation/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
    }
    private static void Fail(Exception ex)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
        Directory.CreateDirectory("Validation"); File.WriteAllText("Validation/Level1-Failure.txt", ex.ToString());
        Debug.LogException(ex); EditorApplication.Exit(1);
    }
}
#endif
