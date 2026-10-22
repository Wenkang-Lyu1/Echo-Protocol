#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Batch smoke test in a disposable project; never run over an unsaved working scene.</summary>
public static class LoginValidation
{
    private static int _step;
    private static double _until;
    private static double _timeout;
    private static U_Login_MainView _login;
    private static UIValidationProbe _probe;
    private static float _savedVolume;
    private static bool _savedFullScreen;
    private static bool _hadVolume;
    private static bool _hadFullScreen;

    public static void RunBatch()
    {
        if (!Path.GetFullPath(Application.dataPath).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("RunBatch is restricted to a temporary validation project.");
        try
        {
            _hadVolume = PlayerPrefs.HasKey("EchoProtocol.MasterVolume");
            _hadFullScreen = PlayerPrefs.HasKey("EchoProtocol.FullScreen");
            _savedVolume = PlayerPrefs.GetFloat("EchoProtocol.MasterVolume",1f);
            _savedFullScreen = PlayerPrefs.GetInt("EchoProtocol.FullScreen",0) != 0;
            // The asset builder preserves the current scene by creating previews
            // additively; Unity requires the initial temporary scene to be saved.
            var bootstrap = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(bootstrap, "Assets/ValidationBootstrap.unity");
            LoginAssetBuilder.BuildAll();
            // An empty temporary target checks routing without implementing the main city.
            var target = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(target, "Assets/Start_MainCity.unity");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Resource/Scenes/LoginPreview.unity",true),
                new EditorBuildSettingsScene("Assets/Start_MainCity.unity",true)
            };
            EditorSceneManager.OpenScene("Assets/Resource/Scenes/LoginPreview.unity");
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            _step = 0;
            _timeout = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Fail(error); }
    }

    private static void OnLog(string message,string trace,LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
            Fail(new Exception(message + "\n" + trace));
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup > _timeout) throw new TimeoutException("Login validation timed out");
            if (_step == 0)
            {
                _login = ViewRegistry.Get<U_Login_MainView>();
                if (_login == null || _login.State != ViewState.Showing) return;
                Check(_login.transform.Find("Title").GetComponent<Text>().text == "Echo-Protocol", "Static title");
                Capture(_login.GetComponent<Canvas>(),"Login.png");
                _login.transform.Find("SettingsButton").GetComponent<Button>().onClick.Invoke();
                Check(LoginControl.Instance.model.settingsOpen,"Settings open");
                _login.GetComponentInChildren<Slider>(true).value = .35f;
                Check(Mathf.Abs(AudioListener.volume-.35f)<.01f,"Volume control");
                Capture(_login.GetComponent<Canvas>(),"Settings.png");
                _login.transform.Find("Settings/Window/CloseButton").GetComponent<Button>().onClick.Invoke();
                Check(!LoginControl.Instance.model.settingsOpen,"Settings close");
                _probe = new GameObject("Lifecycle probe").AddComponent<UIValidationProbe>();
                _probe.Open(); _probe.Open(); _probe.Close(); _probe.Close(); _probe.Open();
                Check(_probe.initialized==1 && _probe.widgets==1 && _probe.first==1 && _probe.opened==2 && _probe.closed==1,"Lifecycle idempotence and reuse");
                _login.Close();
                _until = EditorApplication.timeSinceStartup + 1;
                _step = 1;
            }
            else if (_step == 1 && EditorApplication.timeSinceStartup >= _until)
            {
                LoginControl.Instance.OpenLogin();
                Check(ReferenceEquals(_login,ViewRegistry.Get<U_Login_MainView>()),"Reopen reuses instance");
                _login.Close(); _probe.Close();
                Time.timeScale=0;
                _until=EditorApplication.timeSinceStartup+10.5;
                _step=2;
            }
            else if (_step == 2 && EditorApplication.timeSinceStartup >= _until)
            {
                Check(_login==null && _probe==null,"Unscaled ten-second recycle");
                LoginControl.Instance.OpenLogin();
                _login=ViewRegistry.Get<U_Login_MainView>();
                Check(_login!=null && _login.State==ViewState.Showing,"Recreate after recycle");
                var config = new SerializedObject(LoginControl.Instance);
                var scene = config.FindProperty("gameplayScene");
                scene.stringValue = "Missing_Validation_Scene"; config.ApplyModifiedPropertiesWithoutUndo();
                _login.transform.Find("StartButton").GetComponent<Button>().onClick.Invoke();
                Check(!LoginControl.Instance.model.isLoading && _login.State == ViewState.Showing &&
                    LoginControl.Instance.model.status.Length > 0,"Missing scene retains login with message");
                scene.stringValue = "Start_MainCity"; config.ApplyModifiedPropertiesWithoutUndo();
                _login.transform.Find("StartButton").GetComponent<Button>().onClick.Invoke();
                Check(LoginControl.Instance.model.isLoading,"Start button loads scene");
                _step=3;
            }
            else if (_step == 3)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Start_MainCity" ||
                    GameManager.Instance.State != GameState.Playing) return;
                Check(_login.State == ViewState.Ready, "Login closes after scene load");
                LoginControl.Instance.OpenLogin();
                Check(ViewRegistry.Get<U_Login_MainView>().State == ViewState.Showing, "Return to login");
                Finish();
            }
        }
        catch(Exception error) { Fail(error); }
    }

    private static void Check(bool passed,string name)
    { if(!passed) throw new Exception("FAILED: "+name); Debug.Log("PASS: "+name); }

    private static void Capture(Canvas canvas,string filename)
    {
        var camera=new GameObject("Capture camera",typeof(Camera)).GetComponent<Camera>();
        camera.transform.position=new Vector3(0,0,-20);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.03f,.07f,.1f);
        camera.orthographic=true; camera.orthographicSize=540; camera.cullingMask=1<<5;
        var rt=new RenderTexture(1920,1080,24);
        camera.targetTexture=rt;
        foreach(var t in canvas.GetComponentsInChildren<RectTransform>(true)) t.gameObject.layer=5;
        var mode=canvas.renderMode; var oldCamera=canvas.worldCamera;
        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10;
        var scaler=canvas.GetComponent<CanvasScaler>(); scaler.enabled=false; scaler.enabled=true;
        Canvas.ForceUpdateCanvases(); camera.Render();
        Save(rt,filename);
        canvas.renderMode=mode; canvas.worldCamera=oldCamera;
        camera.targetTexture=null; UnityEngine.Object.Destroy(camera.gameObject); rt.Release(); UnityEngine.Object.Destroy(rt);
    }

    private static void Save(RenderTexture rt,string filename)
    {
        var old=RenderTexture.active; RenderTexture.active=rt;
        var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply();
        Directory.CreateDirectory("Validation"); File.WriteAllBytes("Validation/"+filename,image.EncodeToPNG());
        UnityEngine.Object.Destroy(image); RenderTexture.active=old;
    }

    private static void RestorePrefs()
    {
        if(_hadVolume) PlayerPrefs.SetFloat("EchoProtocol.MasterVolume",_savedVolume); else PlayerPrefs.DeleteKey("EchoProtocol.MasterVolume");
        if(_hadFullScreen) PlayerPrefs.SetInt("EchoProtocol.FullScreen",_savedFullScreen?1:0); else PlayerPrefs.DeleteKey("EchoProtocol.FullScreen");
        PlayerPrefs.Save();
    }
    private static void Finish()
    {
        EditorApplication.update-=Tick; Application.logMessageReceived-=OnLog;
        RestorePrefs(); Debug.Log("LOGIN_VALIDATION_PASSED");
        File.WriteAllText("Validation/Result.txt","PASS: lifecycle, settings, unscaled recycle, scene loading, return to login");
        EditorApplication.Exit(0);
    }
    private static void Fail(Exception error)
    {
        Application.logMessageReceived-=OnLog; EditorApplication.update-=Tick;
        RestorePrefs(); Debug.LogException(error); EditorApplication.Exit(1);
    }
}
#endif
