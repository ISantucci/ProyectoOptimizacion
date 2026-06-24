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

        // Cola de enemigos pendientes de entregar en la wave actual (orden ya mezclado).
        private readonly Queue<EnemyTypeData> _pendingThisWave = new Queue<EnemyTypeData>();

        // Pausa entre waves.
        private bool _waitingBetweenWaves;
        private float _interWaveTimer;
        private float _interWaveDelay;

        private readonly System.Random _rng = new System.Random();

        public int CurrentWaveIndex => _waveIndex;
        public bool IsWaveComplete { get; private set; }
        public bool IsRoomComplete { get; private set; }

        private int WaveCount => (_room != null && _room.Waves != null) ? _room.Waves.Count : 0;

        // --- Datos expuestos para el HUD (solo lectura, no cambian la lógica) ---

        /// <summary>Cantidad total de waves de la room activa.</summary>
        public int TotalWaves => WaveCount;

        /// <summary>True si la wave actual es la última de la room.</summary>
        public bool IsFinalWave => WaveCount > 0 && _waveIndex == WaveCount - 1;

        /// <summary>Nombre de la wave actual (WaveDefinition.WaveName), o cadena vacía si no hay wave válida.</summary>
        public string CurrentWaveName
        {
            get
            {
                if (_room == null || _room.Waves == null || _waveIndex < 0 || _waveIndex >= WaveCount)
                    return string.Empty;
                var wave = _room.Waves[_waveIndex];
                return wave != null ? wave.WaveName : string.Empty;
            }
        }

        /// <summary>Enemigos que todavía faltan spawnear en la wave actual.</summary>
        public int PendingToSpawnCount => _pendingThisWave.Count;

        /// <summary>Arranca las waves de una room. Si la room no tiene waves, queda IsRoomComplete.</summary>
        public void StartRoom(RoomDefinition room)
        {
            _room = room;
            _waveIndex = -1;
            _roomActive = room != null;
            _spawnTimer = 0f;
            _waitingBetweenWaves = false;
            _interWaveTimer = 0f;
            _interWaveDelay = 0f;
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

            // Aplanar la wave en una lista (tipo repetido Count veces) y mezclar.
            var spawnOrder = new List<EnemyTypeData>();
            var wave = _room.Waves[_waveIndex];
            if (wave?.Enemies != null)
            {
                for (int e = 0; e < wave.Enemies.Count; e++)
                {
                    var entry = wave.Enemies[e];
                    if (entry == null || entry.EnemyType == null)
                        continue;

                    for (int c = 0; c < entry.Count; c++)
                        spawnOrder.Add(entry.EnemyType);
                }
            }

            Shuffle(spawnOrder);

            for (int i = 0; i < spawnOrder.Count; i++)
                _pendingThisWave.Enqueue(spawnOrder[i]);
        }

        // Fisher-Yates in-place.
        private void Shuffle(List<EnemyTypeData> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public void Tick(float deltaTime)
        {
            if (!_roomActive)
                return;

            if (_waitingBetweenWaves)
                _interWaveTimer += deltaTime;
            else
                _spawnTimer += deltaTime;
        }

        /// <summary>
        /// Devuelve true y entrega el próximo EnemyTypeData a spawnear si corresponde
        /// (respeta SpawnDelay, MaxEnemiesAlive y la pausa entre waves). El GameManager hace el spawn real.
        /// </summary>
        public bool TryGetNextEnemyType(int aliveEnemyCount, out EnemyTypeData enemyType)
        {
            enemyType = null;

            if (!_roomActive || _waitingBetweenWaves || _waveIndex < 0 || _waveIndex >= WaveCount)
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
        /// Avanza wave/room. Cuando se spawnearon todos los enemigos de la wave y no queda
        /// ninguno vivo, espera DelayBeforeNextWave antes de arrancar la próxima.
        /// El GameManager la llama tras resolver muertes.
        /// </summary>
        public void UpdateProgress(int aliveEnemyCount)
        {
            if (!_roomActive)
                return;

            // En pausa entre waves: esperar y luego arrancar la siguiente.
            if (_waitingBetweenWaves)
            {
                if (_interWaveTimer >= _interWaveDelay)
                {
                    _waitingBetweenWaves = false;
                    AdvanceToNextWave();
                }
                return;
            }

            if (_waveIndex < 0 || _waveIndex >= WaveCount)
                return;

            bool allSpawned = _pendingThisWave.Count == 0;
            if (allSpawned && aliveEnemyCount == 0)
            {
                IsWaveComplete = true;

                bool hasNextWave = _waveIndex + 1 < WaveCount;
                float delay = _room.Waves[_waveIndex].DelayBeforeNextWave;

                // Solo se espera si hay una wave siguiente y el delay es positivo.
                // La última wave completa la room sin esperar.
                if (hasNextWave && delay > 0f)
                {
                    _waitingBetweenWaves = true;
                    _interWaveTimer = 0f;
                    _interWaveDelay = delay;
                }
                else
                {
                    AdvanceToNextWave();
                }
            }
        }
    }
}
