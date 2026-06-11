using OptimizationGame.Data;
using OptimizationGame.Interfaces;
using UnityEngine;

namespace OptimizationGame.Systems
{
    public class WaveSystem : ITickable
    {
        private WaveConfig _config;
        private int _currentWave;
        private int _totalWaves;
        private int _enemiesSpawnedThisWave;
        private float _spawnTimer;
        private float _waveDelayTimer;
        private bool _waveActive;
        private bool _allWavesCompleted;

        public int CurrentWave => _currentWave;
        public int TotalWaves => _totalWaves;
        public bool WaveActive => _waveActive;
        public bool AllWavesCompleted => _allWavesCompleted;

        public WaveSystem(WaveConfig config, int totalWaves = 5)
        {
            _config = config;
            _totalWaves = totalWaves;
            _currentWave = 0;
            _enemiesSpawnedThisWave = 0;
            _spawnTimer = 0;
            _waveDelayTimer = 0;
            _waveActive = false;
            _allWavesCompleted = false;
        }

        public void StartWaves()
        {
            _currentWave = 1;
            _waveActive = true;
            _enemiesSpawnedThisWave = 0;
            _spawnTimer = 0;
        }

        public void Tick(float deltaTime)
        {
            if (_allWavesCompleted)
                return;

            if (_waveActive)
            {
                _spawnTimer += deltaTime;
                if (_spawnTimer >= _config.SpawnDelay && _enemiesSpawnedThisWave < _config.EnemyCount)
                {
                    _spawnTimer = 0;
                    _enemiesSpawnedThisWave++;
                }
            }
            else if (_currentWave < _totalWaves)
            {
                _waveDelayTimer += deltaTime;
                if (_waveDelayTimer >= _config.DelayBetweenWaves)
                {
                    _waveDelayTimer = 0;
                    _currentWave++;
                    _enemiesSpawnedThisWave = 0;
                    _waveActive = true;
                }
            }
        }

        public bool ShouldSpawnEnemy()
        {
            if (!_waveActive || _enemiesSpawnedThisWave == 0)
                return false;

            int alreadySpawned = _enemiesSpawnedThisWave - 1;
            return alreadySpawned < _config.EnemyCount;
        }

        public void MarkWaveEnemiesClearedIfReady(int aliveEnemyCount)
        {
            if (_waveActive && _enemiesSpawnedThisWave >= _config.EnemyCount && aliveEnemyCount == 0)
            {
                _waveActive = false;

                if (_currentWave >= _totalWaves)
                {
                    _allWavesCompleted = true;
                }
                else
                {
                    _waveDelayTimer = 0;
                }
            }
        }
    }
}
