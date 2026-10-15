using UnityEngine;

[CreateAssetMenu(menuName = "EchoProtocol/AnimClipSet")]
public class AnimClipSet : ScriptableObject
{
        [Header("Basic Locomotion")]
    public AnimationClip idle;
    public AnimationClip run;
    public AnimationClip crouch;
    public AnimationClip jump;

        [Header("Combat")]
    public AnimationClip fire;
    public AnimationClip reload;
    public AnimationClip hit;
    public AnimationClip die;

        [Header("Echo Specific")]
    public AnimationClip recording;
    public AnimationClip playback;
}
