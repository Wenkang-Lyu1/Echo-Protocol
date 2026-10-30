#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Focused offline conversion of the documented Akane GLB. Not a general glTF importer.</summary>
public sealed class AkanePreviewImporter
{
    private string Folder = "Assets/Resource/Models/Akane/Generated";
    private string _character = "Akane";
    private string _idle = "Cafe_Idle";

    public static GameObject BuildStanding(string character)
    {
        string folder = "Assets/Resource/Models/" + character + "/Tutorial";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + character + ".prefab");
        if (existing != null) return existing;
        Directory.CreateDirectory(folder);
        return new AkanePreviewImporter { Folder = folder, _character = character, _idle = "Normal_Idle" }.Import();
    }

    public static GameObject BuildCombat(string character, string idle)
    {
        string folder = "Assets/Resource/Models/" + character + "/Combat";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + character + ".prefab");
        if (existing != null) return existing;
        Directory.CreateDirectory(folder);
        return new AkanePreviewImporter { Folder = folder, _character = character, _idle = idle }.Import();
    }
    private JObject _gltf;
    private byte[] _bytes;
    private int _binary;
    private Transform[] _nodes;

    public static GameObject Build()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resource/Models/Akane/Generated/Akane.prefab");
        if (existing != null) return existing;
        Directory.CreateDirectory("Assets/Resource/Models/Akane/Generated");
        return new AkanePreviewImporter().Import();
    }

    private GameObject Import()
    {
        _bytes = File.ReadAllBytes("Assets/Resource/Models/" + _character + "/" + _character + ".glb");
        if (BitConverter.ToUInt32(_bytes, 0) != 0x46546C67 || BitConverter.ToInt32(_bytes, 4) != 2)
            throw new InvalidDataException("Expected glTF 2.0 GLB");
        int length = BitConverter.ToInt32(_bytes, 12);
        _gltf = JObject.Parse(System.Text.Encoding.UTF8.GetString(_bytes, 20, length));
        _binary = 20 + length + 8;
        var materials = ImportMaterials();
        var root = new GameObject(_character);
        var nodes = (JArray)_gltf["nodes"];
        _nodes = nodes.Select((n, i) => new GameObject((string)n["name"] ?? "Node" + i).transform).ToArray();
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            _nodes[i].SetParent(root.transform, false);
            _nodes[i].localPosition = Vector(node["translation"], Vector3.zero, true);
            _nodes[i].localScale = Vector(node["scale"], Vector3.one, false);
            if (node["rotation"] is JArray q)
                _nodes[i].localRotation = new Quaternion(-(float)q[0], -(float)q[1], (float)q[2], (float)q[3]);
        }
        for (int i = 0; i < nodes.Count; i++)
            foreach (var child in nodes[i]["children"] ?? new JArray())
                _nodes[(int)child].SetParent(_nodes[i], false);

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node["mesh"] == null) continue;
            var source = _gltf["meshes"][(int)node["mesh"]];
            var parts = (JArray)source["primitives"];
            var attributes = parts[0]["attributes"];
            var mesh = new Mesh { name = (string)source["name"], indexFormat = IndexFormat.UInt32 };
            mesh.vertices = Vectors((int)attributes["POSITION"], true);
            mesh.normals = Vectors((int)attributes["NORMAL"], true);
            var uv = Read((int)attributes["TEXCOORD_0"]);
            mesh.uv = Enumerable.Range(0, uv.Length / 2).Select(v => new Vector2(uv[v * 2], 1f - uv[v * 2 + 1])).ToArray();
            mesh.subMeshCount = parts.Count;
            for (int p = 0; p < parts.Count; p++)
            {
                if (parts[p]["attributes"]["POSITION"].Value<int>() != attributes["POSITION"].Value<int>())
                    throw new NotSupportedException("Akane importer expects shared primitive vertex buffers");
                var indices = Read((int)parts[p]["indices"]).Select(v => (int)v).ToArray();
                for (int t = 0; t < indices.Length; t += 3)
                { int old = indices[t]; indices[t] = indices[t + 2]; indices[t + 2] = old; }
                mesh.SetTriangles(indices, p);
            }
            var nodeMaterials = parts.Select(p => materials[(int)p["material"]]).ToArray();
            if (node["skin"] != null)
            {
                var skin = _gltf["skins"][(int)node["skin"]];
                var joints = Read((int)attributes["JOINTS_0"]);
                var weights = Read((int)attributes["WEIGHTS_0"]);
                mesh.boneWeights = Enumerable.Range(0, mesh.vertexCount).Select(v => new BoneWeight
                {
                    boneIndex0 = (int)joints[v * 4], boneIndex1 = (int)joints[v * 4 + 1],
                    boneIndex2 = (int)joints[v * 4 + 2], boneIndex3 = (int)joints[v * 4 + 3],
                    weight0 = weights[v * 4], weight1 = weights[v * 4 + 1],
                    weight2 = weights[v * 4 + 2], weight3 = weights[v * 4 + 3]
                }).ToArray();
                var matrices = Read((int)skin["inverseBindMatrices"]);
                mesh.bindposes = Enumerable.Range(0, matrices.Length / 16).Select(m =>
                {
                    var matrix = new Matrix4x4();
                    for (int c = 0; c < 4; c++) for (int r = 0; r < 4; r++)
                        matrix[r, c] = matrices[m * 16 + c * 4 + r] * ((r == 2) != (c == 2) ? -1f : 1f);
                    return matrix;
                }).ToArray();
                var renderer = _nodes[i].gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = nodeMaterials;
                renderer.bones = skin["joints"].Select(j => _nodes[(int)j]).ToArray();
                renderer.updateWhenOffscreen = true;
            }
            else
            {
                _nodes[i].gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                _nodes[i].gameObject.AddComponent<MeshRenderer>().sharedMaterials = nodeMaterials;
            }
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "/" + mesh.name + ".asset");
            // The GLB contains alternate expression meshes. Show only one mouth.
            if (mesh.name.StartsWith("Mouth_") && mesh.name != "Mouth_402") _nodes[i].gameObject.SetActive(false);
        }
        var clip = ImportIdle(root.transform);
        var animation = root.AddComponent<Animation>();
        animation.AddClip(clip, _idle);
        animation.clip = clip;
        animation.playAutomatically = true;
        clip.SampleAnimation(root, 0f);
        var result = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + _character + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return result;
    }

    private Material[] ImportMaterials()
    {
        var textures = _gltf["images"].Select((image, index) =>
        {
            var view = _gltf["bufferViews"][(int)image["bufferView"]];
            var imageBytes = new byte[(int)view["byteLength"]];
            Buffer.BlockCopy(_bytes, _binary + ((int?)view["byteOffset"] ?? 0), imageBytes, 0, imageBytes.Length);
            string path = Folder + "/Texture" + index + ".png";
            File.WriteAllBytes(path, imageBytes);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }).ToArray();
        return _gltf["materials"].Select((source, index) =>
        {
            var material = new Material(Shader.Find("EchoProtocol/CharacterPreview"));
            var pbr = source["pbrMetallicRoughness"];
            if (pbr["baseColorTexture"] != null)
                material.mainTexture = textures[(int)_gltf["textures"][(int)pbr["baseColorTexture"]["index"]]["source"]];
            var color = pbr["baseColorFactor"];
            material.color = color == null ? Color.white : new Color((float)color[0], (float)color[1], (float)color[2], (float)color[3]);
            material.SetFloat("_Cutoff", (string)source["alphaMode"] == "MASK" ? (float?)source["alphaCutoff"] ?? 0.5f : 0f);
            AssetDatabase.CreateAsset(material, Folder + "/Material" + index + ".mat");
            return material;
        }).ToArray();
    }

    private AnimationClip ImportIdle(Transform root)
    {
        var source = _gltf["animations"].First(a => (string)a["name"] == _idle);
        var clip = new AnimationClip { name = _idle, legacy = true, wrapMode = WrapMode.Loop, frameRate = 30 };
        foreach (var channel in source["channels"])
        {
            string property = (string)channel["target"]["path"];
            if (property == "weights") continue;
            var sampler = source["samplers"][(int)channel["sampler"]];
            bool cubic = (string)sampler["interpolation"] == "CUBICSPLINE";
            var times = Read((int)sampler["input"]);
            var values = Read((int)sampler["output"]);
            int width = property == "rotation" ? 4 : 3;
            string field = property == "rotation" ? "localRotation" : property == "translation" ? "localPosition" : "localScale";
            string path = AnimationUtility.CalculateTransformPath(_nodes[(int)channel["target"]["node"]], root);
            for (int c = 0; c < width; c++)
            {
                float sign = property == "rotation" ? (c < 2 ? -1f : 1f) : property == "translation" && c == 2 ? -1f : 1f;
                var curve = new AnimationCurve(Enumerable.Range(0, times.Length)
                    .Select(t => cubic
                        ? new Keyframe(times[t], values[(t * 3 + 1) * width + c] * sign,
                            values[t * 3 * width + c] * sign, values[(t * 3 + 2) * width + c] * sign)
                        : new Keyframe(times[t], values[t * width + c] * sign)).ToArray());
                for (int k = 0; !cubic && k < curve.length; k++)
                {
                    var mode = (string)sampler["interpolation"] == "STEP" ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear;
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, mode);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, mode);
                }
                clip.SetCurve(path, typeof(Transform), field + "." + "xyzw"[c], curve);
            }
        }
        clip.EnsureQuaternionContinuity();
        AssetDatabase.CreateAsset(clip, Folder + "/" + _idle + ".anim");
        return clip;
    }

    private static Vector3 Vector(JToken token, Vector3 fallback, bool mirror) => token == null ? fallback :
        new Vector3((float)token[0], (float)token[1], (float)token[2] * (mirror ? -1f : 1f));

    private Vector3[] Vectors(int accessor, bool mirror)
    {
        var raw = Read(accessor);
        return Enumerable.Range(0, raw.Length / 3).Select(i =>
            new Vector3(raw[i * 3], raw[i * 3 + 1], raw[i * 3 + 2] * (mirror ? -1 : 1))).ToArray();
    }

    private float[] Read(int index)
    {
        var accessor = _gltf["accessors"][index];
        var view = _gltf["bufferViews"][(int)accessor["bufferView"]];
        int type = (int)accessor["componentType"];
        int size = type == 5123 ? 2 : 4;
        string shape = (string)accessor["type"];
        int width = shape == "SCALAR" ? 1 : shape == "VEC2" ? 2 : shape == "VEC3" ? 3 : shape == "VEC4" ? 4 : 16;
        int count = (int)accessor["count"], stride = (int?)view["byteStride"] ?? size * width;
        int offset = _binary + ((int?)view["byteOffset"] ?? 0) + ((int?)accessor["byteOffset"] ?? 0);
        var output = new float[count * width];
        for (int i = 0; i < count; i++) for (int c = 0; c < width; c++)
        {
            int at = offset + i * stride + c * size;
            output[i * width + c] = type == 5126 ? BitConverter.ToSingle(_bytes, at) :
                type == 5123 ? BitConverter.ToUInt16(_bytes, at) : BitConverter.ToUInt32(_bytes, at);
        }
        return output;
    }
}
#endif
