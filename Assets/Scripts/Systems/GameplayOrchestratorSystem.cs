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

        // --- Telegraph de spawn: la marca en el piso que avisa dónde spawnea el enemigo. ---
        // Referencias compartidas creadas por GameManager (mismo patrón que pickups/enemigos).
        private readonly SpawnTelegraphSystem _spawnTelegraphSystem;
        private readonly Dictionary<SpawnMarkerModel, EntityView> _spawnMarkerViews;
        private const string SpawnMarkerPoolKey = "SpawnMarker";
        private bool _loggedMissingSpawnMarkerPool;
        // Pequeño offset en Y para apoyar la marca sobre el piso sin z-fighting.
        private const float SpawnMarkerFloorOffsetY = 0.05f;

        // --- Pickups (Bloque C). Referencias compartidas creadas por GameManager. ---
        private readonly PickupSystem _pickupSystem;
        private readonly Dictionary<PickupModel, EntityView> _pickupViews;
        // Aplica speed boost en PlayerSystem sin acoplar el orquestador a ese tipo.
        // (multiplier, duration, displayName, icon)
        private readonly Action<float, float, string, Sprite> _applySpeedBoost;
        // Aplica un arma temporal en PlayerSystem sin acoplar el orquestador a ese tipo.
        // (weapon, duration, displayName, icon)
        private readonly Action<WeaponData, float, string, Sprite> _applyTemporaryWeapon;
        // Lee el estado del powerup activo (Speed) sin acoplar al tipo PlayerSystem.
        // Devuelve (active, displayName, icon, remaining, duration).
        private readonly Func<(bool active, string name, Sprite icon, float remaining, float duration)> _getPowerUpState;
        // Notifica el estado del powerup a la UI (vía evento de GameManager).
        private readonly Action<bool, string, Sprite, float, float> _notifyPowerUpChanged;
        // Diff para emitir un único "inactive" al expirar y evitar spam estando apagado.
        private bool _lastPowerUpActive;

        // Estado del arma temporal (HUD separado). Mismo patrón que PowerUp.
        // Devuelve (active, name, icon, remaining, duration).
        private readonly Func<(bool active, string name, Sprite icon, float remaining, float duration)> _getTemporaryWeaponState;
        private readonly Action<bool, string, Sprite, float, float> _notifyTemporaryWeaponChanged;
        private bool _lastTemporaryWeaponActive;

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
        // bool = victory (true) / defeat (false). GameManager emite el evento de UI según el flag.
        private readonly Action<bool> _endGameplay;

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
            SpawnTelegraphSystem spawnTelegraphSystem,
            Dictionary<SpawnMarkerModel, EntityView> spawnMarkerViews,
            PickupSystem pickupSystem,
            Dictionary<PickupModel, EntityView> pickupViews,
            Action<float, float, string, Sprite> applySpeedBoost,
            Action<WeaponData, float, string, Sprite> applyTemporaryWeapon,
            Func<(bool active, string name, Sprite icon, float remaining, float duration)> getPowerUpState,
            Action<bool, string, Sprite, float, float> notifyPowerUpChanged,
            Func<(bool active, string name, Sprite icon, float remaining, float duration)> getTemporaryWeaponState,
            Action<bool, string, Sprite, float, float> notifyTemporaryWeaponChanged,
            Action<float, float> raiseHealthChanged,
            Action<string, int, int, bool> raiseWaveChanged,
            Action<int> raiseEnemiesLeftChanged,
            Action<bool> endGameplay)
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
            _spawnTelegraphSystem = spawnTelegraphSystem;
            _spawnMarkerViews = spawnMarkerViews;
            _pickupSystem = pickupSystem;
            _pickupViews = pickupViews;
            _applySpeedBoost = applySpeedBoost;
            _applyTemporaryWeapon = applyTemporaryWeapon;
            _getPowerUpState = getPowerUpState;
            _notifyPowerUpChanged = notifyPowerUpChanged;
            _getTemporaryWeaponState = getTemporaryWeaponState;
            _notifyTemporaryWeaponChanged = notifyTemporaryWeaponChanged;
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
            // Telegraph: titileo de las marcas activas y resolución de las que ya vencieron
            // (spawn del enemigo en su posición). SpawnTelegraphSystem ya tickeó su countdown
            // antes que el orquestador, así que ReadyMarkers está al día en este frame.
            UpdateSpawnMarkerVisuals();
            ResolveReadySpawnMarkers();
            HandleCombat(deltaTime);

            // Procesa pickups recogidos por PickupSystem (que ya tickeó antes en el frame)
            // ANTES de RefreshHud, para que un Heal emita HealthChanged en el mismo frame.
            HandlePickups();
            // Expirados DESPUÉS de recogidos: la recolección ya removió de ActivePickups
            // los que se juntaron este frame, así que no hay doble procesamiento.
            HandleExpiredPickups();
            // Titileo de los pickups que siguen en el piso.
            UpdatePickupVisuals();

            // Notifica a la UI el estado del powerup activo (Speed). Pasa por el Tick central
            // (no es Update). PlayerSystem ya tickeó antes este frame, así que remaining está al día.
            NotifyPowerUpState();
            // Mismo patrón para el arma temporal (HUD separado del PowerUp).
            NotifyTemporaryWeaponState();

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

            // Suma las marcas en vuelo: un enemigo ya sacado de la cola pero todavía en
            // telegraph no está "vivo" ni "pendiente", así que sin esto el HUD parpadearía.
            int enemiesLeft = _waveSystem.PendingToSpawnCount + _enemySystem.AliveCount + _spawnTelegraphSystem.ActiveCount;
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

            // El pacing cuenta las marcas en vuelo como enemigos ya "reservados": una marca
            // se convertirá en enemigo en 1s, así que suma al conteo de vivos para respetar
            // MaxEnemiesAlive y no llenar la sala de marcas.
            int reservedCount = _enemySystem.AliveCount + _spawnTelegraphSystem.ActiveCount;
            if (_waveSystem.TryGetNextEnemyType(reservedCount, out var enemyType))
            {
                SpawnMarker(spawnGroup, enemyType);
            }
        }

        // Elige un spawn point y coloca la MARCA (telegraph) en el piso. El enemigo NO
        // aparece todavía: lo hace ResolveReadySpawnMarkers cuando el countdown termina.
        private void SpawnMarker(RoomSpawnGroup spawnGroup, EnemyTypeData enemyType)
        {
            int index = UnityEngine.Random.Range(0, spawnGroup.SpawnPoints.Count);
            var spawnPoint = spawnGroup.SpawnPoints[index];

            Vector3 spawnPosition = spawnPoint.position;
            spawnPosition.y = _playerModel.Position.y;

            var marker = _spawnTelegraphSystem.CreateMarker(enemyType, spawnPosition);

            // Marca visible sobre el piso (con un pequeño offset para evitar z-fighting).
            Vector3 markerViewPosition = spawnPosition;
            markerViewPosition.y += SpawnMarkerFloorOffsetY;

            var view = _objectPool.Spawn(SpawnMarkerPoolKey, markerViewPosition);
            if (view != null)
            {
                view.SetVisible(true);
                _spawnMarkerViews[marker] = view;
            }
            else if (!_loggedMissingSpawnMarkerPool)
            {
                // Pool no registrado (sin prefab de marca): degradá sin crashear. El enemigo
                // igual va a spawnear cuando venza el countdown; solo falta el aviso visual.
                Debug.LogWarning("GameplayOrchestratorSystem: pool 'SpawnMarker' sin prefab. El telegraph no se mostrará (el spawn del enemigo sigue funcionando).");
                _loggedMissingSpawnMarkerPool = true;
            }
        }

        // Aplica el titileo (que acelera) a las marcas todavía activas, leyendo
        // ShouldBeVisible del modelo. Mismo patrón que UpdatePickupVisuals.
        private void UpdateSpawnMarkerVisuals()
        {
            var active = _spawnTelegraphSystem.ActiveMarkers;
            for (int i = 0; i < active.Count; i++)
            {
                var marker = active[i];
                if (_spawnMarkerViews.TryGetValue(marker, out var view))
                    view.SetVisible(marker.ShouldBeVisible);
            }
        }

        // Para cada marca cuyo countdown terminó este frame: devuelve su view al pool y
        // spawnea el enemigo en la posición marcada. Luego limpia la lista en el sistema.
        private void ResolveReadySpawnMarkers()
        {
            var ready = _spawnTelegraphSystem.ReadyMarkers;
            if (ready.Count == 0)
                return;

            for (int i = 0; i < ready.Count; i++)
            {
                var marker = ready[i];

                if (_spawnMarkerViews.TryGetValue(marker, out var view))
                {
                    // Restaurar visible antes de devolver al pool (el titileo pudo dejarla oculta).
                    view.SetVisible(true);
                    _objectPool.Despawn(SpawnMarkerPoolKey, view);
                    _spawnMarkerViews.Remove(marker);
                }

                SpawnEnemyAt(marker.Position, marker.EnemyType);
            }

            _spawnTelegraphSystem.ClearReadyMarkers();
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

        // Spawn REAL del enemigo en una posición ya decidida (la de la marca vencida).
        private void SpawnEnemyAt(Vector3 spawnPosition, EnemyTypeData enemyType)
        {
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

                    if (hit)
                    {
                        // Daño en área: además del impacto directo, daña a los demás enemigos
                        // dentro del radio. excludedEnemy = enemy: ya recibió el daño directo,
                        // no debe recibirlo dos veces. CombatSystem solo aplica daño; las
                        // muertes (directa o por área) se procesan abajo en ProcessDeadEnemies.
                        if (projectile.HasAreaDamage)
                        {
                            _combatSystem.ApplyAreaDamage(
                                projectile.Position,
                                projectile.AreaRadius,
                                projectile.Damage,
                                _enemySystem.Enemies,
                                enemy);
                        }

                        projectileHit = true;
                        break;
                    }
                }

                if (projectileHit || projectile.HasReachedMaxDistance)
                {
                    if (_projectileViews.ContainsKey(projectile))
                    {
                        var projectileView = _projectileViews[projectile];
                        // Devolver la view a la MISMA cola desde la que se spawneó. La key
                        // viaja en el modelo (default "Projectile" o "Projectile_<WeaponId>").
                        _objectPool.Despawn(projectile.PoolKey, projectileView);
                        _projectileViews.Remove(projectile);
                    }
                    _projectileSystem.RemoveProjectile(projectile);
                }
            }

            // Procesa TODAS las muertes (impacto directo o área) en un único barrido, después
            // de resolver los proyectiles. Garantiza un solo despawn por enemigo y captura los
            // muertos por área aunque el proyectil ya haya hecho break en el loop anterior.
            ProcessDeadEnemies();

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

        // Barrido único de enemigos muertos: tira drop, devuelve la view al pool y remueve
        // el modelo. Captura muertes por impacto directo Y por daño en área. Iteración reversa
        // para poder remover sin saltar índices. Un enemigo solo se procesa una vez: tras
        // RemoveEnemy/_enemyViews.Remove ya no vuelve a entrar.
        private void ProcessDeadEnemies()
        {
            for (int i = _enemySystem.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemySystem.Enemies[i];
                if (enemy.IsAlive || !_enemyViews.ContainsKey(enemy))
                    continue;

                // Tirar drop al morir y, si sale, spawnear pickup visible en su posición.
                if (_dropSystem.TryRollDrop(enemy.DropTable, out var pickupData))
                {
                    SpawnPickup(pickupData, enemy.Position);
                }

                var enemyView = _enemyViews[enemy];
                _objectPool.Despawn("Enemy", enemyView);
                _enemyViews.Remove(enemy);
                _enemySystem.RemoveEnemy(enemy);
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
            // Si el PickupData tiene ícono y el prefab usa SpriteRenderer, mostrarlo.
            // Si no hay ícono o no hay SpriteRenderer, queda el color/debug (no crashea).
            view.SetSprite(data.Icon);
            // La view viene del pool: pudo quedar oculta por el titileo de un uso anterior.
            // Garantizar que arranca visible.
            view.SetVisible(true);

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
                    // Restaurar visible antes de devolver al pool (el titileo pudo dejarlo oculto).
                    view.SetVisible(true);
                    _objectPool.Despawn("Pickup", view);
                    _pickupViews.Remove(model);
                }
            }

            _pickupSystem.ClearCollectedPickups();
        }

        // Procesa pickups EXPIRADOS (no recogidos): NO aplica efecto, solo devuelve la view
        // al pool y limpia el mapping. La recolección ya tuvo prioridad en PickupSystem.Tick.
        private void HandleExpiredPickups()
        {
            if (_pickupSystem == null)
                return;

            var expired = _pickupSystem.ExpiredPickups;
            if (expired.Count == 0)
                return;

            for (int i = 0; i < expired.Count; i++)
            {
                var model = expired[i];
                if (_pickupViews.TryGetValue(model, out var view))
                {
                    view.SetVisible(true);
                    _objectPool.Despawn("Pickup", view);
                    _pickupViews.Remove(model);
                }
            }

            _pickupSystem.ClearExpiredPickups();
        }

        // Aplica el titileo a los pickups todavía activos leyendo ShouldBeVisible del modelo.
        private void UpdatePickupVisuals()
        {
            if (_pickupSystem == null)
                return;

            var active = _pickupSystem.ActivePickups;
            for (int i = 0; i < active.Count; i++)
            {
                var model = active[i];
                if (_pickupViews.TryGetValue(model, out var view))
                    view.SetVisible(model.ShouldBeVisible);
            }
        }

        // Empuja a la UI el estado del powerup activo. Mientras hay Speed activo, actualiza
        // cada frame (aceptable: pasa por el Tick central, no por Update). Cuando expira,
        // emite UNA sola vez "inactive" y deja de spamear.
        private void NotifyPowerUpState()
        {
            if (_getPowerUpState == null || _notifyPowerUpChanged == null)
                return;

            var state = _getPowerUpState();

            if (state.active)
            {
                _lastPowerUpActive = true;
                _notifyPowerUpChanged(true, state.name, state.icon, state.remaining, state.duration);
            }
            else if (_lastPowerUpActive)
            {
                // Transición activo -> inactivo: emitir una vez para que la UI se oculte.
                _lastPowerUpActive = false;
                _notifyPowerUpChanged(false, null, null, 0f, 0f);
            }
        }

        // Empuja a la UI el estado del arma temporal. Mientras hay arma activa, actualiza cada
        // frame (pasa por el Tick central, no por Update) para que el fill baje. Al expirar,
        // emite UNA sola vez "inactive" y deja de spamear (diff con _lastTemporaryWeaponActive).
        private void NotifyTemporaryWeaponState()
        {
            if (_getTemporaryWeaponState == null || _notifyTemporaryWeaponChanged == null)
                return;

            var state = _getTemporaryWeaponState();

            if (state.active)
            {
                _lastTemporaryWeaponActive = true;
                _notifyTemporaryWeaponChanged(true, state.name, state.icon, state.remaining, state.duration);
            }
            else if (_lastTemporaryWeaponActive)
            {
                _lastTemporaryWeaponActive = false;
                _notifyTemporaryWeaponChanged(false, null, null, 0f, 0f);
            }
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
                    // Pasa nombre e ícono del PickupData para que la UI muestre el powerup activo.
                    _applySpeedBoost?.Invoke(data.Amount, data.Duration, data.DisplayName, data.Icon);
                    break;
                case PickupKind.Weapon:
                    // Arma temporal: usa PickupData.Duration (NO WeaponData.Duration). El HUD
                    // de arma se conecta en Sub-bloque 5; acá solo se cambia el arma activa.
                    if (data.WeaponData != null)
                        _applyTemporaryWeapon?.Invoke(data.WeaponData, data.Duration, data.DisplayName, data.Icon);
                    else
                        Debug.LogWarning("GameplayOrchestratorSystem: PickupKind.Weapon sin WeaponData asignado. No se aplica arma temporal.");
                    break;
                // Shield: bloque futuro. No-op por ahora.
            }
        }

        private void HandleGameState()
        {
            if (!_playerModel.IsAlive)
            {
                _gameState = GameState.Defeat;
                _endGameplay?.Invoke(false);
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
                    _endGameplay?.Invoke(true);
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
