using UnityEngine;
using UnityEngine.AI;

/// <summary>Scene-owned navigation data. Does not create a persistent service.</summary>
public sealed class LevelNavigation : MonoBehaviour
{
    [SerializeField] private NavMeshData data;
    private NavMeshDataInstance _instance;
    private void OnEnable()
    {
        if (data != null) _instance = NavMesh.AddNavMeshData(data);
    }
    private void OnDisable()
    {
        if (_instance.valid) _instance.Remove();
    }
}
