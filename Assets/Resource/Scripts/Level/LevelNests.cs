using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public partial class LevelControl
{
    private readonly List<NestWaveSchedule> _nestSchedules = new List<NestWaveSchedule>();
    private readonly Dictionary<string, NestSpawnBinding> _nestBindings = new Dictionary<string, NestSpawnBinding>(StringComparer.Ordinal);
    private readonly Dictionary<int, NestWaveSchedule> _enemyNests = new Dictionary<int, NestWaveSchedule>();
    private readonly Dictionary<string, float> _nestRetries = new Dictionary<string, float>(StringComparer.Ordinal);
    public LevelConfig Configuration => preparation.levelConfig;

    private void ValidateNestBindings()
    {
        _nestBindings.Clear();
        foreach (var binding in preparation.combat.nests ?? new NestSpawnBinding[0])
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.nestId) || _nestBindings.ContainsKey(binding.nestId))
                throw new InvalidOperationException("巢穴场景绑定 ID 缺失或重复。");
            _nestBindings.Add(binding.nestId, binding);
        }
        foreach (var nest in Configuration.nests)
        {
            if (nest.WaveCount == 0) continue;
            if (!_nestBindings.TryGetValue(nest.nestId, out var binding) ||
                (binding.prefab == null && preparation.combat.ordinaryPrefab == null) ||
                binding.spawnPoints == null || binding.spawnPoints.Length == 0)
                throw new InvalidOperationException("巢穴缺少出生点或怪物预制体：" + nest.nestId);
            foreach (var point in binding.spawnPoints)
                if (point == null) throw new InvalidOperationException("巢穴存在空出生点：" + nest.nestId);
        }
        foreach (var id in _nestBindings.Keys)
            if (!Array.Exists(Configuration.nests, nest => nest.nestId == id))
                throw new InvalidOperationException("场景巢穴不在配置中：" + id);
    }

    private void InitializeNests()
    {
        _nestSchedules.Clear(); _enemyNests.Clear(); _nestRetries.Clear();
        foreach (var nest in Configuration.nests) _nestSchedules.Add(new NestWaveSchedule(nest));
        model.normalSpawningComplete = _nestSchedules.TrueForAll(nest => nest.SpawningComplete);
    }

    private void TickNests()
    {
        foreach (var nest in _nestSchedules)
        {
            if (_nestRetries.TryGetValue(nest.NestId, out float retry) && model.combatElapsed < retry) continue;
            nest.Tick(model.combatElapsed, index => SpawnNestEnemy(nest, index));
            model.currentWave = Mathf.Max(model.currentWave, nest.CurrentWave);
        }
        model.normalSpawningComplete = _nestSchedules.TrueForAll(nest => nest.SpawningComplete);
    }

    private int? SpawnNestEnemy(NestWaveSchedule nest, int index)
    {
        var binding = _nestBindings[nest.NestId];
        var point = binding.spawnPoints[index % binding.spawnPoints.Length];
        EnemyActor created = null;
        try
        {
            if (point == null || !NavMesh.SamplePosition(point.position, out var nav, 2, NavMesh.AllAreas))
                throw new InvalidOperationException("巢穴出生点不在导航网格上：" + nest.NestId);
            var prefab = binding.prefab != null ? binding.prefab : preparation.combat.ordinaryPrefab;
            created = Instantiate(prefab, nav.position, point.rotation, preparation.effectsRoot);
            created.name = nest.NestId + " - Wave " + nest.CurrentWave + " - Enemy " + (index + 1);
            created.ResetForRun(this, model.runId, preparation.player, preparation.combat.ordinary);
            created.Activate(nav.position);
            RegisterEnemy(created); _dynamicEnemies.Add(created);
            _enemyNests.Add(created.Model.instanceId, nest);
            _nestRetries.Remove(nest.NestId);
            return created.Model.instanceId;
        }
        catch (Exception error)
        {
            if (created != null) { created.gameObject.SetActive(false); Destroy(created.gameObject); }
            _nestRetries[nest.NestId] = model.combatElapsed + 1;
            if (model.runtimeError != error.Message) Debug.LogWarning(error.Message);
            model.runtimeError = error.Message;
            return null;
        }
    }

    private void ClearNests()
    {
        _nestSchedules.Clear(); _enemyNests.Clear(); _nestRetries.Clear();
    }
}
