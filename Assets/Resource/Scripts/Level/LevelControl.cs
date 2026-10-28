using System;
using UnityEngine;

// ============================================================
// LevelControl -- level controller.
// Responsibilities:
//   1) Holds LevelModel.
//   2) Wave scheduling (cooperates with EnemyControl).
//   3) Level objective evaluation.
// ============================================================
public partial class LevelControl : GlobalControl<LevelControl, LevelModel>
{
    [SerializeField] private WaveConfig[] waves;
    [SerializeField] private EnemyControl[] enemyPrefabs;
    [SerializeField] private Transform spawnRoot;
    [SerializeField] private Transform[] spawnPoints;

    private int _waveIndex;
    private float _waveTimer;
    private int _spawnedInWave;
    private float _intervalTimer;
    private bool _active;

    protected override void OnInitialize()
    {
        base.OnInitialize();
        if (usePreparation) { InitializePreparation(); return; }
        EventCenter.Instance.AddListener(GameEvent.Enemy_Die, OnEnemyDied);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Enemy_Die, OnEnemyDied);
    }

    public void StartLevel()
    {
        if (usePreparation) { RestartPreparation(); return; }
        model.Reset();
        _waveIndex = 0;
        _spawnedInWave = 0;
        _active = waves != null && waves.Length > 0;

        if (_active)
        {
            _waveTimer = waves[0].delayBeforeStart;
            EventCenter.Instance.Dispatch(new GameEventArgs
            {
                eventType = GameEvent.Level_WaveStart,
                data = new ValueArgs<int>(0)
            });
        }

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Level_Start,
            data = model
        });
    }

    public override void Tick(float deltaTime)
    {
        if (usePreparation) { TickPreparation(deltaTime); return; }
        if (!_active) return;

        var wave = waves[_waveIndex];
        if (_waveTimer > 0f)
        {
            _waveTimer -= deltaTime;
            return;
        }

        _intervalTimer -= deltaTime;
        if (_intervalTimer <= 0f && _spawnedInWave < wave.enemyCount)
        {
            Spawn(wave.enemyType);
            _spawnedInWave++;
            _intervalTimer = wave.spawnInterval;
        }

        if (_spawnedInWave >= wave.enemyCount && model.aliveEnemyCount <= 0)
        {
            _waveIndex++;
            if (_waveIndex >= waves.Length)
            {
                _active = false;
                CompleteLevel();
                return;
            }

            var next = waves[_waveIndex];
            _waveTimer = next.delayBeforeStart;
            _spawnedInWave = 0;
            EventCenter.Instance.Dispatch(new GameEventArgs
            {
                eventType = GameEvent.Level_WaveStart,
                data = new ValueArgs<int>(_waveIndex)
            });
        }
    }

    private void Spawn(EnemyType type)
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;
        var idx = Mathf.Clamp((int)type, 0, enemyPrefabs.Length - 1);
        var prefab = enemyPrefabs[idx];
        if (prefab == null) return;

        var point = ResolveSpawnPoint();
        var go = Instantiate(prefab.gameObject, point, Quaternion.identity, spawnRoot);
        var enemy = go.GetComponent<EnemyControl>();
        model.aliveEnemyCount++;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Enemy_Spawn,
            data = enemy
        });
    }

    private Vector3 ResolveSpawnPoint()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var pick = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            return pick != null ? pick.position : Vector3.zero;
        }
        return transform.position;
    }

    private void OnEnemyDied(GameEventArgs args)
    {
        if (usePreparation) return;
        model.aliveEnemyCount = Mathf.Max(0, model.aliveEnemyCount - 1);
        model.killCount++;

        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Level_ObjectiveUpdated,
            data = new IntPairArgs(model.killCount, model.killRequirement)
        });

        if (model.objective == LevelObjective.EliminateAll &&
            model.killCount >= model.killRequirement && !model.objectiveComplete)
        {
            model.objectiveComplete = true;
            CompleteLevel();
        }
    }

    private void CompleteLevel()
    {
        EventCenter.Instance.Dispatch(new GameEventArgs
        {
            eventType = GameEvent.Level_Complete,
            data = new ValueArgs<bool>(true)
        });
        WarControl.Instance?.EndWar(true);
    }
}
