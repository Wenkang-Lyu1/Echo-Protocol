using System;
using System.Collections.Generic;

/// <summary>Per-run wave scheduling. No scene access and no mutation of the config asset.</summary>
public sealed class NestWaveSchedule
{
    private readonly NestWaveConfigEntry[] _waves;
    private readonly HashSet<int> _alive = new HashSet<int>();
    private int _waveIndex, _spawned;
    private float _readyTime;
    public string NestId { get; }
    public int CurrentWave => Math.Min(_waveIndex + 1, _waves.Length);
    public bool SpawningComplete => _waves.Length == 0 ||
        (_waveIndex == _waves.Length - 1 && _spawned == _waves[_waveIndex].enemyCount) || _waveIndex >= _waves.Length;

    public NestWaveSchedule(NestConfigEntry config)
    {
        NestId = config.nestId;
        _readyTime = config.firstWaveTimeSeconds;
        _waves = new NestWaveConfigEntry[config.waves.Length];
        for (int i = 0; i < _waves.Length; i++)
            _waves[i] = new NestWaveConfigEntry { enemyCount = config.waves[i].enemyCount,
                delayAfterPreviousClearSeconds = config.waves[i].delayAfterPreviousClearSeconds };
    }

    // The callback returns a successfully initialized enemy ID, or null to retry later.
    public void Tick(float combatTime, Func<int, int?> spawn)
    {
        AdvanceAfterClear(combatTime);
        if (_waveIndex >= _waves.Length || combatTime < _readyTime) return;
        while (_spawned < _waves[_waveIndex].enemyCount)
        {
            int? id = spawn(_spawned);
            if (!id.HasValue) return;
            if (!_alive.Add(id.Value)) throw new InvalidOperationException("Duplicate nest enemy ID: " + id);
            _spawned++;
        }
    }

    public void NotifyDeath(int instanceId, float combatTime)
    {
        if (_alive.Remove(instanceId)) AdvanceAfterClear(combatTime);
    }

    private void AdvanceAfterClear(float combatTime)
    {
        if (_waveIndex >= _waves.Length || _spawned < _waves[_waveIndex].enemyCount || _alive.Count != 0) return;
        _waveIndex++;
        _spawned = 0;
        if (_waveIndex < _waves.Length)
            _readyTime = combatTime + _waves[_waveIndex].delayAfterPreviousClearSeconds;
    }
}
