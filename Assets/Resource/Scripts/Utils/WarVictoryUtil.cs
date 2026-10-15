using UnityEngine;

// ============================================================
// WarVictoryUtil -- victory evaluation utility.
// Pure static, stateless.
// ============================================================
public static class WarVictoryUtil
{
        // Required kill count for the current level (reads LevelModel across modules).
    public static int GetRequiredKills(WarModel warModel)
    {
        var level = LevelControl.Instance?.GetModel();
        return level != null ? level.killRequirement : 0;
    }

        // Check whether the victory condition is met.
    public static bool CheckVictory(WarModel warModel)
    {
        var level = LevelControl.Instance?.GetModel();
        if (level == null) return false;

        switch (level.objective)
        {
            case LevelObjective.EliminateAll:
                return level.killCount >= level.killRequirement;
            case LevelObjective.ActivateSwitches:
                return level.objectiveComplete;
            case LevelObjective.ReachExtraction:
                return level.objectiveComplete;
            default:
                return false;
        }
    }

        // Combat score.
    public static int CalculateScore(WarModel warModel)
    {
        var baseScore = warModel.killCount * 100;
        var timeBonus = Mathf.Max(0, 5000 - Mathf.RoundToInt(warModel.elapsedTime * 10f));
        return baseScore + timeBonus;
    }
}
