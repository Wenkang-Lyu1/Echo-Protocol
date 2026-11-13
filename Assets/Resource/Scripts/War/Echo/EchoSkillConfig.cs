using System;
using UnityEngine;

[CreateAssetMenu(fileName = "EchoSkill", menuName = "Echo Protocol/回声技能配置")]
public sealed class EchoSkillConfig : ScriptableObject
{
    [Min(1)] public int slotCount = 3;
    [Min(.01f)] public float cooldownSeconds = 20;
    [Min(.01f)] public float segmentLimitSeconds = 20;

    public void Validate()
    {
        if (slotCount != 3 || cooldownSeconds != 20 || segmentLimitSeconds != 20)
            throw new InvalidOperationException("回声 2.0 配置必须为 3 个槽位、20 秒共享冷却、单段最多 20 秒。");
    }
}
