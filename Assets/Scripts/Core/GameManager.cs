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
        [SerializeField] private List<Transform> _spawnPoints = new();
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private CustomUpdateManager _updateManager;

        private PlayerSystem _playerSystem;
        private EnemySystem _enemySystem;
        private ProjectileSystem _projectileSystem;
        private WaveSystem _waveSystem;
        private CombatSystem _combatSystem;
        private ObjectPool _objectPool;

        private PlayerModel _playerModel;
        private Dictionary<EnemyModel, MonoBehaviours.EntityView> _enemyViews = new();
        private Dictionary<ProjectileModel, MonoBehaviours.EntityView> _projectileViews = new();

        private int _nextSpawnPointIndex;
        private float _spawnCheckTimer;
        private bool _gameOver;
        private bool _playerWon;

        private const float SpawnCheckInterval = 0.1f;
        private const float EnemyDamageInterval = 1f;
        private float _enemyDamageTimer;

        private void Awake()
        {
            InitializeSystems();
            InitializePools();
            RegisterSystems();
        }

        private void Start()
        {
            _waveSystem.StartWaves();
        }

        private void InitializeSystems()
        {
            var playerConfig = new PlayerConfig();
            _playerModel = new PlayerModel(playerConfig.MaxHealth, playerConfig.MoveSpeed);
            _playerModel.Position = _playerTransform.position;

            _playerSystem = new PlayerSystem(_playerModel, playerConfig);
            _enemySystem = new EnemySystem(new EnemyTypeData(), () => _playerModel.Position);
            _projectileSystem = new ProjectileSystem();
            _waveSystem = new WaveSystem(new WaveConfig(), totalWaves: 5);
            _combatSystem = new CombatSystem();
        }

        private void InitializePools()
        {
            _objectPool = new ObjectPool();
            _objectPool.RegisterPrefab("Enemy", _enemyPrefab);
            _objectPool.RegisterPrefab("Projectile", _projectilePrefab);

            _objectPool.Prewarm("Enemy", 64);
            _objectPool.Prewarm("Projectile", 256);
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
            if (_gameOver)
                return;

            HandleSpawning();
            HandleCombat();
            HandleGameState();
            UpdateViews();
        }

        private void HandleSpawning()
        {
            _spawnCheckTimer += Time.deltaTime;
            if (_spawnCheckTimer < SpawnCheckInterval)
                return;

            _spawnCheckTimer = 0;

            if (_waveSystem.ShouldSpawnEnemy() && _enemySystem.AliveCount < 15)
            {
                SpawnEnemy();
            }
        }

        private void SpawnEnemy()
        {
            if (_spawnPoints.Count == 0)
                return;

            var spawnPoint = _spawnPoints[_nextSpawnPointIndex % _spawnPoints.Count];
            _nextSpawnPointIndex++;

            // Spawn en X/Z del spawnpoint, Y fijada al plano de juego (Y del player).
            Vector3 spawnPosition = spawnPoint.position;
            spawnPosition.y = _playerModel.Position.y;

            var enemyModel = _enemySystem.CreateEnemy();
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
            _waveSystem.MarkWaveEnemiesClearedIfReady(_enemySystem.AliveCount);

            if (!_playerModel.IsAlive)
            {
                _gameOver = true;
                Debug.Log("GAME OVER - PLAYER DEFEATED");
            }

            if (_waveSystem.AllWavesCompleted && _enemySystem.AliveCount == 0)
            {
                _playerWon = true;
                _gameOver = true;
                Debug.Log("VICTORY - ALL WAVES COMPLETED");
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
