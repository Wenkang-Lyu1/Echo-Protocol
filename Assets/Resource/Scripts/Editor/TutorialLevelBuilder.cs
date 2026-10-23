#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds an editable, serialized tutorial scene from the imported Kenney assets.</summary>
public static class TutorialLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/Scene_Level1.unity";
    private const string Kit = "Assets/Resource/Models/Environment/KenneySpaceStation/Prefabs/";
    private const string Generated = "Assets/Resource/Level1";
    private static readonly Color Cyan = new Color(.22f, .86f, 1);
    private static readonly Color Orange = new Color(1, .52f, .22f);
    private static Material _cyan, _orange, _dark;
    private static Transform _environment;

    [MenuItem("Tools/Echo Protocol/Level 1/Rebuild Tutorial Scene")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static void Build()
    {
        Directory.CreateDirectory(Generated);
        AssetDatabase.Refresh();
        var akane = AkanePreviewImporter.BuildStanding("Akane");
        var atsuko = AkanePreviewImporter.BuildStanding("Atsuko");
        _cyan = Material("GuideCyan", Cyan, true);
        _orange = Material("TrainingOrange", Orange, true);
        _dark = Material("Gunmetal", new Color(.06f, .10f, .14f), false);
        var echoMaterial = Material("Echo", new Color(.12f, .80f, 1, .22f), false, "EchoProtocol/TutorialEcho");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        var root = new GameObject("Level 1 - Echo Training Station");
        _environment = Node("Kenney Space Station", root.transform);
        var actors = Node("Actors", root.transform);
        var effects = Node("Transient Effects", root.transform);
        var logic = Node("Tutorial", root.transform).gameObject.AddComponent<TutorialControl>();

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.32f, .38f, .45f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(.025f, .055f, .09f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 24;
        RenderSettings.fogEndDistance = 65;
        var sun = Node("Station fill light", root.transform).gameObject.AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = .65f;
        sun.color = new Color(.78f, .88f, 1);
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50, -25, 0);
        RenderSettings.sun = sun;

        for (int x = 0; x < 6; x++) for (int z = 0; z < 8; z++)
        {
            Place((x == 2 || x == 3) ? "floor-panel" : "floor", new Vector3(-10 + x * 4, -.09f, -6 + z * 4), new Vector3(4, .18f, 4));
            var ceilingCenter = new Vector3(-10 + x * 4, 4.1f, -6 + z * 4);
            var ceiling = Place("floor", ceilingCenter, new Vector3(4, .18f, 4));
            ceiling.name = "Ceiling - Kenney floor";
            ceiling.transform.RotateAround(ceilingCenter, Vector3.right, 180);
        }
        for (int z = 0; z < 8; z++)
        {
            Place(z % 3 == 1 ? "wall-window" : "wall", new Vector3(-12, 1.8f, -6 + z * 4), new Vector3(4, 3.6f, .35f), 90);
            Place(z % 3 == 1 ? "wall-window" : "wall", new Vector3(12, 1.8f, -6 + z * 4), new Vector3(4, 3.6f, .35f), -90);
        }
        for (int x = 0; x < 6; x++)
        {
            Place("wall", new Vector3(-10 + x * 4, 1.8f, -8), new Vector3(4, 3.6f, .35f));
            Place(x == 2 || x == 3 ? "wall-window-banner" : "wall", new Vector3(-10 + x * 4, 1.8f, 24), new Vector3(4, 3.6f, .35f), 180);
        }
        // Entry vestibule has an unobstructed eight-metre central opening.
        foreach (float x in new[] { -10f, -6f, 6f, 10f })
            Place("wall-detail", new Vector3(x, 1.8f, 0), new Vector3(4, 3.6f, .3f));
        Cube("Entry lintel", new Vector3(0, 3.4f, 0), new Vector3(8, .4f, .5f), _dark, true);
        Cube("Entry light", new Vector3(0, 3.16f, -.28f), new Vector3(7.5f, .06f, .06f), _cyan);
        for (int z = -4; z <= 20; z += 8)
        {
            foreach (float x in new[] { -11.5f, 11.5f })
            {
                Place("wall-pillar", new Vector3(x, 1.8f, z), new Vector3(.45f, 3.6f, .5f));
                Cube("Light column", new Vector3(x * .99f, 2.2f, z - .28f), new Vector3(.07f, 1.6f, .06f), _cyan);
            }
            Cube("Overhead support", new Vector3(0, 3.8f, z), new Vector3(24, .25f, .35f), _dark);
            Cube("Ceiling light", new Vector3(0, 3.65f, z), new Vector3(7, .06f, .3f), _cyan);
            var light = Node("Ceiling fill", _environment).gameObject.AddComponent<Light>();
            light.transform.position = new Vector3(0, 3.2f, z);
            light.type = LightType.Point; light.color = new Color(.48f, .80f, 1);
            light.intensity = 1.2f; light.range = 12;
        }
        foreach (float x in new[] { -3.7f, 3.7f })
            Cube("Walkway guide", new Vector3(x, .015f, 7), new Vector3(.045f, .018f, 27), _cyan);
        for (int z = -4; z <= 20; z += 4)
            Cube("Path marker", new Vector3(0, .018f, z), new Vector3(.65f, .02f, .13f), _orange);
        Place("computer-wide", new Vector3(-9, .65f, -4), new Vector3(2.8f, 1.3f, 1.2f));
        Place("chair", new Vector3(-9, .5f, -5.7f), new Vector3(.8f, 1, .8f), 180);
        Place("computer-system", new Vector3(9, .8f, -4), new Vector3(2.2f, 1.6f, 1), 180);
        Place("container-wide", new Vector3(-8.5f, .55f, 7), new Vector3(2.8f, 1.1f, 1.3f));
        Place("container", new Vector3(8.6f, .65f, 12), new Vector3(1.7f, 1.3f, 1.4f));
        Place("container-tall", new Vector3(-9.5f, 1, 19), new Vector3(1.6f, 2, 1.5f));
        Place("table-display", new Vector3(9, .6f, 20), new Vector3(2.4f, 1.2f, 1.6f));
        Place("display-wall-wide", new Vector3(0, 2.3f, 23.5f), new Vector3(5, 1.5f, .2f), 180);

        var playerObject = Node("Akane - First Person", actors).gameObject;
        playerObject.layer = 2;
        playerObject.transform.position = new Vector3(0, .05f, -5.5f);
        var cc = playerObject.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.center = Vector3.up * .9f; cc.radius = .28f;
        cc.stepOffset = .25f; cc.skinWidth = .03f;
        var bodyFacing = Node("Akane Body", playerObject.transform);
        bodyFacing.localRotation = Quaternion.Euler(0, 180, 0);
        var body = Character(akane, bodyFacing);
        foreach (var t in body.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
        var cameraPivot = Node("CameraPivot", playerObject.transform);
        cameraPivot.localPosition = Vector3.up * 1.58f;
        var camera = cameraPivot.gameObject.AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.nearClipPlane = .04f; camera.farClipPlane = 100;
        camera.fieldOfView = 72; camera.cullingMask = ~(1 << 2);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = RenderSettings.fogColor;
        camera.gameObject.AddComponent<AudioListener>();
        var playerControl = playerObject.AddComponent<PlayerControl>();
        Assign(playerControl, "cameraPivot", cameraPivot);
        var gun = Node("Training Carbine", cameraPivot);
        gun.localPosition = new Vector3(.25f, -.24f, .48f);
        // A compact view model remains visible while the full Akane body is hidden locally.
        LocalCube(gun, "Receiver", Vector3.zero, new Vector3(.10f, .12f, .36f), _dark);
        LocalCube(gun, "Barrel", new Vector3(0, .02f, .25f), new Vector3(.045f, .045f, .25f), _dark);
        LocalCube(gun, "Energy strip", new Vector3(-.053f, .02f, 0), new Vector3(.01f, .035f, .22f), _cyan);
        LocalCube(gun, "Sight", new Vector3(0, .09f, .03f), new Vector3(.025f, .04f, .06f), _orange);
        LocalCube(gun, "Grip", new Vector3(0, -.11f, -.08f), new Vector3(.07f, .18f, .08f), _dark);
        var enemyPositions = new[] { new Vector3(-2, 0, 5), new Vector3(5, 0, 9), new Vector3(-5, 0, 16), new Vector3(3, 0, 20) };
        var enemies = new TutorialEnemyView[enemyPositions.Length];
        for (int i = 0; i < enemies.Length; i++)
        {
            var enemy = Node("Atsuko - Idle Target " + (i + 1), actors).gameObject;
            enemy.transform.position = enemyPositions[i];
            Character(atsuko, enemy.transform);
            var collider = enemy.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * .85f; collider.height = 1.7f; collider.radius = .42f;
            enemies[i] = enemy.AddComponent<TutorialEnemyView>();
            Cube("Target pad " + (i + 1), enemyPositions[i] + Vector3.up * .012f, new Vector3(1.8f, .02f, 1.8f), _orange);
        }
        // Save a normalized Akane visual for the echo, without player cameras or controllers.
        var echoSource = Node("Akane Echo Visual", root.transform).gameObject;
        var echoFacing = Node("Model Facing", echoSource.transform);
        echoFacing.localRotation = Quaternion.Euler(0, 180, 0);
        var echoCharacter = Character(akane, echoFacing);
        PrefabUtility.UnpackPrefabInstance(echoCharacter, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var echoPrefab = PrefabUtility.SaveAsPrefabAsset(echoSource, Generated + "/AkaneEchoVisual.prefab");
        if (echoPrefab == null) throw new InvalidOperationException("Could not save Akane echo prefab.");
        UnityEngine.Object.DestroyImmediate(echoSource);
        var hud = BuildHud(root.transform);
        Assign(logic, "player", playerControl); Assign(logic, "playerCamera", camera);
        Assign(logic, "echoPrefab", echoPrefab); Assign(logic, "effectsRoot", effects);
        Assign(logic, "shotMaterial", _orange); Assign(logic, "echoMaterial", echoMaterial);
        Assign(logic, "gun", gun); Assign(logic, "hud", hud);
        var serialized = new SerializedObject(logic);
        var array = serialized.FindProperty("enemies"); array.arraySize = enemies.Length;
        for (int i = 0; i < enemies.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        else scenes.First(s => s.path == ScenePath).enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("LEVEL1_BUILD_OK: " + ScenePath + "; 4 idle Atsuko; Akane first person; Kenney station.");
    }

    private static GameObject Character(GameObject prefab, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        var animation = go.GetComponent<Animation>();
        animation.clip.SampleAnimation(go, 0);
        var bounds = Bounds(go);
        go.transform.localScale *= 1.7f / bounds.size.y;
        bounds = Bounds(go);
        go.transform.position += parent.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        return go;
    }

    private static GameObject Place(string name, Vector3 center, Vector3 size, float yaw = 0)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + name + ".prefab");
        if (prefab == null) throw new InvalidOperationException("Missing Kenney prefab " + name);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _environment);
        var bounds = Bounds(go);
        go.transform.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
        bounds = Bounds(go);
        go.transform.position = center - bounds.center;
        go.transform.RotateAround(center, Vector3.up, yaw);
        foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        return go;
    }

    private static Bounds Bounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        if (bounds.size.y < .0001f) throw new InvalidOperationException("Empty model bounds " + go.name);
        return bounds;
    }

    private static Transform Node(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }
    private static void LocalCube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    private static void Cube(string name, Vector3 position, Vector3 size, Material material, bool collision = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(_environment, false); go.transform.position = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    private static Material Material(string name, Color color, bool emission, string shader = "Standard")
    {
        string path = Generated + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find(shader)) { color = color };
        if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .65f); }
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static U_Tutorial_MainView BuildHud(Transform parent)
    {
        var canvas = new GameObject("U_Tutorial_MainView", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.transform.SetParent(parent, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        var hud = canvas.AddComponent<U_Tutorial_MainView>();
        Panel(canvas.transform, "Briefing", new Vector2(.025f, .70f), new Vector2(.28f, .965f));
        Label(canvas.transform, "Title", "E / P   ·   ECHO TRAINING", new Vector2(.04f, .90f), new Vector2(.28f, .945f), 27, Cyan);
        Label(canvas.transform, "Subtitle", "LEVEL 01   /   TUTORIAL", new Vector2(.04f, .85f), new Vector2(.28f, .89f), 17, Color.white);
        var objectives = Label(canvas.transform, "Objectives", "", new Vector2(.04f, .715f), new Vector2(.275f, .84f), 19, Color.white);
        Panel(canvas.transform, "Echo panel", new Vector2(.025f, .03f), new Vector2(.44f, .14f));
        var echo = Label(canvas.transform, "EchoStatus", "", new Vector2(.04f, .045f), new Vector2(.43f, .13f), 20, Cyan);
        Panel(canvas.transform, "Target panel", new Vector2(.80f, .89f), new Vector2(.975f, .965f));
        var targets = Label(canvas.transform, "Targets", "", new Vector2(.82f, .90f), new Vector2(.97f, .955f), 22, Orange);
        Label(canvas.transform, "Controls", "WASD Move   /   Mouse Look   /   LMB Shoot\nE Echo   ·   ESC Release cursor   ·   R Reset", new Vector2(.58f, .035f), new Vector2(.975f, .12f), 18, Color.white).alignment = TextAnchor.MiddleRight;
        var message = Label(canvas.transform, "Message", "", new Vector2(.30f, .55f), new Vector2(.70f, .73f), 32, Cyan);
        message.alignment = TextAnchor.MiddleCenter;
        var crosshair = new GameObject("Crosshair", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        crosshair.transform.SetParent(canvas.transform, false);
        crosshair.rectTransform.anchorMin = crosshair.rectTransform.anchorMax = new Vector2(.5f, .5f);
        crosshair.rectTransform.anchoredPosition = Vector2.zero; crosshair.rectTransform.sizeDelta = new Vector2(5, 5);
        crosshair.raycastTarget = false;
        Assign(hud, "objectives", objectives); Assign(hud, "echoStatus", echo);
        Assign(hud, "targetStatus", targets); Assign(hud, "message", message); Assign(hud, "crosshair", crosshair);
        return hud;
    }
    private static void Panel(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false); Rect(image.rectTransform, min, max);
        image.color = new Color(.02f, .06f, .09f, .85f); image.raycastTarget = false;
    }
    private static Text Label(Transform parent, string name, string content, Vector2 min, Vector2 max, int size, Color color)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(parent, false); Rect(text.rectTransform, min, max);
        text.text = content; text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resource/Fonts/NotoSansSC.ttf");
        text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false; return text;
    }
    private static void Rect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
#endif
