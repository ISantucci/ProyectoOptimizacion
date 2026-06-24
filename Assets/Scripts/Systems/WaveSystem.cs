using System.Collections.Generic;
using OptimizationGame.Data;
using OptimizationGame.Interfaces;

namespace OptimizationGame.Systems
{
    /// <summary>
    /// Sabe QUÉ enemigos faltan en la room activa, wave por wave.
    /// Data-driven desde RoomDefinition.Waves -> WaveEnemyEntry -> EnemyTypeData.
    /// Clase pura. No conoce Transform, ObjectPool, EntityView ni GameManager.
    /// No tiene lógica de spawn: solo decide cuándo/qué entregar.
    /// </summary>
    public class WaveSystem : ITickable
    {
        private RoomDefinition _room;
        private int _waveIndex;
        private bool _roomActive;
        private float _spawnTimer;

        // Cola de enemigos pendientes de entregar en la wave actual.
        private readonly Queue<EnemyTypeData> _pendingThisWave = new Queue<EnemyTypeData>();

        public int CurrentWaveIndex => _waveIndex;
        public bool IsWaveComplete { get; private set; }
        public bool IsRoomComplete { get; private set; }

        private int WaveCount => (_room != null && _room.Waves != null) ? _room.Waves.Count : 0;

        /// <summary>Arranca las waves de una room. Si la room no tiene waves, queda IsRoomComplete.</summary>
        public void StartRoom(RoomDefinition room)
        {
            _room = room;
            _waveIndex = -1;
            _roomActive = room != null;
            _spawnTimer = 0f;
            IsRoomComplete = false;
            IsWaveComplete = false;
            _pendingThisWave.Clear();

            if (WaveCount == 0)
            {
                _roomActive = false;
                IsRoomComplete = true;
                return;
            }

            AdvanceToNextWave();
        }

        private void AdvanceToNextWave()
        {
            _waveIndex++;
            _spawnTimer = 0f;
            IsWaveComplete = false;
            _pendingThisWave.Clear();

            if (_waveIndex >= WaveCount)
            {
                _roomActive = false;
                IsRoomComplete = true;
                return;
            }

            var wave = _room.Waves[_waveIndex];
            if (wave?.Enemies != null)
            {
                for (int e = 0; e < wave.Enemies.Count; e++)
                {
                    var entry = wave.Enemies[e];
                    if (entry == null || entry.EnemyType == null)
                        continue;

                    for (int c = 0; c < entry.Count; c++)
                        _pendingThisWave.Enqueue(entry.EnemyType);
                }
            }
        }

        public void Tick(float deltaTime)
        {
            if (_roomActive)
                _spawnTimer += deltaTime;
        }

        /// <summary>
        /// Devuelve true y entrega el próximo EnemyTypeData a spawnear si corresponde
        /// (respeta SpawnDelay y MaxEnemiesAlive de la wave). El GameManager hace el spawn real.
        /// </summary>
        public bool TryGetNextEnemyType(int aliveEnemyCount, out EnemyTypeData enemyType)
        {
            enemyType = null;

            if (!_roomActive || _waveIndex < 0 || _waveIndex >= WaveCount)
                return false;

            var wave = _room.Waves[_waveIndex];

            // MaxEnemiesAlive <= 0 significa sin límite.
            if (wave.MaxEnemiesAlive > 0 && aliveEnemyCount >= wave.MaxEnemiesAlive)
                return false;

            if (_spawnTimer < wave.SpawnDelay)
                return false;

            if (_pendingThisWave.Count == 0)
                return false;

            _spawnTimer = 0f;
            enemyType = _pendingThisWave.Dequeue();
            return true;
        }

        /// <summary>
        /// Avanza wave/room cuando se spawnearon todos los enemigos de la wave y no queda
        /// ninguno vivo. El GameManager la llama tras resolver muertes.
        /// </summary>
        public void UpdateProgress(int aliveEnemyCount)
        {
            if (!_roomActive || _waveIndex < 0 || _waveIndex >= WaveCount)
                return;

            bool allSpawned = _pendingThisWave.Count == 0;
            if (allSpawned && aliveEnemyCount == 0)
            {
                IsWaveComplete = true;
                AdvanceToNextWave();
            }
        }
    }
}
