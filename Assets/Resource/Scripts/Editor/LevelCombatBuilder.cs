#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class LevelCombatBuilder
{
    public static NestSpawnBinding[] BindNests(Transform sceneRoot, LevelConfig config, Vector3[] authoredPositions)
    {
        config.Validate();
        var bindings = new NestSpawnBinding[config.nests.Length];
        var root = new GameObject("Configured nests").transform;
        root.SetParent(sceneRoot, false);
        for (int i = 0; i < bindings.Length; i++)
        {
            var nestRoot = new GameObject(config.nests[i].nestId).transform;
            nestRoot.SetParent(root, false);
            // Seed positions from the existing layout; creators can then place markers in Scene view.
            var points = new Transform[2];
            for (int p = 0; p < points.Length; p++)
            {
                points[p] = new GameObject("Spawn " + (p + 1)).transform;
                points[p].SetParent(nestRoot, false);
                points[p].position = authoredPositions[(i * points.Length + p) % authoredPositions.Length];
            }
            bindings[i] = new NestSpawnBinding { nestId = config.nests[i].nestId, spawnPoints = points };
        }
        return bindings;
    }

    public static CombatConfig Configure(Transform sceneRoot, PreparationEnemy[] enemies)
    {
        foreach (var enemy in enemies)
        {
            var actor = enemy.gameObject.AddComponent<EnemyActor>();
            var agent = actor.GetComponent<NavMeshAgent>();
            agent.enabled = false; agent.radius = .35f; agent.height = 1.8f;
        }
        BakeNavigation(sceneRoot);
        var left = new GameObject("Boss spawn left").transform; left.SetParent(sceneRoot, false); left.position = new Vector3(-7, 0, 20);
        var right = new GameObject("Boss spawn right").transform; right.SetParent(sceneRoot, false); right.position = new Vector3(7, 0, 20);
        return new CombatConfig { ordinaryPrefab = BuildOrdinary(enemies[0]), bossPrefab = BuildBoss(), bossSpawnPoints = new[] { left, right },
            playerShotMaterial = ShotMaterial("PlayerBullet", new Color(.25f, .85f, 1)),
            enemyShotMaterial = ShotMaterial("EnemyBullet", new Color(1, .3f, .12f)) };
    }

    private static EnemyActor BuildOrdinary(PreparationEnemy source)
    {
        const string path = "Assets/Resource/Level1/OrdinaryEnemy.prefab";
        var go = Object.Instantiate(source.gameObject);
        Object.DestroyImmediate(go.GetComponent<PreparationEnemy>());
        go.name = "Atsuko - Combat Enemy"; go.transform.position = Vector3.zero;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<EnemyActor>();
    }

    private static Material ShotMaterial(string name, Color color)
    {
        string path = "Assets/Resource/Level1/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Unlit/Color")) { name = name, color = color };
        AssetDatabase.CreateAsset(material, path); return material;
    }

    private static void BakeNavigation(Transform sceneRoot)
    {
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(sceneRoot.Find("Kenney Space Station"), ~0, NavMeshCollectGeometry.PhysicsColliders,
            0, new List<NavMeshBuildMarkup>(), sources);
        var settings = NavMesh.GetSettingsByIndex(0);
        settings.agentRadius = .4f; settings.agentHeight = 1.8f; settings.agentClimb = .3f;
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(new Vector3(0, 2, 8), new Vector3(30, 12, 40)), Vector3.zero, Quaternion.identity);
        if (data == null) throw new System.InvalidOperationException("Could not bake level navigation.");
        const string path = "Assets/Resource/Level1/Level1Navigation.asset";
        var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if (existing == null) AssetDatabase.CreateAsset(data, path);
        else { EditorUtility.CopySerialized(data, existing); Object.DestroyImmediate(data); data = existing; }
        var owner = new GameObject("Level navigation"); owner.transform.SetParent(sceneRoot, false);
        var navigation = owner.AddComponent<LevelNavigation>();
        var serialized = new SerializedObject(navigation);
        serialized.FindProperty("data").objectReferenceValue = data; serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static DestructionBossActor BuildBoss()
    {
        const string path = "Assets/Resource/Level1/DestructionBoss.prefab";
        var source = AkanePreviewImporter.BuildCombat("Yoshimi", "Formation_Idle");
        var root = new GameObject("Yoshimi - Destruction Boss");
        var facing = new GameObject("Model facing").transform; facing.SetParent(root.transform, false);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, facing);
        visual.GetComponent<Animation>().clip.SampleAnimation(visual, 0);
        var renderers = visual.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        visual.transform.localScale *= 1.9f / bounds.size.y;
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        visual.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        var capsule = root.AddComponent<CapsuleCollider>(); capsule.height = 1.9f; capsule.radius = .4f; capsule.center = Vector3.up * .95f;
        var actor = root.AddComponent<DestructionBossActor>();
        actor.GetComponent<NavMeshAgent>().enabled = false;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<DestructionBossActor>();
    }
}
#endif
