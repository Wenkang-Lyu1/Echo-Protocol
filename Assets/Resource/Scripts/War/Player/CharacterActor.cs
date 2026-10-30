using UnityEngine;

/// <summary>Scene instance of a character. Movement and playback can place any derived actor.</summary>
public abstract class CharacterActor : MonoBehaviour
{
    public void SetPose(Vector3 position, Quaternion rotation) => transform.SetPositionAndRotation(position, rotation);
}
