using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class LevelControl
{
    private bool _completingAndAdvancing;
    private bool _loadingNextLevel;

    /// <summary>F1 shortcut: settle once, then load the configured next level.</summary>
    public bool CompleteAndAdvance()
    {
        if (!usePreparation || !IsInitialized || _completingAndAdvancing || _loadingNextLevel ||
            model.phase == LevelPhase.Idle || model.result == LevelResult.Defeat || preparation.player.IsDead)
            return false;

        _completingAndAdvancing = true;
        try
        {
            string run = model.runId;
            if (model.phase != LevelPhase.Ended)
            {
                var flow = GameManager.Instance;
                if (flow.IsPaused) flow.RequestResume();
                if (flow.State != GameState.Playing) return false;
                // Return the camera to the body even if F1 interrupted an active recording.
                preparation.echo.ResetState();
                model.phase = LevelPhase.Combat;
                Settle(LevelResult.Victory);
            }
            if (model.runId != run || model.phase != LevelPhase.Ended || model.result != LevelResult.Victory)
                return false;

            string next = Configuration.nextSceneName;
            if (string.IsNullOrWhiteSpace(next)) return true; // Final level keeps its victory screen.
            if (!Application.CanStreamedLevelBeLoaded(next))
                return ReportNextLevelError("下一关未加入构建列表或场景不存在：" + next);

            _loadingNextLevel = true;
            GameManager.Instance.SetState(GameState.Loading);
            try
            {
                if (SceneManager.LoadSceneAsync(next, LoadSceneMode.Single) == null)
                    return ReportNextLevelError("下一关加载失败：" + next);
            }
            catch (Exception error)
            {
                return ReportNextLevelError("下一关加载失败：" + error.Message);
            }
            // Single-scene loading unloads old gameplay while the Frame remains persistent.
            CleanupCombat();
            preparation.hud.gameObject.SetActive(false);
            return true;
        }
        finally { _completingAndAdvancing = false; }
    }

    private bool ReportNextLevelError(string message)
    {
        _loadingNextLevel = false;
        model.runtimeError = message;
        if (GameManager.Instance.State == GameState.Loading) GameManager.Instance.SetState(GameState.Victory);
        Debug.LogWarning(message);
        preparation.hud.Refresh();
        return false;
    }
}
