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
    public const string ConfigPath = "Assets/Resource/Resources/Config/Level_1.asset";
    private const string HudPath = "Assets/Resource/Resources/UI/Level/U_Level_MainView.prefab";
    [MenuItem("Tools/Echo Protocol/Level 1/Bind Configuration To Existing Scene")]
    public static void BindConfigurationMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BindConfiguration();
    }

    public static void BindConfiguration()
    {
        var levelConfig = AssetDatabase.LoadAssetAtPath<LevelConfig>(ConfigPath);
        if (levelConfig == null) throw new System.InvalidOperationException("先完成关卡配置：" + ConfigPath);
        levelConfig.Validate();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var level = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelControl>(true)).Single();
        var field = typeof(LevelControl).GetField("preparation", BindingFlags.Instance | BindingFlags.NonPublic);
        var config = (PreparationConfig)field.GetValue(level);
        config.levelConfig = levelConfig;
        if (config.combat.nests == null || config.combat.nests.Length == 0)
            config.combat.nests = LevelCombatBuilder.BindNests(level.transform.root, levelConfig,
                config.enemies.Select(enemy => enemy.transform.position).ToArray());
        EditorUtility.SetDirty(level);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LEVEL_CONFIG_BOUND: " + ScenePath);
    }

    [MenuItem("Tools/Echo Protocol/Level 1/Rebuild Preparation Scene")]
    public static void BuildMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
    }

    public static void Build()
    {
        var levelConfig = AssetDatabase.LoadAssetAtPath<LevelConfig>(ConfigPath);
        if (levelConfig == null) throw new System.InvalidOperationException("先完成关卡配置，再构建关卡：" + ConfigPath);
        levelConfig.Validate();
        var scene = EditorSceneManager.OpenScene(TutorialLevelBuilder.ScenePath);
        var tutorial = Object.FindObjectOfType<TutorialControl>();
        var logic = tutorial.gameObject;
        Object.DestroyImmediate(tutorial);
        var oldHud = Object.FindObjectOfType<U_Tutorial_MainView>();
        Object.DestroyImmediate(oldHud.gameObject);
        var player = Object.FindObjectOfType<PlayerControl>();
        var gun = player.CameraPivot.Find("Training Carbine");
        if (gun != null) gun.localPosition = new Vector3(.25f, -.08f, .48f);
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
        var config = new PreparationConfig { levelConfig = levelConfig, player = player, echo = echo, effectsRoot = effects,
            echoVisual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resource/Level1/AkaneEchoVisual.prefab"),
            shadowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resource/Level1/Echo.mat"),
            enemyShadowMaterial = EnemyMaterial(), enemies = enemies, combat = LevelCombatBuilder.Configure(logic.transform.root, enemies), hud = BuildHud(logic.transform.root) };
        config.combat.nests = LevelCombatBuilder.BindNests(logic.transform.root, levelConfig, enemies.Select(enemy => enemy.transform.position).ToArray());
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

    private static Material EnemyMaterial()
    {
        const string path = "Assets/Resource/Level1/EnemyEcho.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resource/Level1/Echo.mat"));
        material.name = "EnemyEcho";
        material.color = new Color(1, .25f, .12f, .32f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static U_Level_MainView BuildHud(Transform parent)
    {
        var canvas = new GameObject("U_Level_MainView", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        var hud = canvas.AddComponent<U_Level_MainView>();
        AtLabel(canvas.transform, "Crosshair", "+", new Vector2(.49f, .49f), new Vector2(.51f, .51f), 22).alignment = TextAnchor.MiddleCenter;
        var panel = Box(canvas.transform, "Preparation panel", new Vector2(.025f, .46f), new Vector2(.29f, .97f));
        panel.GetComponent<Image>().color = new Color(.025f, .06f, .09f, .94f);
        Set(hud, "stageTitle", Label(panel, "Title", "LEVEL 01 / ECHO PREPARATION", .85f, .98f, 25));
        Set(hud, "preparationHelp", Label(panel, "Preparation help", "WASD Move · Mouse Look\nE Record / Save · T Speed\nLMB Fire · RMB Cursor\nEnter Start · R Restart", .60f, .85f, 18).gameObject);
        Set(hud, "combatHelp", Label(panel, "Combat help", "WASD Move · Mouse Look\nLMB Fire · RMB Cursor\nESC Pause / Resume · R Restart", .60f, .85f, 18).gameObject);
        Set(hud, "status", Label(panel, "Status", "Echo Time 0.00s   Speed 0.0x\nClip 0.00 / 20s   Saved 0 Clips", .43f, .60f, 19));
        var control = Button(panel, "Control", "Create Echo (E)", new Vector2(.04f, .33f), new Vector2(.96f, .42f));
        Set(hud, "controlButton", control); Set(hud, "controlLabel", control.GetComponentInChildren<Text>());
        Set(hud, "speed01", Button(panel, "Speed01", "0.1x", new Vector2(.04f, .21f), new Vector2(.33f, .30f)));
        Set(hud, "speed03", Button(panel, "Speed03", "0.3x", new Vector2(.35f, .21f), new Vector2(.64f, .30f)));
        Set(hud, "speed1", Button(panel, "Speed1", "1x", new Vector2(.66f, .21f), new Vector2(.96f, .30f)));
        Set(hud, "resumeButton", Button(panel, "Resume", "Resume / Capture Mouse", new Vector2(.04f, .05f), new Vector2(.96f, .16f)));

        Set(hud, "startButton", Button(canvas.transform, "Start combat", "Start Combat (Enter)", new Vector2(.025f, .385f), new Vector2(.29f, .445f)));
        var result = Box(canvas.transform, "Result panel", new Vector2(.33f, .39f), new Vector2(.72f, .76f));
        result.GetComponent<Image>().color = new Color(.025f, .06f, .09f, .98f);
        Set(hud, "resultPanel", result.gameObject);
        Set(hud, "resultText", Label(result, "Result", "Level Complete", .36f, .95f, 28));
        Set(hud, "restartButton", Button(result, "Restart", "Restart", new Vector2(.05f, .06f), new Vector2(.47f, .27f)));
        Set(hud, "exitButton", Button(result, "Exit", "Back to Login", new Vector2(.53f, .06f), new Vector2(.95f, .27f)));
        result.gameObject.SetActive(false);

        var timelineRoot = Box(canvas.transform, "Timeline", new Vector2(.025f, .025f), new Vector2(.975f, .37f));
        timelineRoot.GetComponent<Image>().color = new Color(.025f, .06f, .09f, .96f);
        var timeline = timelineRoot.gameObject.AddComponent<U_Level_TimelineSV>();
        Set(hud, "timeline", timeline);
        AtLabel(timelineRoot, "Title", "Echo Timeline", new Vector2(.015f, .81f), new Vector2(.19f, .98f), 24);
        var play = Button(timelineRoot, "Play preview", "Play Preview", new Vector2(.2f, .83f), new Vector2(.32f, .96f));
        Set(timeline, "playButton", play); Set(timeline, "playLabel", play.GetComponentInChildren<Text>());
        Set(timeline, "currentTime", AtLabel(timelineRoot, "Current time", "0.00s / 240s", new Vector2(.34f, .81f), new Vector2(.53f, .98f), 20));
        AtLabel(timelineRoot, "Help", "Scrub to preview · Play all echoes · Delete only", new Vector2(.55f, .81f), new Vector2(.99f, .98f), 17);
        var sliderRoot = Box(timelineRoot, "Time ruler", new Vector2(.2f, .69f), new Vector2(.88f, .75f));
        var slider = sliderRoot.gameObject.AddComponent<Slider>();
        var handle = Box(sliderRoot, "Playhead handle", Vector2.zero, new Vector2(0, 1));
        handle.sizeDelta = new Vector2(5, 12);
        handle.GetComponent<Image>().color = new Color(1, .8f, .3f);
        slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 0; slider.maxValue = 240;
        Set(timeline, "scrubber", slider);
        var serializedTimeline = new SerializedObject(timeline);
        var ticks = serializedTimeline.FindProperty("ticks"); ticks.arraySize = 6;
        for (int i = 0; i < 6; i++)
        {
            float x = .2f + .68f * i / 5;
            var tick = AtLabel(timelineRoot, "Tick " + i, (i * 48) + "s", new Vector2(x - .03f, .56f), new Vector2(x + .03f, .68f), 16);
            tick.alignment = TextAnchor.MiddleCenter;
            ticks.GetArrayElementAtIndex(i).objectReferenceValue = tick;
        }
        serializedTimeline.ApplyModifiedPropertiesWithoutUndo();
        var viewport = Box(timelineRoot, "Tracks viewport", new Vector2(0, .06f), new Vector2(1, .54f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = new GameObject("Tracks", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, 100);
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.scrollSensitivity = 25; scroll.movementType = ScrollRect.MovementType.Clamped;
        var row = Box(content, "Segment template", new Vector2(0, 1), Vector2.one);
        row.sizeDelta = new Vector2(0, 44);
        var rowView = row.gameObject.AddComponent<U_Level_SegmentSV>();
        Set(rowView, "label", AtLabel(row, "Segment name", "Echo 1", new Vector2(.01f, 0), new Vector2(.19f, 1), 16));
        var track = Box(row, "Track", new Vector2(.2f, .08f), new Vector2(.88f, .92f));
        track.GetComponent<Image>().color = new Color(.06f, .12f, .16f);
        var bar = Box(track, "Recorded segment", new Vector2(0, .12f), new Vector2(.1f, .88f));
        bar.GetComponent<Image>().color = new Color(.13f, .69f, .88f);
        Set(rowView, "segmentBar", bar);
        Set(rowView, "deleteButton", Button(row, "Delete", "Delete", new Vector2(.9f, .1f), new Vector2(.99f, .9f)));
        row.gameObject.SetActive(false);
        Set(timeline, "rowTemplate", rowView); Set(timeline, "rowsRoot", content);
        var playhead = Box(content, "Playhead", new Vector2(.2f, 0), new Vector2(.2f, 1));
        playhead.sizeDelta = new Vector2(2, 0);
        playhead.GetComponent<Image>().color = new Color(1, .8f, .3f);
        playhead.GetComponent<Image>().raycastTarget = false;
        Set(timeline, "playhead", playhead);
        var empty = AtLabel(timelineRoot, "Empty", "No clips · Press E to record an echo", new Vector2(.2f, .14f), new Vector2(.88f, .45f), 20);
        Set(timeline, "emptyMessage", empty);
        Directory.CreateDirectory(Path.GetDirectoryName(HudPath));
        PrefabUtility.SaveAsPrefabAsset(canvas, HudPath);
        canvas.transform.SetParent(parent, false);
        return hud;
    }

    private static Text AtLabel(Transform parent, string name, string value, Vector2 min, Vector2 max, int size)
    {
        var label = Label(parent, name, value, min.y, max.y, size);
        label.rectTransform.anchorMin = min; label.rectTransform.anchorMax = max;
        return label;
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
