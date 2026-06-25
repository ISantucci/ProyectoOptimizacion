using System;
using System.Collections.Generic;
using OptimizationGame.Data;
using OptimizationGame.Interfaces;
using OptimizationGame.Models;
using OptimizationGame.MonoBehaviours;
using UnityEngine;

namespace OptimizationGame.Systems
{
    /// <summary>
    /// Sistema puro (NO MonoBehaviour) que ejecuta la lógica recurrente de gameplay
    /// que antes vivía en GameManager.Update(): spawn, combate, estado de juego,
    /// sync de modelos->views y refresh de HUD. Se registra en CustomUpdateManager y
    /// corre por ITickable.Tick(dt), DESPUÉS de los sistemas de simulación
    /// (Player/Enemy/Projectile/Wave), para leer el estado ya actualizado del frame.
    ///
    /// OWNERSHIP: este sistema NO crea pool, views ni eventos. Recibe por constructor
    /// las MISMAS instancias creadas por GameManager (ObjectPool, diccionarios
    /// modelo->EntityView, spawn groups, transform del player) y delegates para HUD y
    /// fin de juego. No hay copias ni doble pool. El despawn está coordinado en un único
    /// lugar (este Tick): cada vez que se devuelve una view al pool, se quita su mapping
    /// y se remueve el modelo del sistema, en el mismo paso. No hay views huérfanas ni
    /// doble retorno al pool.
    ///
    /// Dependencia de Transform/EntityView justificada: este sistema es el puente
    /// simulación->vista; su responsabilidad explícita es sincronizar views. No contiene
    /// reglas de presentación visual (eso queda en EntityView/UIManager).
    /// </summary>
    public class GameplayOrchestratorSystem : ITickable
    {
        private enum GameState { Playing, Victory, Defeat }
        private GameState _gameState = GameState.Playing;

        // --- Simulación (modelos/sistemas puros) ---
        private readonly PlayerModel _playerModel;
        private readonly EnemySystem _enemySystem;
        private readonly ProjectileSystem _projectileSystem;
        private readonly WaveSystem _waveSystem;
        private readonly RoomSystem _roomSystem;
        private readonly CombatSystem _combatSystem;

        // --- Recursos propiedad de GameManager (referencias compartidas, no copias) ---
        private readonly ObjectPool _objectPool;
        private readonly Dictionary<EnemyModel, EntityView> _enemyViews;
        private readonly Dictionary<ProjectileModel, EntityView> _projectileViews;
        private readonly List<RoomSpawnGroup> _roomSpawnGroups;
        private readonly Transform _playerTransform;

        // --- Pickups (Bloque C). Referencias compartidas creadas por GameManager. ---
        private readonly PickupSystem _pickupSystem;
        private readonly Dictionary<PickupModel, EntityView> _pickupViews;
        // Aplica speed boost en PlayerSystem sin acoplar el orquestador a ese tipo.
        private readonly Action<float, float> _applySpeedBoost;

        private int _nextPickupId;
        private bool _loggedMissingPickupPool;

        // Radio de recogida del pickup (XZ).
        private const float PickupCollectRadius = 1.5f;

        // DropSystem: clase pura, stateless. Instanciado aquí para no tocar el wiring de
        // GameManager. NO es ITickable; solo se invoca puntualmente al morir un enemigo.
        private readonly DropSystem _dropSystem = new DropSystem();

        // --- Salidas hacia GameManager (sin acoplar al tipo MonoBehaviour) ---
        private readonly Action<float, float> _raiseHealthChanged;
        private readonly Action<string, int, int, bool> _raiseWaveChanged;
        private readonly Action<int> _raiseEnemiesLeftChanged;
        private readonly Action _endGameplay;

        // Cooldown de ataque por enemigo (el timer vive en cada EnemyModel).
        private const float EnemyDamageInterval = 1f;

        // Cache de últimos valores enviados al HUD (push por diff).
        private float _lastHealth = float.NaN;
        private float _lastMaxHealth = float.NaN;
        private int _lastWaveIndex = int.MinValue;
        private int _lastEnemiesLeft = int.MinValue;

        private bool _loggedMissingSpawnGroup;

        public GameplayOrchestratorSystem(
            PlayerModel playerModel,
            EnemySystem enemySystem,
            ProjectileSystem projectileSystem,
            WaveSystem waveSystem,
            RoomSystem roomSystem,
            CombatSystem combatSystem,
            ObjectPool objectPool,
            Dictionary<EnemyModel, EntityView> enemyViews,
            Dictionary<ProjectileModel, EntityView> projectileViews,
            List<RoomSpawnGroup> roomSpawnGroups,
            Transform playerTransform,
            PickupSystem pickupSystem,
            Dictionary<PickupModel, EntityView> pickupViews,
            Action<float, float> applySpeedBoost,
            Action<float, float> raiseHealthChanged,
            Action<string, int, int, bool> raiseWaveChanged,
            Action<int> raiseEnemiesLeftChanged,
            Action endGameplay)
        {
            _playerModel = playerModel;
            _enemySystem = enemySystem;
            _projectileSystem = projectileSystem;
            _waveSystem = waveSystem;
            _roomSystem = roomSystem;
            _combatSystem = combatSystem;
            _objectPool = objectPool;
            _enemyViews = enemyViews;
            _projectileViews = projectileViews;
            _roomSpawnGroups = roomSpawnGroups;
            _playerTransform = playerTransform;
            _pickupSystem = pickupSystem;
            _pickupViews = pickupViews;
            _applySpeedBoost = applySpeedBoost;
            _raiseHealthChanged = raiseHealthChanged;
            _raiseWaveChanged = raiseWaveChanged;
            _raiseEnemiesLeftChanged = raiseEnemiesLeftChanged;
            _endGameplay = endGameplay;
        }

        public void Tick(float deltaTime)
        {
            // Tras Victory/Defeat el CustomUpdateManager se pausa, así que normalmente
            // no se vuelve a tickear. Este guard es defensivo.
            if (_gameState != GameState.Playing)
                return;

            HandleSpawning();
            HandleCombat(deltaTime);

            // Procesa pickups recogidos por PickupSystem (que ya tickeó antes en el frame)
            // ANTES de RefreshHud, para que un Heal emita HealthChanged en el mismo frame.
            HandlePickups();

            // RefreshHud DESPUÉS del combate: garantiza que en el frame de muerte el HUD
            // emita vida = 0 ANTES de que HandleGameState pause el loop.
            RefreshHud();
            UpdateViews();

            // Último: puede terminar el juego y pausar el CustomUpdateManager.
            HandleGameState();
        }

        /// <summary>
        /// Resetea las caches del HUD a sentinela. La llama GameManager.BroadcastInitialUiState
        /// (UI init) para que el primer Tick re-emita con datos ya correctos.
        /// </summary>
        public void ResetHudCaches()
        {
            _lastHealth = float.NaN;
            _lastMaxHealth = float.NaN;
            _lastWaveIndex = int.MinValue;
            _lastEnemiesLeft = int.MinValue;
        }

        private void RefreshHud()
        {
            float health = _playerModel.Health;
            float maxHealth = _playerModel.MaxHealth;
            if (health != _lastHealth || maxHealth != _lastMaxHealth)
            {
                _lastHealth = health;
                _lastMaxHealth = maxHealth;
                _raiseHealthChanged?.Invoke(health, maxHealth);
            }

            int waveIndex = _waveSystem.CurrentWaveIndex;
            if (waveIndex != _lastWaveIndex)
            {
                _lastWaveIndex = waveIndex;
                _raiseWaveChanged?.Invoke(
                    _waveSystem.CurrentWaveName,
                    waveIndex,
                    _waveSystem.TotalWaves,
                    _waveSystem.IsFinalWave);
            }

            int enemiesLeft = _waveSystem.PendingToSpawnCount + _enemySystem.AliveCount;
            if (enemiesLeft != _lastEnemiesLeft)
            {
                _lastEnemiesLeft = enemiesLeft;
                _raiseEnemiesLeftChanged?.Invoke(enemiesLeft);
            }
        }

        private void HandleSpawning()
        {
            var spawnGroup = GetSpawnGroupForCurrentRoom();
            if (spawnGroup == null || spawnGroup.SpawnPoints == null || spawnGroup.SpawnPoints.Count == 0)
            {
                if (!_loggedMissingSpawnGroup)
                {
                    Debug.LogError($"GameplayOrchestratorSystem: no hay RoomSpawnGroup con spawnpoints para RoomId '{_roomSystem.CurrentRoomId}'.");
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
            int index = UnityEngine.Random.Range(0, spawnGroup.SpawnPoints.Count);
            var spawnPoint = spawnGroup.SpawnPoints[index];

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

        private void HandleCombat(float deltaTime)
        {
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
                        // Bloque C: tirar drop al morir y, si sale, spawnear pickup visible
                        // en la posición del enemigo. La DropTable viaja en el EnemyModel.
                        if (_dropSystem.TryRollDrop(enemy.DropTable, out var pickupData))
                        {
                            SpawnPickup(pickupData, enemy.Position);
                        }

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

            // Daño al player con cooldown por enemigo (timer en cada EnemyModel).
            for (int i = _enemySystem.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemySystem.Enemies[i];
                enemy.TickAttackCooldown(deltaTime);

                if (enemy.CanAttack &&
                    _combatSystem.CheckPlayerEnemyCollision(_playerModel.Position, enemy.Position))
                {
                    _combatSystem.ApplyEnemyDamageToPlayer(enemy, _playerModel);
                    enemy.RegisterAttack(EnemyDamageInterval);
                }
            }
        }

        // Crea el PickupModel, pide una EntityView al pool ("Pickup") y la registra.
        // Si el pool no tiene la key "Pickup" configurada, degrada con warning (no crashea).
        private void SpawnPickup(PickupData data, Vector3 position)
        {
            if (data == null || _pickupSystem == null)
                return;

            var view = _objectPool.Spawn("Pickup", position);
            if (view == null)
            {
                if (!_loggedMissingPickupPool)
                {
                    Debug.LogWarning("GameplayOrchestratorSystem: pool 'Pickup' no configurado (prefab faltante). El drop no se mostrará.");
                    _loggedMissingPickupPool = true;
                }
                return;
            }

            view.SetColor(data.DebugColor);

            var model = new PickupModel(_nextPickupId++, data, position, PickupCollectRadius);
            _pickupViews[model] = view;
            _pickupSystem.AddPickup(model);
        }

        // Aplica los efectos de los pickups recogidos, devuelve sus views al pool y limpia.
        private void HandlePickups()
        {
            if (_pickupSystem == null)
                return;

            var collected = _pickupSystem.CollectedPickups;
            if (collected.Count == 0)
                return;

            for (int i = 0; i < collected.Count; i++)
            {
                var model = collected[i];
                ApplyPickupEffect(model.Data);

                if (_pickupViews.TryGetValue(model, out var view))
                {
                    _objectPool.Despawn("Pickup", view);
                    _pickupViews.Remove(model);
                }
            }

            _pickupSystem.ClearCollectedPickups();
        }

        private void ApplyPickupEffect(PickupData data)
        {
            if (data == null)
                return;

            switch (data.Kind)
            {
                case PickupKind.Heal:
                    // El HealthChanged se emite en RefreshHud (push por diff) este mismo frame.
                    _playerModel.Heal(data.Amount);
                    break;
                case PickupKind.Speed:
                    _applySpeedBoost?.Invoke(data.Amount, data.Duration);
                    break;
                // Weapon/Shield: bloque futuro. No-op por ahora.
            }
        }

        private void HandleGameState()
        {
            if (!_playerModel.IsAlive)
            {
                _gameState = GameState.Defeat;
                _endGameplay?.Invoke();
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
                    _gameState = GameState.Victory;
                    _endGameplay?.Invoke();
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
    }
}
