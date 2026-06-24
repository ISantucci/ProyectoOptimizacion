using System;
using System.Collections.Generic;
using OptimizationGame.Data;
using OptimizationGame.Models;
using OptimizationGame.Systems;
using UnityEngine;

namespace OptimizationGame.Core
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private CustomUpdateManager _updateManager;
        [SerializeField] private PoolConfig _poolConfig;

        // Flujo data-driven: GameFlowConfig define QUÉ pasa; RoomSpawnGroups, DÓNDE.
        [SerializeField] private GameFlowConfig _gameFlowConfig;
        [SerializeField] private List<RoomSpawnGroup> _roomSpawnGroups = new();

        private PlayerSystem _playerSystem;
        private EnemySystem _enemySystem;
        private ProjectileSystem _projectileSystem;
        private WaveSystem _waveSystem;
        private RoomSystem _roomSystem;
        private CombatSystem _combatSystem;
        private ObjectPool _objectPool;

        private PlayerModel _playerModel;
        private Dictionary<EnemyModel, MonoBehaviours.EntityView> _enemyViews = new();
        private Dictionary<ProjectileModel, MonoBehaviours.EntityView> _projectileViews = new();

        private bool _gameOver;
        private bool _playerWon;
        private bool _loggedMissingSpawnGroup;

        private const float EnemyDamageInterval = 1f;
        private float _enemyDamageTimer;

        // --- HUD: eventos push (clases puras, sin MonoBehaviours nuevos) ---
        // UIManager se suscribe a estos eventos. Solo se disparan cuando el dato cambia.
        public event Action<float, float> HealthChanged;          // (current, max)
        public event Action<string, int, int, bool> WaveChanged;  // (waveName, currentIndex, totalWaves, isFinalWave)
        public event Action<int> EnemiesLeftChanged;              // (enemiesLeft)
        public event Action<string> WeaponChanged;               // (weaponName)

        // Nombre de arma actual (fuente temporal: PlayerConfig.WeaponName).
        private string _weaponName;

        // Cache de últimos valores enviados al HUD para detectar cambios.
        private float _lastHealth = float.NaN;
        private float _lastMaxHealth = float.NaN;
        private int _lastWaveIndex = int.MinValue;
        private int _lastEnemiesLeft = int.MinValue;

        private void Awake()
        {
            InitializeSystems();
            InitializePools();
            RegisterSystems();
        }

        private void Start()
        {
            _roomSystem.StartFirstRoom();

            if (!_roomSystem.HasCurrentRoom)
            {
                Debug.LogError("GameManager: GameFlowConfig vacío o sin rooms asignadas. No se iniciará el spawn.");
                return;
            }

            _waveSystem.StartRoom(_roomSystem.CurrentRoom);
        }

        private void InitializeSystems()
        {
            var playerConfig = new PlayerConfig();
            _weaponName = playerConfig.WeaponName;
            _playerModel = new PlayerModel(playerConfig.MaxHealth, playerConfig.MoveSpeed);
            _playerModel.Position = _playerTransform.position;

            _playerSystem = new PlayerSystem(_playerModel, playerConfig);
            _enemySystem = new EnemySystem(() => _playerModel.Position);
            _projectileSystem = new ProjectileSystem();
            _waveSystem = new WaveSystem();
            _roomSystem = new RoomSystem(_gameFlowConfig);
            _combatSystem = new CombatSystem();
        }

        private void InitializePools()
        {
            _objectPool = new ObjectPool();
            _objectPool.RegisterPrefab("Enemy", _enemyPrefab);
            _objectPool.RegisterPrefab("Projectile", _projectilePrefab);

            if (_poolConfig == null)
            {
                Debug.LogError("GameManager missing PoolConfig reference. Pools will not be prewarmed (they will still grow on demand).");
                return;
            }

            _objectPool.Prewarm("Enemy", _poolConfig.EnemyPrewarm);
            _objectPool.Prewarm("Projectile", _poolConfig.ProjectilePrewarm);
        }

        private void RegisterSystems()
        {
            _updateManager.Register(_playerSystem);
            _updateManager.Register(_enemySystem);
            _updateManager.Register(_projectileSystem);
            _updateManager.Register(_waveSystem);
        }

        private void Update()
        {
            // Flush del HUD primero para que el último estado (p. ej. vida = 0 al morir)
            // se notifique aunque el frame siguiente salga temprano por _gameOver.
            RefreshHud();

            if (_gameOver)
                return;

            HandleSpawning();
            HandleCombat();
            HandleGameState();
            UpdateViews();
        }

        /// <summary>
        /// Reusa el loop existente del GameManager (no agrega Update propio en la UI).
        /// Compara cada dato contra su valor cacheado y solo dispara el evento si cambió.
        /// </summary>
        private void RefreshHud()
        {
            // Vida.
            float health = _playerModel.Health;
            float maxHealth = _playerModel.MaxHealth;
            if (health != _lastHealth || maxHealth != _lastMaxHealth)
            {
                _lastHealth = health;
                _lastMaxHealth = maxHealth;
                HealthChanged?.Invoke(health, maxHealth);
            }

            // Wave (cambia cuando cambia el índice de wave).
            int waveIndex = _waveSystem.CurrentWaveIndex;
            if (waveIndex != _lastWaveIndex)
            {
                _lastWaveIndex = waveIndex;
                WaveChanged?.Invoke(
                    _waveSystem.CurrentWaveName,
                    waveIndex,
                    _waveSystem.TotalWaves,
                    _waveSystem.IsFinalWave);
            }

            // Enemigos restantes = pendientes de spawnear + vivos.
            int enemiesLeft = _waveSystem.PendingToSpawnCount + _enemySystem.AliveCount;
            if (enemiesLeft != _lastEnemiesLeft)
            {
                _lastEnemiesLeft = enemiesLeft;
                EnemiesLeftChanged?.Invoke(enemiesLeft);
            }
        }

        /// <summary>
        /// Empuja el estado inicial completo del HUD una sola vez.
        /// La llama UIManager en su Start(), después de suscribirse a los eventos,
        /// para no depender del orden de ejecución de scripts.
        /// </summary>
        public void BroadcastInitialUiState()
        {
            // Empuja una snapshot inmediata para que el HUD no quede vacío.
            HealthChanged?.Invoke(_playerModel.Health, _playerModel.MaxHealth);
            WaveChanged?.Invoke(
                _waveSystem.CurrentWaveName,
                _waveSystem.CurrentWaveIndex,
                _waveSystem.TotalWaves,
                _waveSystem.IsFinalWave);
            EnemiesLeftChanged?.Invoke(_waveSystem.PendingToSpawnCount + _enemySystem.AliveCount);

            // El arma no cambia en este bloque: este es su único broadcast.
            WeaponChanged?.Invoke(_weaponName);

            // Dejar caches en sentinela: si UIManager.Start corrió antes de GameManager.Start
            // (StartRoom aún no aplicado), el primer RefreshHud re-emitirá con datos ya correctos.
            _lastHealth = float.NaN;
            _lastMaxHealth = float.NaN;
            _lastWaveIndex = int.MinValue;
            _lastEnemiesLeft = int.MinValue;
        }

        private void HandleSpawning()
        {
            var spawnGroup = GetSpawnGroupForCurrentRoom();
            if (spawnGroup == null || spawnGroup.SpawnPoints == null || spawnGroup.SpawnPoints.Count == 0)
            {
                if (!_loggedMissingSpawnGroup)
                {
                    Debug.LogError($"GameManager: no hay RoomSpawnGroup con spawnpoints para RoomId '{_roomSystem.CurrentRoomId}'.");
                    _loggedMissingSpawnGroup = true;
                }
                return;
            }

            if (_waveSystem.TryGetNextEnemyType(_enemySystem.AliveCount, out var enemyType))
            {
                SpawnEnemy(spawnGroup, enemyType);
            }
        }

        private RoomSpawnGroup GetSpawnGroupForCurrentRoom()
        {
            string roomId = _roomSystem.CurrentRoomId;
            if (roomId == null || _roomSpawnGroups == null)
                return null;

            for (int i = 0; i < _roomSpawnGroups.Count; i++)
            {
                var group = _roomSpawnGroups[i];
                if (group != null && group.RoomId == roomId)
                    return group;
            }

            return null;
        }

        private void SpawnEnemy(RoomSpawnGroup spawnGroup, EnemyTypeData enemyType)
        {
            // Spawnpoint random dentro del RoomSpawnGroup de la room actual.
            int index = UnityEngine.Random.Range(0, spawnGroup.SpawnPoints.Count);
            var spawnPoint = spawnGroup.SpawnPoints[index];

            // Spawn en X/Z del spawnpoint, Y fijada al plano de juego (Y del player).
            Vector3 spawnPosition = spawnPoint.position;
            spawnPosition.y = _playerModel.Position.y;

            var enemyModel = _enemySystem.CreateEnemy(enemyType);
            enemyModel.Position = spawnPosition;

            var view = _objectPool.Spawn("Enemy", spawnPosition);
            if (view != null)
            {
                _enemyViews[enemyModel] = view;
            }
        }

        private void HandleCombat()
        {
            _enemyDamageTimer += Time.deltaTime;

            for (int i = _projectileSystem.Projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectileSystem.Projectiles[i];
                bool projectileHit = false;

                for (int j = _enemySystem.Enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = _enemySystem.Enemies[j];
                    bool hit = _combatSystem.ResolveProjectileEnemyCollision(projectile, enemy);

                    if (!enemy.IsAlive && _enemyViews.ContainsKey(enemy))
                    {
                        var enemyView = _enemyViews[enemy];
                        _objectPool.Despawn("Enemy", enemyView);
                        _enemyViews.Remove(enemy);
                        _enemySystem.RemoveEnemy(enemy);
                    }

                    if (hit)
                    {
                        projectileHit = true;
                        break;
                    }
                }

                if (projectileHit || projectile.HasReachedMaxDistance)
                {
                    if (_projectileViews.ContainsKey(projectile))
                    {
                        var projectileView = _projectileViews[projectile];
                        _objectPool.Despawn("Projectile", projectileView);
                        _projectileViews.Remove(projectile);
                    }
                    _projectileSystem.RemoveProjectile(projectile);
                }
            }

            if (_enemyDamageTimer >= EnemyDamageInterval)
            {
                _enemyDamageTimer = 0;

                for (int i = _enemySystem.Enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = _enemySystem.Enemies[i];
                    if (_combatSystem.CheckPlayerEnemyCollision(_playerModel.Position, enemy.Position))
                    {
                        _combatSystem.ApplyEnemyDamageToPlayer(enemy, _playerModel);
                    }
                }
            }
        }

        private void HandleGameState()
        {
            if (!_playerModel.IsAlive)
            {
                _gameOver = true;
                Debug.Log("GAME OVER - PLAYER DEFEATED");
                return;
            }

            _waveSystem.UpdateProgress(_enemySystem.AliveCount);

            if (_waveSystem.IsRoomComplete)
            {
                if (_roomSystem.TryAdvanceToNextRoom())
                {
                    _loggedMissingSpawnGroup = false;
                    _waveSystem.StartRoom(_roomSystem.CurrentRoom);
                }
                else
                {
                    _playerWon = true;
                    _gameOver = true;
                    Debug.Log("VICTORY - ALL ROOMS COMPLETED");
                }
            }
        }

        private void UpdateViews()
        {
            _playerTransform.position = _playerModel.Position;

            foreach (var kvp in _enemyViews)
            {
                kvp.Value.SetPosition(kvp.Key.Position);
            }

            foreach (var kvp in _projectileViews)
            {
                kvp.Value.SetPosition(kvp.Key.Position);
            }
        }

        public void FireProjectile()
        {
            if (!_playerSystem.CanFire())
                return;

            _playerSystem.Fire();
            var projectile = _playerSystem.CreateProjectile(_projectileSystem.Projectiles.Count);
            _projectileSystem.AddProjectile(projectile);

            var view = _objectPool.Spawn("Projectile", projectile.Position);
            if (view != null)
            {
                view.SetRotation(Quaternion.LookRotation(_playerSystem.GetFireDirection()));
                _projectileViews[projectile] = view;
            }
        }

        public void SetPlayerInput(Vector3 moveInput)
        {
            _playerSystem.SetMoveInput(moveInput);
        }

        public void SetPlayerLook(Vector3 direction)
        {
            _playerSystem.SetLookDirection(direction);
        }

        public PlayerModel GetPlayerModel() => _playerModel;
    }
}
