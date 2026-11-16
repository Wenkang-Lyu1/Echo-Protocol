using System;

[Serializable]
public sealed class PurgeBossConfig
{
    public float warningSeconds = 1.2f;
    public float sweepSeconds = 2.4f;
    public float sweepArc = 65;
    public float chargeWarningSeconds = 1;
    public float chargeSeconds = .85f;
    public float chargeDistance = 11;
    public float chargeDamage = 20;
    public float exposedSeconds = 3.2f;
    public float exposedDamageMultiplier = 1.75f;

    public void Validate()
    {
        foreach (float value in new[] { warningSeconds, sweepSeconds, sweepArc, chargeWarningSeconds, chargeSeconds,
            chargeDistance, chargeDamage, exposedSeconds, exposedDamageMultiplier })
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) throw new InvalidOperationException("毁灭 Boss 技能参数必须为正数且有限。");
        if (sweepArc > 180 || exposedDamageMultiplier < 1) throw new InvalidOperationException("扫射角度或核心倍率无效。");
    }
}
