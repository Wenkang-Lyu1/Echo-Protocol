#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SpaceStationSetup
{
    private const string Root = "Assets/Resource/Models/Environment/KenneySpaceStation";

    [MenuItem("Tools/Echo Protocol/Space Station/Rebuild Prefabs")]
    public static void Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Prefabs");
        AssetDatabase.Refresh();
        var shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Built-in Standard shader is required.");
        var material = CreateMaterial("SpaceStation", "colormap", shader);
        CreateMaterial("SpaceStationVariationA", "variation-a", shader);
        var paths = Directory.GetFiles(Root + "/Models", "*.fbx").OrderBy(p => p).ToArray();
        int rendererCount = 0;
        foreach (var file in paths)
        {
            string path = file.Replace('\\', '/');
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidOperationException("Model import failed: " + path);
            foreach (var imported in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(imported), material);
            importer.SaveAndReimport();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = Path.GetFileNameWithoutExtension(path);
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("No renderer: " + path);
                foreach (var renderer in renderers)
                {
                    var mesh = renderer.GetComponent<MeshFilter>();
                    if (mesh == null || mesh.sharedMesh == null || mesh.sharedMesh.vertexCount == 0)
                        throw new InvalidOperationException("Empty mesh: " + path);
                    renderer.sharedMaterials = Enumerable.Repeat(material, mesh.sharedMesh.subMeshCount).ToArray();
                    rendererCount++;
                }
                if (PrefabUtility.SaveAsPrefabAsset(instance, Root + "/Prefabs/" + instance.name + ".prefab") == null)
                    throw new InvalidOperationException("Could not save prefab: " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        AssetDatabase.SaveAssets();
        foreach (var path in Directory.GetFiles(Root + "/Prefabs", "*.prefab"))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.sharedMaterials.Any(m => m == null || m.mainTexture == null || m.shader != shader))
                    throw new InvalidOperationException("Invalid prefab material: " + path);
        }
        File.WriteAllText(Root + "/Documentation/ImportValidation.txt",
            "Unity: " + Application.unityVersion + "\nModels: " + paths.Length +
            "\nPrefabs: " + Directory.GetFiles(Root + "/Prefabs", "*.prefab").Length +
            "\nMesh renderers: " + rendererCount +
            "\nMaterials: Standard shader, shared palette textures.\nValidation: all meshes nonempty; all prefab materials and textures resolved.\n");
        AssetDatabase.Refresh();
        Debug.Log("Space Station import validated: " + paths.Length + " models.");
    }

    private static Material CreateMaterial(string name, string textureName, Shader shader)
    {
        string texturePath = Root + "/Textures/" + textureName + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null) throw new InvalidOperationException("Missing texture: " + texturePath);
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(shader) { name = name, mainTexture = texture, enableInstancing = true };
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.15f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
#endif
