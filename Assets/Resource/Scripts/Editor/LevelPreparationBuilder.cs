#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class LevelPreparationBuilder
{
    public const string ScenePath = "Assets/Scenes/Scene_Level1.unity";
    private const string HudPath = "Assets/Resource/Resources/UI/Level/U_Level_MainView.prefab";
    [MenuItem("Tools/Echo Protocol/Level 1/Rebuild Preparation Scene")]
    public static void BuildMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
    }

    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(TutorialLevelBuilder.ScenePath);
        var tutorial = Object.FindObjectOfType<TutorialControl>();
        var logic = tutorial.gameObject;
        Object.DestroyImmediate(tutorial);
        var oldHud = Object.FindObjectOfType<U_Tutorial_MainView>();
        Object.DestroyImmediate(oldHud.gameObject);
        var player = Object.FindObjectOfType<PlayerControl>();
        var gun = player.CameraPivot.Find("Training Carbine");
        if (gun != null) Object.DestroyImmediate(gun.gameObject);
        var enemies = Object.FindObjectsOfType<TutorialEnemyView>().Select(view =>
        {
            var go = view.gameObject;
            Object.DestroyImmediate(view);
            go.name = go.name.Replace("Idle Target", "Preparation Enemy");
            return go.AddComponent<PreparationEnemy>();
        }).ToArray();
        logic.name = "Level - Echo Preparation";
        logic.transform.root.name = "Level 1 - Echo Preparation";
        var echoObject = new GameObject("Echo");
        echoObject.transform.SetParent(logic.transform, false);
        var echo = echoObject.AddComponent<EchoControl>();
        var level = logic.AddComponent<LevelControl>();
        var effects = logic.transform.root.Find("Transient Effects");
        var config = new PreparationConfig { player = player, echo = echo, effectsRoot = effects,
            echoVisual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resource/Level1/AkaneEchoVisual.prefab"),
            shadowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resource/Level1/Echo.mat"),
            enemies = enemies, hud = BuildHud(logic.transform.root) };
        typeof(LevelControl).GetField("preparation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(level, config);
        var serialized = new SerializedObject(level);
        serialized.FindProperty("usePreparation").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(logic.transform.root, false);
        }
        // Save under a new path; the tutorial and its GUID remain intact.
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("PREPARATION_BUILD_OK: " + ScenePath);
    }

    private static U_Level_MainView BuildHud(Transform parent)
    {
        var canvas = new GameObject("U_Level_MainView", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        var hud = canvas.AddComponent<U_Level_MainView>();
        var panel = Box(canvas.transform, "Preparation panel", new Vector2(.02f, .06f), new Vector2(.34f, .95f));
        panel.GetComponent<Image>().color = new Color(.025f, .06f, .09f, .94f);
        Label(panel, "Title", "LEVEL 01 / 回声准备", .89f, .98f, 27);
        Label(panel, "Help", "WASD 移动 · 鼠标转向\nE 创建 / 保存影子 · T 切换流速\nESC 切换鼠标 · R 重开准备", .76f, .89f, 18);
        Set(hud, "status", Label(panel, "Status", "回声时间 0.00s   流速 0.0x\n本段 0.00 / 20s   已保存 0 段", .65f, .76f, 20));
        var control = Button(panel, "Control", "创建并操控影子（E）", new Vector2(.04f, .57f), new Vector2(.96f, .64f));
        Set(hud, "controlButton", control); Set(hud, "controlLabel", control.GetComponentInChildren<Text>());
        Set(hud, "speed01", Button(panel, "Speed01", "0.1x", new Vector2(.04f, .49f), new Vector2(.33f, .55f)));
        Set(hud, "speed03", Button(panel, "Speed03", "0.3x", new Vector2(.35f, .49f), new Vector2(.64f, .55f)));
        Set(hud, "speed1", Button(panel, "Speed1", "1x", new Vector2(.66f, .49f), new Vector2(.96f, .55f)));
        var inputRoot = Box(panel, "TimeInput", new Vector2(.04f, .40f), new Vector2(.64f, .47f));
        var input = inputRoot.gameObject.AddComponent<InputField>();
        var inputText = Label(inputRoot, "Value", "0", 0, 1, 20);
        inputText.rectTransform.anchorMin = new Vector2(.05f, 0);
        inputText.rectTransform.anchorMax = new Vector2(.95f, 1);
        input.textComponent = inputText;
        input.contentType = InputField.ContentType.DecimalNumber;
        input.text = "0";
        Set(hud, "timeInput", input);
        Set(hud, "seekButton", Button(panel, "Seek", "跳转（秒）", new Vector2(.66f, .40f), new Vector2(.96f, .47f)));
        Label(panel, "Segments heading", "已保存片段 · 时间跳转后新增", .34f, .40f, 18);
        var viewport = Box(panel, "Segment viewport", new Vector2(.04f, .08f), new Vector2(.96f, .34f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.spacing = 6;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var row = Box(content, "Segment template", Vector2.zero, Vector2.one);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
        var rowView = row.gameObject.AddComponent<U_Level_SegmentSV>();
        var rowLabel = Label(row, "Segment time", "0.00s → 0.00s", 0, 1, 16);
        rowLabel.rectTransform.anchorMax = new Vector2(.73f, 1);
        Set(rowView, "label", rowLabel);
        Set(rowView, "deleteButton", Button(row, "Delete", "删除", new Vector2(.75f, .08f), new Vector2(.98f, .92f)));
        row.gameObject.SetActive(false);
        Set(hud, "rowTemplate", rowView); Set(hud, "rowsRoot", content);
        Set(hud, "resumeButton", Button(panel, "Resume", "返回操控 / 捕获鼠标", new Vector2(.04f, .01f), new Vector2(.96f, .07f)));
        Directory.CreateDirectory(Path.GetDirectoryName(HudPath));
        PrefabUtility.SaveAsPrefabAsset(canvas, HudPath);
        canvas.transform.SetParent(parent, false);
        return hud;
    }

    private static RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.GetComponent<Image>().color = new Color(.10f, .19f, .24f, .95f);
        return rect;
    }
    private static Text Label(Transform parent, string name, string value, float min, float max, int size)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.rectTransform.anchorMin = new Vector2(.04f, min);
        label.rectTransform.anchorMax = new Vector2(.96f, max);
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resource/Fonts/NotoSansSC.ttf");
        label.text = value; label.fontSize = size; label.color = new Color(.7f, .95f, 1);
        label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
        return label;
    }
    private static Button Button(Transform parent, string name, string value, Vector2 min, Vector2 max)
    {
        var root = Box(parent, name, min, max);
        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        Label(root, "Label", value, 0, 1, 18).alignment = TextAnchor.MiddleCenter;
        return button;
    }
    private static void Set(Object target, string name, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
