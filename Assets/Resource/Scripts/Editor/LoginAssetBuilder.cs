#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates editable UGUI assets in the editor, never during runtime.</summary>
public static class LoginAssetBuilder
{
    public const string LoginPath = "Assets/Resource/Resources/UI/Login/U_Login_MainView.prefab";

    private static readonly Color Ink = new Color(0.035f, 0.08f, 0.12f);
    private static readonly Color Cyan = new Color(0.45f, 0.88f, 0.91f);
    private static readonly Color Orange = new Color(1f, 0.60f, 0.31f);
    private static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/Resource/Fonts/NotoSansSC.ttf");

    [MenuItem("Echo Protocol/Create Missing Login Assets")]
    public static void BuildAll()
    {
        Directory.CreateDirectory("Assets/Resource/Resources/UI/Login");
        Directory.CreateDirectory("Assets/Resource/Scenes");
        Directory.CreateDirectory("Assets/Resource/UIArt/Login");
        AssetDatabase.Refresh();
        var character = AkanePreviewImporter.Build();
        if (!File.Exists(LoginPath)) BuildLogin(character);

        if (!File.Exists("Assets/Resource/Scenes/LoginPreview.unity")) BuildPreview();
        AssetDatabase.SaveAssets();
        Debug.Log("LOGIN_ASSETS_READY");
    }

    private static void BuildLogin(GameObject character)
    {
        var root = CanvasRoot("U_Login_MainView");
        var view = root.AddComponent<U_Login_MainView>();
        var bg = Image(root.transform, "Bg", Vector2.zero, Vector2.one, Color.white);
        bg.sprite = Background();
        bg.raycastTarget = true;

        Label(root.transform, "Brand", "E / P     —     PROJECT ECHO", new Vector2(.065f,.88f), new Vector2(.45f,.92f), 20, Cyan);
        Label(root.transform, "Section", "01   /   CONNECTION ESTABLISHED", new Vector2(.065f,.76f), new Vector2(.48f,.80f), 17, Cyan);
        Label(root.transform, "Title", "Echo-Protocol", new Vector2(.06f,.62f), new Vector2(.63f,.76f), 77, Color.white);
        Label(root.transform, "Subtitle", "ECHO PROTOCOL", new Vector2(.065f,.56f), new Vector2(.45f,.62f), 26, new Color(.76f,.84f,.87f));
        Image(root.transform, "TitleAccent", new Vector2(.065f,.52f), new Vector2(.11f,.524f), Orange);
        Label(root.transform, "Tagline", "Every echo is a new beginning.", new Vector2(.065f,.44f), new Vector2(.46f,.49f), 20, new Color(.56f,.69f,.75f));

        var raw = new GameObject("Character", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
        raw.transform.SetParent(root.transform, false);
        Rect(raw.rectTransform, new Vector2(.50f,.065f), new Vector2(.98f,.93f));
        raw.raycastTarget = false;
        var preview = root.AddComponent<UIModelPreview>();
        Assign(preview, "modelPrefab", character);
        Assign(preview, "output", raw);
        Label(root.transform, "CharacterIndex", "AKANE  /  01", new Vector2(.76f,.10f), new Vector2(.94f,.14f), 18, Cyan);

        var start = Button(root.transform, "StartButton", "Start Game     →", new Vector2(.065f,.285f), new Vector2(.315f,.365f), Orange, Ink, 28);
        var quit = Button(root.transform, "QuitButton", "Quit Game", new Vector2(.065f,.18f), new Vector2(.315f,.25f), new Color(.12f,.20f,.25f), Color.white, 24);
        var settings = Button(root.transform, "SettingsButton", "≡", new Vector2(.93f,.88f), new Vector2(.97f,.95f), new Color(.15f,.25f,.30f), Cyan, 36);
        settings.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Label(root.transform, "Status", "", new Vector2(.065f,.12f), new Vector2(.55f,.17f), 17, Orange);
        Label(root.transform, "Footer", "ECHO-PROTOCOL  /  MAIN TERMINAL", new Vector2(.065f,.04f), new Vector2(.45f,.08f), 15, new Color(.42f,.57f,.63f));
        Label(root.transform, "FooterRight", "SYSTEM ONLINE   ●", new Vector2(.80f,.04f), new Vector2(.97f,.08f), 15, Cyan);

        var shade = Image(root.transform, "Settings", Vector2.zero, Vector2.one, new Color(0,0,0,.72f));
        shade.raycastTarget = true;
        var panel = Image(shade.transform, "Window", new Vector2(.30f,.25f), new Vector2(.70f,.75f), new Color(.055f,.12f,.17f));
        Image(panel.transform, "Accent", new Vector2(0,.985f), Vector2.one, Orange);
        Label(panel.transform, "Title", "Settings", new Vector2(.08f,.77f), new Vector2(.8f,.94f), 32, Color.white);
        var close = Button(panel.transform, "CloseButton", "×", new Vector2(.86f,.80f), new Vector2(.96f,.95f), Color.clear, Color.white, 32);
        Label(panel.transform, "VolumeLabel", "Volume", new Vector2(.08f,.58f), new Vector2(.40f,.70f), 23, Cyan);
        var slider = Image(panel.transform, "VolumeSlider", new Vector2(.10f,.45f), new Vector2(.90f,.51f), new Color(.2f,.3f,.35f)).gameObject.AddComponent<Slider>();
        slider.GetComponent<Image>().raycastTarget = true;
        var handle = Image(slider.transform, "Handle", Vector2.zero, Vector2.one, Orange);
        handle.rectTransform.sizeDelta = new Vector2(24f, 16f);
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        var toggle = Image(panel.transform, "FullScreenToggle", new Vector2(.10f,.21f), new Vector2(.90f,.34f), Color.clear).gameObject.AddComponent<Toggle>();
        var box = Image(toggle.transform, "Box", new Vector2(0,.12f), new Vector2(.08f,.88f), new Color(.2f,.3f,.35f));
        var tick = Image(box.transform, "Check", new Vector2(.18f,.18f), new Vector2(.82f,.82f), Orange);
        Label(toggle.transform, "Label", "Fullscreen", new Vector2(.14f,0), Vector2.one, 23, Color.white);
        toggle.targetGraphic = box;
        toggle.graphic = tick;
        toggle.GetComponent<Image>().raycastTarget = true;
        shade.gameObject.SetActive(false);

        Assign(view, "startButton", start); Assign(view, "quitButton", quit);
        Assign(view, "settingsButton", settings); Assign(view, "closeSettingsButton", close);
        Assign(view, "settingsRoot", shade.gameObject); Assign(view, "volumeSlider", slider);
        Assign(view, "fullScreenToggle", toggle); Assign(view, "statusText", root.transform.Find("Status").GetComponent<Text>());
        Assign(view, "modelPreview", preview);
        PrefabUtility.SaveAsPrefabAsset(root, LoginPath);
        Object.DestroyImmediate(root);
    }

    private static void BuildPreview()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var frame = new GameObject("Frame", typeof(ControlRegistry), typeof(GameLauncher));
        var input = new GameObject("Input", typeof(InputManager)); input.transform.SetParent(frame.transform);
        var events = new GameObject("Event", typeof(EventCenter)); events.transform.SetParent(frame.transform);
        var flow = new GameObject("Flow", typeof(GameManager)); flow.transform.SetParent(frame.transform);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        EditorSceneManager.SaveScene(scene, "Assets/Resource/Scenes/LoginPreview.unity");
        EditorSceneManager.CloseScene(scene, true);
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
    }

    private static GameObject CanvasRoot(string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        scaler.matchWidthOrHeight = .5f;
        return root;
    }

    private static Image Image(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent,false); Rect(image.rectTransform,min,max);
        image.color = color; image.raycastTarget = false;
        return image;
    }

    private static Text Label(Transform parent, string name, string text, Vector2 min, Vector2 max, int size, Color color)
    {
        var label = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent,false); Rect(label.rectTransform,min,max);
        label.font = Font; label.fontSize = size; label.text = text; label.color = color;
        label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
        return label;
    }

    private static Button Button(Transform parent, string name, string text, Vector2 min, Vector2 max, Color bg, Color fg, int size)
    {
        var image = Image(parent,name,min,max,bg); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(1.1f,1.1f,1.1f); colors.pressedColor = new Color(.75f,.85f,.9f); button.colors = colors;
        Label(button.transform,"Label",text,new Vector2(.04f,0),new Vector2(.96f,1),size,fg).alignment = TextAnchor.MiddleCenter;
        return button;
    }

    private static void Rect(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }

    private static void Assign(Object owner, string field, Object value)
    { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }

    private static Sprite Background()
    {
        const string path = "Assets/Resource/UIArt/Login/LoginBackground.png";
        if (!File.Exists(path))
        {
            const int width = 1920, height = 1080;
            var texture = new Texture2D(width,height,TextureFormat.RGB24,false);
            var pixels = new Color32[width*height];
            for (int y=0;y<height;y++) for (int x=0;x<width;x++)
            {
                float u=x/(float)width, v=y/(float)height;
                float dx=(u-.76f)*1.78f, dy=v-.5f, d=Mathf.Sqrt(dx*dx+dy*dy);
                float glow=Mathf.Exp(-d*d*5f);
                var c=Color.Lerp(new Color(.025f,.055f,.085f),new Color(.11f,.25f,.30f),glow);
                if (u>.52f && (x%96==0 || y%96==0)) c+=new Color(.025f,.04f,.045f);
                if (u>.50f && (Mathf.Abs(d-.34f)<.0015f || Mathf.Abs(d-.43f)<.0007f)) c+=new Color(.07f,.18f,.20f);
                if (u>.48f && Mathf.Abs(v-(u*.45f-.08f))<.0012f) c+=new Color(.12f,.21f,.22f);
                pixels[y*width+x]=c;
            }
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.mipmapEnabled=false;
            importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
