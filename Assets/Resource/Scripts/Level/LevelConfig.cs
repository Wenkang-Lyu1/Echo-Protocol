using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One asset per level. Keep runtime counters in the controller.</summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Echo Protocol/关卡配置")]
public sealed class LevelConfig : ScriptableObject
{
    public string levelId = "level_new";
    [Tooltip("下一关场景名或完整资源路径；留空表示最后一关。")] public string nextSceneName = "";
    [Min(.01f)] public float durationSeconds = 240;
    [HideInInspector] public int echoLimit = 5; // Legacy serialized data only; formal levels use echoSkill.
    public EchoSkillConfig echoSkill;
    public NestConfigEntry[] nests = new NestConfigEntry[0];

    public void Validate()
    {
        Require(!string.IsNullOrWhiteSpace(levelId), "关卡 ID 不能为空。");
        Require(FiniteNonnegative(durationSeconds) && durationSeconds > 0, "关卡时长必须为正数且有限。");
        Require(echoSkill != null, "请迁移关卡配置：绑定回声 2.0 技能资源（echoSkill），旧 echoLimit 已不生效。");
        echoSkill.Validate();
        Require(nests != null, "巢穴列表不能为 null。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nest in nests)
        {
            Require(nest != null, "巢穴不能为 null。");
            Require(!string.IsNullOrWhiteSpace(nest.nestId) && ids.Add(nest.nestId), "巢穴 ID 不能为空或重复。");
            Require(FiniteNonnegative(nest.firstWaveTimeSeconds), nest.nestId + "：首波时间必须非负且有限。");
            Require(nest.maxAliveEnemies >= 0, nest.nestId + "：同时存活上限不能为负数。");
            Require(nest.waves != null, nest.nestId + "：波次列表不能为 null。");
            for (int i = 0; i < nest.waves.Length; i++)
            {
                var wave = nest.waves[i];
                string path = nest.nestId + " 第 " + (i + 1) + " 波：";
                Require(wave != null, path + "不能为 null。");
                Require(wave.enemyCount > 0, path + "怪物数量必须大于 0。");
                Require(FiniteNonnegative(wave.delayAfterPreviousClearSeconds), path + "清波后等待必须非负且有限。");
                Require(FiniteNonnegative(wave.spawnIntervalSeconds), path + "出怪间隔必须非负且有限。");
                Require(i != 0 || wave.delayAfterPreviousClearSeconds == 0, path + "首波等待必须为 0，请配置首波开始时间。");
            }
        }
    }

    private static bool FiniteNonnegative(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}

[Serializable]
public sealed class NestConfigEntry
{
    public string nestId = "nest_new";
    [Min(0)] public float firstWaveTimeSeconds;
    [Tooltip("0 不限制；只统计本巢穴的存活敌人。")] [Min(0)] public int maxAliveEnemies;
    public bool randomizeSpawnPoints;
    public NestWaveConfigEntry[] waves = new NestWaveConfigEntry[0];
    public int WaveCount => waves == null ? 0 : waves.Length;
}

[Serializable]
public sealed class NestWaveConfigEntry
{
    [Min(1)] public int enemyCount = 1;
    [Min(0)] public float delayAfterPreviousClearSeconds;
    [Tooltip("0 同时生成；大于 0 时逐只生成，不在卡顿后集中补怪。")] [Min(0)] public float spawnIntervalSeconds;
}
