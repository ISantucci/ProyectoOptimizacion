using System;
using System.Collections.Generic;
using OptimizationGame.Data;
using OptimizationGame.Models;
using OptimizationGame.Systems;
using UnityEngine;

namespace OptimizationGame.Core
{
    /// <summary>
    /// Composition root. Crea sistemas/modelos/pool, hace el wiring, registra los
    /// ITickable en el CustomUpdateManager y es dueño de los recursos de presentación
    /// (ObjectPool, mappings modelo->EntityView, eventos de HUD).
    ///
    /// NO ejecuta lógica recurrente: no tiene Update/FixedUpdate/LateUpdate. Toda la
    /// lógica recurrente de gameplay vive en GameplayOrchestratorSystem (clase pura),
    /// que corre por CustomUpdateManager. Aquí solo quedan: inicialización (Awake),
    /// callbacks no recurrentes invocados por InputReader y el broadcast inicial de UI.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private GameObject _projectilePrefab;
        // Bloque C: prefab visual del pickup (debe tener EntityView). Opcional: si queda
        // sin asignar, los drops no se mostrarán pero el juego no crashea (warning).
        [SerializeField] private GameObject _pickupPrefab;
        [SerializeField] private CustomUpdateManager _updateManager;
        // Cámara usada por InputReader para el raycast de aim. InputReader ya no es
        // componente, así que la cámara se asigna acá por Inspector (no Camera.main en loop).
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private PoolConfig _poolConfig;

        // Arma base data-driven. Si queda sin asignar, el disparo cae a los valores
        // legacy de PlayerConfig (ver InitializeSystems / PlayerSystem).
        [SerializeField] private WeaponData _baseWeapon;

        // Flujo data-driven: GameFlowConfig define QUÉ pasa; RoomSpawnGroups, DÓNDE.
        [SerializeField] private GameFlowConfig _gameFlowConfig;
        [SerializeField] private List<RoomSpawnGroup> _roomSpawnGroups = new();

        // Menú de inicio OPT-IN. Default false = flujo actual intacto (arranca solo).
        // Si se activa, el gameplay arranca pausado mostrando el Start Panel hasta que
        // un botón llame StartGameFromUI(). El botón se conecta a mano en Unity.
        [SerializeField] private bool _startWithMenu = false;

        private PlayerSystem _playerSystem;
        private EnemySystem _enemySystem;
        private ProjectileSystem _projectileSystem;
        private WaveSystem _waveSystem;
        private RoomSystem _roomSystem;
        private CombatSystem _combatSystem;
        private PickupSystem _pickupSystem;
        private ObjectPool _objectPool;
        private GameplayOrchestratorSystem _orchestrator;

        // InputReader ya no es MonoBehaviour: GameManager la crea, inicializa y posee.
        private MonoBehaviours.InputReader _inputReader;

        private PlayerModel _playerModel;
        private Dictionary<EnemyModel, MonoBehaviours.EntityView> _enemyViews = new();
        private Dictionary<ProjectileModel, MonoBehaviours.EntityView> _projectileViews = new();
        private Dictionary<PickupModel, MonoBehaviours.EntityView> _pickupViews = new();

        // Bloqueo de gameplay tras Victory/Defeat. Lo activa el orquestador vía EndGameplay().
        // GameManager es dueño de la pausa del loop y del bloqueo del disparo.
        private bool _gameplayEnded;

        // Estado mínimo de juego. Gobierna qué transiciones de UI son válidas:
        // solo se pausa/despausa desde Playing/Paused; Victory/Defeat son terminales;
        // Menu solo existe si _startWithMenu está activo.
        private enum GameState { Menu, Playing, Paused, Victory, Defeat }
        private GameState _gameState;

        // Posición de spawn del player, capturada en Awake ANTES de InitializeSystems.
        // Fuente de verdad para reconstruir la run: cada StartRun reposiciona al player acá.
        private Vector3 _playerSpawnPosition;

        // --- Eventos de flujo hacia UIManager ---
        public event Action<bool> PauseChanged; // true = pausado, false = reanudado
        public event Action Victory;
        public event Action Defeat;
        public event Action GameStarted;        // se dispara al iniciar una run (Start o Restart)
        public event Action ReturnedToMenu;     // se dispara al volver al Main Menu (Etapa B: UIManager)

        // UIManager consulta esto en su Start (Awake del GM ya corrió) para mostrar
        // el Start Panel sin depender del orden de ejecución de scripts.
        public bool IsStartMenuActive => _gameState == GameState.Menu;

        // --- HUD: eventos push (clases puras, sin MonoBehaviours nuevos) ---
        // UIManager se suscribe a estos eventos. El orquestador los dispara vía delegates.
        public event Action<float, float> HealthChanged;          // (current, max)
        public event Action<string, int, int, bool> WaveChanged;  // (waveName, currentIndex, totalWaves, isFinalWave)
        public event Action<int> EnemiesLeftChanged;              // (enemiesLeft)
        public event Action<string> WeaponChanged;               // (weaponName)
        // Powerup temporal activo (Speed). (active, displayName, icon, remaining, duration)
        public event Action<bool, string, Sprite, float, float> PowerUpChanged;
        // Arma temporal activa (HUD separado del PowerUp). (active, displayName, icon, remaining, duration)
        public event Action<bool, string, Sprite, float, float> TemporaryWeaponChanged;

        // Nombre de arma actual (fuente: WeaponData.DisplayName, fallback PlayerConfig.WeaponName).
        private string _weaponName;

        private void Awake()
        {
            // Spawn como fuente de verdad: capturar ANTES de InitializeSystems, que lo usa
            // para posicionar el PlayerModel. El transform se mueve durante la run, así que
            // se guarda la posición inicial para poder reconstruir la run en el mismo lugar.
            _playerSpawnPosition = _playerTransform.position;

            // El pool y el InputReader viven toda la escena: se crean/registran UNA sola vez.
            // Los sistemas de gameplay se construyen acá por primera vez y luego se
            // recrean por cada run (RebuildRunSystems). InputReader NUNCA se re-registra.
            InitializePools();
            InitializeSystems();
            CreateOrchestrator();
            InitializeInput();
            RegisterGameplaySystems();

            if (_startWithMenu)
            {
                // Arranca pausado en el menú: el loop no simula gameplay hasta
                // StartGameFromUI(). InputReader igual tickea (always-tickable),
                // pero TogglePause se ignora en estado Menu. Los sistemas recién creados
                // quedan idle (no tickean por estar pausados) y son válidos para que
                // InputReader nunca encuentre _playerModel/_playerSystem en null.
                _gameState = GameState.Menu;
                _updateManager.SetPaused(true);
            }
            else
            {
                // Boot directo a gameplay: reusa los sistemas recién creados (sin rebuild).
                // GameStarted/HUD inicial los maneja UIManager.Start (corre después de Awake).
                StartGameplay();
                _gameState = GameState.Playing;
            }
        }

        // InputReader es clase pura y posee InputActions no manejadas: liberarlas acá
        // para evitar leaks (reemplaza al antiguo OnDisable del componente).
        private void OnDestroy()
        {
            _inputReader?.Dispose();
        }

        private void InitializeSystems()
        {
            var playerConfig = new PlayerConfig();

            // Nombre de arma para el HUD: desde WeaponData si está asignado; si no, legacy + warning.
            if (_baseWeapon != null)
            {
                _weaponName = _baseWeapon.DisplayName;
            }
            else
            {
                _weaponName = playerConfig.WeaponName;
                Debug.LogWarning("GameManager: _baseWeapon sin asignar en el Inspector. Disparo y HUD usan valores legacy de PlayerConfig.");
            }

            _playerModel = new PlayerModel(playerConfig.MaxHealth, playerConfig.MoveSpeed);
            // Spawn como fuente de verdad (capturado en Awake): el player siempre arranca ahí,
            // tanto en el boot como al reconstruir la run en cada StartRun.
            _playerModel.Position = _playerSpawnPosition;

            _playerSystem = new PlayerSystem(_playerModel, playerConfig, _baseWeapon);
            _enemySystem = new EnemySystem(() => _playerModel.Position);
            _projectileSystem = new ProjectileSystem();
            _waveSystem = new WaveSystem();
            _roomSystem = new RoomSystem(_gameFlowConfig);
            _combatSystem = new CombatSystem();
            _pickupSystem = new PickupSystem(() => _playerModel.Position);
        }

        private void InitializePools()
        {
            _objectPool = new ObjectPool();
            _objectPool.RegisterPrefab("Enemy", _enemyPrefab);
            _objectPool.RegisterPrefab("Projectile", _projectilePrefab);

            // Pickup: opcional. Si no hay prefab, no se registra la key y el spawn degrada
            // con warning en el orquestador (sin crashear).
            if (_pickupPrefab != null)
                _objectPool.RegisterPrefab("Pickup", _pickupPrefab);
            else
                Debug.LogWarning("GameManager: _pickupPrefab sin asignar. Los drops no se mostrarán (gameplay sigue funcionando).");

            if (_poolConfig == null)
            {
                Debug.LogError("GameManager missing PoolConfig reference. Pools will not be prewarmed (they will still grow on demand).");
                return;
            }

            _objectPool.Prewarm("Enemy", _poolConfig.EnemyPrewarm);
            _objectPool.Prewarm("Projectile", _poolConfig.ProjectilePrewarm);
            if (_pickupPrefab != null)
                _objectPool.Prewarm("Pickup", _poolConfig.PickupPrewarm);
        }

        // Construye el sistema puro que ejecuta la lógica recurrente. Recibe las MISMAS
        // instancias de pool/diccionarios/spawn groups (referencias, no copias) para no
        // duplicar ownership. El HUD y el fin de juego se pasan como delegates para no
        // acoplar el sistema puro al tipo GameManager.
        private void CreateOrchestrator()
        {
            _orchestrator = new GameplayOrchestratorSystem(
                _playerModel,
                _enemySystem,
                _projectileSystem,
                _waveSystem,
                _roomSystem,
                _combatSystem,
                _objectPool,
                _enemyViews,
                _projectileViews,
                _roomSpawnGroups,
                _playerTransform,
                _pickupSystem,
                _pickupViews,
                (multiplier, duration, displayName, icon) => _playerSystem.ApplySpeedBoost(multiplier, duration, displayName, icon),
                (weapon, duration, displayName, icon) => _playerSystem.ApplyTemporaryWeapon(weapon, duration, displayName, icon),
                () => (_playerSystem.HasActiveSpeedBoost,
                       _playerSystem.SpeedBoostName,
                       _playerSystem.SpeedBoostIcon,
                       _playerSystem.SpeedBoostRemaining,
                       _playerSystem.SpeedBoostDuration),
                (active, displayName, icon, remaining, duration) => PowerUpChanged?.Invoke(active, displayName, icon, remaining, duration),
                () => (_playerSystem.HasTemporaryWeapon,
                       _playerSystem.TemporaryWeaponName,
                       _playerSystem.TemporaryWeaponIcon,
                       _playerSystem.TemporaryWeaponRemaining,
                       _playerSystem.TemporaryWeaponDuration),
                (active, displayName, icon, remaining, duration) => NotifyTemporaryWeaponChanged(active, displayName, icon, remaining, duration),
                (current, max) => HealthChanged?.Invoke(current, max),
                (waveName, index, total, isFinal) => WaveChanged?.Invoke(waveName, index, total, isFinal),
                enemiesLeft => EnemiesLeftChanged?.Invoke(enemiesLeft),
                EndGameplay);
        }

        // InputReader vive TODA la escena: se crea y registra UNA sola vez (Awake).
        // Separado del registro de gameplay para no re-registrarlo en cada Restart.
        private void InitializeInput()
        {
            if (_mainCamera == null)
                Debug.LogError("GameManager: _mainCamera sin asignar en el Inspector. El aim no funcionará.");

            _inputReader = new MonoBehaviours.InputReader(this, _mainCamera);
            _inputReader.Initialize();
            // InputReader NO se pausa: debe seguir leyendo Escape para poder despausar.
            _updateManager.Register(_inputReader, pauseWithGameplay: false);
        }

        // Registra los sistemas pausables de gameplay (NO InputReader). Se llama en cada
        // build de run. Orden de tick: simulación primero, orquestador al final, para que
        // lea el estado ya actualizado del frame. PickupSystem antes del orquestador:
        // detecta recogidas en el frame y el orquestador las procesa en el mismo Tick.
        private void RegisterGameplaySystems()
        {
            _updateManager.Register(_playerSystem);
            _updateManager.Register(_enemySystem);
            _updateManager.Register(_projectileSystem);
            _updateManager.Register(_waveSystem);
            _updateManager.Register(_pickupSystem);
            _updateManager.Register(_orchestrator);
        }

        // Desregistra los sistemas pausables de la run anterior antes de recrearlos.
        // Unregister es null-safe (Remove(null) no rompe), así que es seguro en cualquier
        // estado. Evita que queden tickables viejos corriendo en paralelo (doble simulación).
        private void UnregisterGameplaySystems()
        {
            _updateManager.Unregister(_playerSystem);
            _updateManager.Unregister(_enemySystem);
            _updateManager.Unregister(_projectileSystem);
            _updateManager.Unregister(_waveSystem);
            _updateManager.Unregister(_pickupSystem);
            _updateManager.Unregister(_orchestrator);
        }

        // Devuelve al pool todas las views activas de la run y limpia los mappings.
        // GameManager es dueño de los 3 diccionarios (única fuente de views activas), así
        // que despawnea con la key correcta: "Enemy", la PoolKey del proyectil y "Pickup".
        // Idempotente: si los diccionarios están vacíos (primera run) no hace nada.
        private void CleanupRunEntities()
        {
            foreach (var kvp in _enemyViews)
                _objectPool.Despawn("Enemy", kvp.Value);
            _enemyViews.Clear();

            foreach (var kvp in _projectileViews)
                _objectPool.Despawn(kvp.Key.PoolKey, kvp.Value);
            _projectileViews.Clear();

            foreach (var kvp in _pickupViews)
                _objectPool.Despawn("Pickup", kvp.Value);
            _pickupViews.Clear();
        }

        // Reconstruye los sistemas puros de gameplay desde cero para una run nueva.
        // Desregistra los viejos, recrea modelos/sistemas/orquestador y registra los nuevos.
        // El pool y el InputReader NO se tocan (persisten toda la escena).
        private void RebuildRunSystems()
        {
            UnregisterGameplaySystems();
            InitializeSystems();
            CreateOrchestrator();
            RegisterGameplaySystems();
        }

        // Única entrada para "empezar una run" en runtime (Start desde menú y Restart).
        // Limpia la run anterior, reconstruye sistemas, reposiciona al player en el spawn,
        // arranca el flujo y notifica UI/HUD. Sin reload de escena, sin static, sin nullear
        // _playerModel/_playerSystem (InputReader sigue tickeando y necesita ambos válidos).
        private void StartRun()
        {
            CleanupRunEntities();
            RebuildRunSystems();

            _gameplayEnded = false;
            // InitializeSystems ya posicionó el modelo en el spawn; se reafirma el transform
            // para que la vista no quede un frame en la última posición de la run anterior.
            _playerModel.Position = _playerSpawnPosition;
            _playerTransform.position = _playerSpawnPosition;

            StartGameplay();
            _gameState = GameState.Playing;
            _updateManager.SetPaused(false);

            GameStarted?.Invoke();
            BroadcastInitialUiState();
        }

        private void StartGameplay()
        {
            _roomSystem.StartFirstRoom();

            if (!_roomSystem.HasCurrentRoom)
            {
                Debug.LogError("GameManager: GameFlowConfig vacío o sin rooms asignadas. No se iniciará el spawn.");
                return;
            }

            _waveSystem.StartRoom(_roomSystem.CurrentRoom);
        }

        // Llamado por el orquestador al entrar en Defeat/Victory. Pausa el loop central,
        // bloquea el disparo y notifica a la UI el panel correspondiente.
        // El último HUD (vida = 0) ya se emitió antes de esta llamada.
        private void EndGameplay(bool victory)
        {
            _gameplayEnded = true;
            _gameState = victory ? GameState.Victory : GameState.Defeat;
            _updateManager.SetPaused(true);

            if (victory)
                Victory?.Invoke();
            else
                Defeat?.Invoke();
        }

        /// <summary>
        /// Alterna pausa/reanudar. Lo invoca InputReader al presionar Escape.
        /// Solo válido en Playing/Paused: Victory/Defeat son terminales y el menú
        /// de inicio no se puede pausar.
        /// </summary>
        public void TogglePause()
        {
            if (_gameState != GameState.Playing && _gameState != GameState.Paused)
                return;

            bool nextPaused = !_updateManager.IsPaused;
            _updateManager.SetPaused(nextPaused);
            _gameState = nextPaused ? GameState.Paused : GameState.Playing;
            PauseChanged?.Invoke(nextPaused);
        }

        /// <summary>
        /// Reanuda el juego SOLO si está pausado. A diferencia de TogglePause, es
        /// idempotente: si no está en Paused no hace nada. La invoca UIManager desde
        /// el botón Resume del Pause Menu.
        /// </summary>
        public void ResumeGame()
        {
            if (_gameState != GameState.Paused)
                return;

            _updateManager.SetPaused(false);
            _gameState = GameState.Playing;
            PauseChanged?.Invoke(false);
        }

        /// <summary>
        /// Reinicia la run in-place SIN recargar escena y SIN pasar por el Main Menu.
        /// StartRun limpia la run anterior (views al pool, sistemas viejos desregistrados),
        /// reconstruye los sistemas, reposiciona al player en el spawn y arranca jugando.
        /// La invoca UIManager desde el botón Restart (Pause y End).
        /// </summary>
        public void RestartGame()
        {
            StartRun();
        }

        /// <summary>
        /// Placeholder seguro de Options. Todavía no hay menú real de opciones; solo
        /// deja traza para verificar el wiring del botón. La invoca UIManager.
        /// </summary>
        public void OpenOptions()
        {
            Debug.Log("GameManager: OpenOptions() (placeholder, sin menú de opciones aún).");
        }

        /// <summary>
        /// Vuelve al Main Menu in-place SIN recargar escena. Limpia la run actual (devuelve
        /// todas las views al pool), pasa a estado Menu y pausa el loop. NO recrea sistemas:
        /// el próximo StartGameFromUI/StartRun los reconstruye fresh. _playerModel/_playerSystem
        /// quedan vivos (idle) para que InputReader siga tickeando sin null. Dispara
        /// ReturnedToMenu para que UIManager (Etapa B) muestre el menú y oculte HUD/overlays.
        /// La invoca UIManager desde el botón Return (Pause y End).
        /// </summary>
        public void ReturnToMainMenu()
        {
            CleanupRunEntities();
            _gameplayEnded = false;
            _gameState = GameState.Menu;
            _updateManager.SetPaused(true);
            ReturnedToMenu?.Invoke();
        }

        /// <summary>
        /// Inicia una run limpia desde el Main Menu. Solo actúa en estado Menu. Reusa StartRun,
        /// que reconstruye los sistemas: garantiza una partida fresca aunque se venga de
        /// ReturnToMainMenu (sin enemigos/proyectiles/pickups viejos ni doble spawn).
        /// La invoca UIManager desde el botón Start Game.
        /// </summary>
        public void StartGameFromUI()
        {
            if (_gameState != GameState.Menu)
                return;

            StartRun();
        }

        /// <summary>
        /// Empuja el estado inicial completo del HUD una sola vez.
        /// La llama UIManager en su Start(), después de suscribirse a los eventos,
        /// para no depender del orden de ejecución de scripts.
        /// </summary>
        public void BroadcastInitialUiState()
        {
            HealthChanged?.Invoke(_playerModel.Health, _playerModel.MaxHealth);
            WaveChanged?.Invoke(
                _waveSystem.CurrentWaveName,
                _waveSystem.CurrentWaveIndex,
                _waveSystem.TotalWaves,
                _waveSystem.IsFinalWave);
            EnemiesLeftChanged?.Invoke(_waveSystem.PendingToSpawnCount + _enemySystem.AliveCount);
            WeaponChanged?.Invoke(_weaponName);
            // Powerup arranca inactivo: la UI del powerup queda oculta hasta recoger un Speed.
            PowerUpChanged?.Invoke(false, null, null, 0f, 0f);
            // Arma temporal arranca inactiva: el Weapon HUD queda oculto hasta recoger un arma.
            TemporaryWeaponChanged?.Invoke(false, null, null, 0f, 0f);

            // Si UIManager.Start corrió antes que el primer Tick, el orquestador re-emitirá
            // con datos ya correctos en su primer RefreshHud.
            _orchestrator.ResetHudCaches();
        }

        // Punto de notificación controlado del estado del arma temporal hacia la UI.
        // Lo invoca el orquestador desde su Tick (no es Update). GameManager sigue siendo
        // dueño del evento; el orquestador no lo dispara directo.
        public void NotifyTemporaryWeaponChanged(bool active, string displayName, Sprite icon, float remaining, float duration)
        {
            TemporaryWeaponChanged?.Invoke(active, displayName, icon, remaining, duration);
        }

        // --- Callbacks no recurrentes invocados por InputReader ---

        public void FireProjectile()
        {
            // Bloqueo tras fin de juego o durante pausa/menú: no se crea ningún proyectil
            // aunque InputReader siga enviando el click (sigue tickeando siempre).
            if (_gameplayEnded || _updateManager.IsPaused)
                return;

            if (!_playerSystem.CanFire())
                return;

            _playerSystem.Fire();
            var projectile = _playerSystem.CreateProjectile(_projectileSystem.Projectiles.Count);
            _projectileSystem.AddProjectile(projectile);

            // Pool key del proyectil: única fuente de verdad = projectile.PoolKey
            // (la decide PlayerSystem). Spawn y Despawn SIEMPRE usan esta misma key:
            // nunca se spawnea con una key distinta a la guardada en el modelo.
            // Registro lazy: si la key es específica y aún no está en el pool, registrar
            // el prefab del arma. Si el prefab falta en ese punto (no debería ocurrir,
            // PlayerSystem solo deriva key especial si hay prefab), se aborta este disparo
            // sin spawnear con otra key.
            if (projectile.PoolKey != "Projectile" && !_objectPool.HasPrefab(projectile.PoolKey))
            {
                var weaponPrefab = _playerSystem.ActiveProjectilePrefab;
                if (weaponPrefab == null)
                {
                    Debug.LogWarning($"GameManager: falta ProjectilePrefab para la pool key '{projectile.PoolKey}'. No se spawnea este proyectil.");
                    _projectileSystem.RemoveProjectile(projectile);
                    return;
                }

                _objectPool.RegisterPrefab(projectile.PoolKey, weaponPrefab);
            }

            var view = _objectPool.Spawn(projectile.PoolKey, projectile.Position);
            if (view != null)
            {
                view.SetRotation(Quaternion.LookRotation(_playerSystem.GetFireDirection()));
                _projectileViews[projectile] = view;
            }
            else
            {
                // Spawn falló: no dejar el modelo sin view (evita un proyectil fantasma
                // que el orquestador no podría sincronizar ni despawnear visualmente).
                _projectileSystem.RemoveProjectile(projectile);
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
