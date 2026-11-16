using System;
using System.Linq;
using UnityEngine;

public partial class LevelControl
{
    [SerializeField] private UploadNode[] uploadNodes = new UploadNode[0];
    [SerializeField] private ExtractionPoint normalExit, emergencyExit;
    [SerializeField] private LineRenderer purgePulse;
    [SerializeField] private Material purgeTelegraphMaterial;
    public Material PurgeTelegraphMaterial => purgeTelegraphMaterial;
    private float _purgePulseStarted = -1;
    public float PurgePulseStrength => _purgePulseStarted < 0 ? 0 : Mathf.Clamp01(1 - (model.combatElapsed - _purgePulseStarted) / 1.4f);
    private int _damageFrame = -1;
    private float _retryUploadAt;
    private float _nodeFeedbackUntil;
    private string _nodeFeedback;
    public bool UsesNodeObjectives => Configuration != null && Configuration.useNodeObjectives;
    public int UploadedCount => model.nodes.Count(n => n.state == UploadNodeState.Uploaded);
    public string NodeFeedback => model.combatElapsed < _nodeFeedbackUntil ? _nodeFeedback : null;
    public float UploadProgressRatio
    {
        get
        {
            var active = model.nodes.Find(n => n.state == UploadNodeState.Uploading);
            return active == null ? 0 : active.progress / Array.Find(uploadNodes, p => p.nodeId == active.nodeId).uploadSeconds;
        }
    }

    private void ValidateNodeBindings()
    {
        if (!UsesNodeObjectives) return;
        if (uploadNodes == null || uploadNodes.Length != Configuration.nodeIds.Length || normalExit == null || emergencyExit == null || normalExit == emergencyExit)
            throw new InvalidOperationException("节点或两条撤离出口绑定不完整。");
        var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var node in uploadNodes)
        {
            if (node == null || !ids.Add(node.nodeId) || !Configuration.nodeIds.Contains(node.nodeId) ||
                !Positive(node.uploadSeconds) || !Positive(node.radius) || !Positive(node.heightTolerance) || node.beacon == null ||
                node.availableMaterial == null || node.uploadingMaterial == null || node.completedMaterial == null || node.destroyedMaterial == null)
                throw new InvalidOperationException("节点 ID、范围、时长或表现资源无效。");
        }
        foreach (var exit in new[] { normalExit, emergencyExit })
            if (!Positive(exit.radius) || !Positive(exit.heightTolerance) || exit.barrier == null || exit.lockedMaterial == null || exit.openMaterial == null)
                throw new InvalidOperationException("撤离出口范围或表现资源无效。");
        if (purgePulse == null || purgeTelegraphMaterial == null) throw new InvalidOperationException("清除脉冲或 Boss 预警材质缺失。");
        foreach (var enemy in preparation.enemies)
        {
            var assignment = InitialEnemy(enemy).assignment;
            if (assignment == null || !Enum.IsDefined(typeof(EnemyRole), assignment.role) || !Positive(assignment.detectionRadius) ||
                !Positive(assignment.leashRadius) || !Positive(assignment.guardRadius))
                throw new InvalidOperationException("敌人职责或警戒、守卫范围无效。");
            if (assignment.role == EnemyRole.Patrol && (assignment.patrolPoints == null || assignment.patrolPoints.Length < 2 || Array.Exists(assignment.patrolPoints, p => p == null)))
                throw new InvalidOperationException("巡逻敌人至少需要两个有效路径点。");
            if (assignment.role == EnemyRole.NodeGuard || assignment.role == EnemyRole.NodeBoss)
                if (!ids.Contains(assignment.nodeId)) throw new InvalidOperationException("守卫绑定了不存在的节点：" + assignment.nodeId);
        }
    }

    private static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);

    private void ResetNodes()
    {
        model.nodes.Clear(); model.mission = UsesNodeObjectives ? MissionStage.Upload : MissionStage.None;
        _damageFrame = -1; _retryUploadAt = 0; _nodeFeedbackUntil = 0; _nodeFeedback = null;
        _purgePulseStarted = -1;
        if (!UsesNodeObjectives) return;
        foreach (var node in uploadNodes) model.nodes.Add(new UploadNodeModel { nodeId = node.nodeId });
        PresentNodes();
    }

    // Called synchronously after actual HP loss, before hurt broadcasts or healing callbacks.
    public void NotifyPlayerHealthLost(PlayerControl player, string runId)
    {
        if (!UsesNodeObjectives || player != preparation.player || runId != model.runId || model.phase != LevelPhase.Combat || model.result != LevelResult.None) return;
        _damageFrame = Time.frameCount;
        _retryUploadAt = model.combatElapsed + .8f;
        foreach (var node in model.nodes)
            if (node.state == UploadNodeState.Uploading) InterruptUpload(node, "HIT / UPLOAD RESET");
        PresentNodes();
    }

    private void InterruptUpload(UploadNodeModel node, string message)
    {
        node.progress = 0; node.state = UploadNodeState.Available;
        _nodeFeedback = message; _nodeFeedbackUntil = model.combatElapsed + 2;
    }

    private void BeginOvertime()
    {
        if (!UsesNodeObjectives || model.mission == MissionStage.Breakout || model.mission == MissionStage.EmergencyExtraction) return;
        model.mission = MissionStage.Breakout;
        _purgePulseStarted = model.combatElapsed;
        foreach (var node in model.nodes) { node.progress = 0; node.state = UploadNodeState.Destroyed; }
        _nodeFeedback = "PURGE PULSE / TERMINALS DESTROYED"; _nodeFeedbackUntil = model.combatElapsed + 4;
        PresentNodes();
    }

    // Runs after every projectile and the final death check: a last-frame hit cannot complete an upload.
    private void TickNodeObjectives(float dt)
    {
        if (model.mission == MissionStage.Upload)
        {
            UploadNode selected = null;
            foreach (var point in uploadNodes)
            {
                var node = model.nodes.Find(n => n.nodeId == point.nodeId);
                bool inside = point.Contains(preparation.player.transform.position);
                if (node.state == UploadNodeState.Uploading && !inside) InterruptUpload(node, "LEFT AREA / UPLOAD RESET");
                if (selected == null && inside && node.state != UploadNodeState.Uploaded) selected = point;
            }
            if (selected != null && _damageFrame != Time.frameCount && model.combatElapsed >= _retryUploadAt)
            {
                var node = model.nodes.Find(n => n.nodeId == selected.nodeId);
                node.state = UploadNodeState.Uploading;
                float safeDt = Mathf.Min(dt, model.combatElapsed - _retryUploadAt);
                node.progress = Mathf.Min(selected.uploadSeconds, node.progress + Mathf.Max(0, safeDt));
                if (node.progress >= selected.uploadSeconds) node.state = UploadNodeState.Uploaded;
            }
            if (UploadedCount == model.nodes.Count) model.mission = MissionStage.NormalExtraction;
        }
        if (model.mission == MissionStage.Breakout && model.bossState == BossSpawnState.Defeated)
            model.mission = MissionStage.EmergencyExtraction;
        PresentNodes();
        var exit = model.mission == MissionStage.NormalExtraction ? normalExit :
            model.mission == MissionStage.EmergencyExtraction ? emergencyExit : null;
        if (exit != null && exit.Contains(preparation.player.transform.position)) Settle(LevelResult.Victory);
    }

    private void PresentNodes()
    {
        if (!UsesNodeObjectives) return;
        foreach (var point in uploadNodes)
        {
            var node = model.nodes.Find(n => n.nodeId == point.nodeId);
            if (node != null) point.Present(node.state, node.progress);
        }
        normalExit.Present(model.mission == MissionStage.NormalExtraction);
        emergencyExit.Present(model.mission == MissionStage.EmergencyExtraction);
        purgePulse.enabled = PurgePulseStrength > 0;
        if (purgePulse.enabled)
        {
            float radius = Mathf.Lerp(2, 55, 1 - PurgePulseStrength);
            for (int i = 0; i < purgePulse.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / (purgePulse.positionCount - 1);
                purgePulse.SetPosition(i, purgePulse.transform.position + new Vector3(Mathf.Sin(angle) * radius, .18f, Mathf.Cos(angle) * radius));
            }
        }
    }

    public string NodeObjectiveText()
    {
        if (!UsesNodeObjectives) return "";
        string text = $"\nRELAYS {UploadedCount}/{model.nodes.Count}";
        if (model.mission == MissionStage.Upload)
        {
            var nearest = uploadNodes.OrderBy(p => Vector3.SqrMagnitude(p.transform.position - preparation.player.transform.position))
                .FirstOrDefault(p => model.nodes.Find(n => n.nodeId == p.nodeId).state != UploadNodeState.Uploaded);
            if (nearest != null)
            {
                var node = model.nodes.Find(n => n.nodeId == nearest.nodeId);
                text += node.state == UploadNodeState.Uploading ? $" / {nearest.displayName} {node.progress:0.0}/{nearest.uploadSeconds:0.0}s" :
                    $" / {nearest.displayName} {Vector3.Distance(nearest.transform.position, preparation.player.transform.position):0}m";
            }
        }
        else text += model.mission == MissionStage.NormalExtraction ? $" / EXIT {Vector3.Distance(normalExit.transform.position, preparation.player.transform.position):0}m" :
            model.mission == MissionStage.Breakout ? " / DEFEAT PURGE UNIT" : $" / RECOVERY GATE {Vector3.Distance(emergencyExit.transform.position, preparation.player.transform.position):0}m";
        if (NodeFeedback != null) text += "\n" + NodeFeedback;
        else if (model.mission == MissionStage.Upload) text += model.nodes.Any(n => n.state == UploadNodeState.Uploading) ?
            "\nStay in the ring / Hits reset upload" : "\nEnter a relay ring / Use echoes [1][2][3] for cover";
        return text;
    }
}
