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
        // Prefabs pooled agrupados en un solo campo serializado para no inflar el conteo
        // de campos expuestos de GameManager (límite <=10). Un único [SerializeField]
        // reemplaza a los prefabs sueltos y deja lugar para ImpactVfxPrefab sin sumar campos.
        [SerializeField] private PrefabReferences _prefabs = new();
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

        // Roots visuales de gameplay (player + escenario). Se apagan en Main Menu para
        // no renderizar ni cargar GPU que no hace falta hasta tocar Play. Un solo campo
        // serializado (contenedor) para respetar el límite <=10 campos de GameManager.
        [SerializeField] private GameplaySceneReferences _sceneRefs = new();

        private PlayerSystem _playerSystem;
        private EnemySystem _enemySystem;
        private ProjectileSystem _projectileSystem;
        private WaveSystem _waveSystem;
        private RoomSystem _roomSystem;
        private CombatSystem _combatSystem;
        private PickupSystem _pickupSystem;
        private VfxSystem _vfxSystem;
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
                // En Menu el player y el escenario no hacen falta: se apagan hasta Start.
                SetGameplaySceneActive(false);
            }
            else
            {
                // Boot directo a gameplay: reusa los sistemas recién creados (sin rebuild).
                // GameStarted/HUD inicial los maneja UIManager.Start (corre después de Awake).
                // Se asegura que los roots estén activos aunque en escena quedaran apagados.
                SetGameplaySceneActive(true);
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
            // VfxSystem usa el ObjectPool ya creado en InitializePools (corre antes en Awake).
            // Se recrea por run junto a los demás sistemas; ReturnAllActive lo limpia antes.
            _vfxSystem = new VfxSystem(_objectPool);
        }

        private void InitializePools()
        {
            _objectPool = new ObjectPool();
            _objectPool.RegisterPrefab("Enemy", _prefabs.EnemyPrefab);
            _objectPool.RegisterPrefab("Projectile", _prefabs.ProjectilePrefab);

            // Pickup: opcional. Si no hay prefab, no se registra la key y el spawn degrada
            // con warning en el orquestador (sin crashear).
            if (_prefabs.PickupPrefab != null)
                _objectPool.RegisterPrefab("Pickup", _prefabs.PickupPrefab);
            else
                Debug.LogWarning("GameManager: PickupPrefab sin asignar. Los drops no se mostrarán (gameplay sigue funcionando).");

            // ImpactVFX: opcional. Si no hay prefab, no se registra la key y SpawnImpact
            // degrada (view null) sin crashear. Warning único en init, nunca por frame.
            if (_prefabs.ImpactVfxPrefab != null)
                _objectPool.RegisterPrefab("ImpactVFX", _prefabs.ImpactVfxPrefab);
            else
                Debug.LogWarning("GameManager: ImpactVfxPrefab sin asignar. Los VFX de impacto no se mostrarán (gameplay sigue funcionando).");

            // ImpactVFX por arma (Bloque 1): registra el VFX propio del arma base con su key
            // derivada ("ImpactVFX_<WeaponId>"), sin sacar el fallback global "ImpactVFX".
            // Las armas temporales de pickup se registran lazy en FireProjectile (no hay una
            // lista de todas las WeaponData accesible sin escanear el árbol de drops).
            RegisterWeaponImpactVfx(_baseWeapon);

            if (_poolConfig == null)
            {
                Debug.LogError("GameManager missing PoolConfig reference. Pools will not be prewarmed (they will still grow on demand).");
                return;
            }

            _objectPool.Prewarm("Enemy", _poolConfig.EnemyPrewarm);
            _objectPool.Prewarm("Projectile", _poolConfig.ProjectilePrewarm);
            if (_prefabs.PickupPrefab != null)
                _objectPool.Prewarm("Pickup", _poolConfig.PickupPrewarm);
            if (_prefabs.ImpactVfxPrefab != null)
                _objectPool.Prewarm("ImpactVFX", _poolConfig.VfxPrewarm);
        }

        // Registra el ImpactVFX propio de un arma (si define prefab + WeaponId) con la MISMA
        // key que deriva PlayerSystem.ResolveImpactVfxKey ("ImpactVFX_<WeaponId>"). Idempotente
        // (RegisterPrefab no pisa) y null-safe. Prewarma con VfxPrewarm solo si es un registro
        // nuevo y hay PoolConfig, para no re-crecer una pool ya prewarmeada. No toca el fallback
        // global "ImpactVFX". Reutilizable para futuras armas (temporales) sin acoplar de más.
        private void RegisterWeaponImpactVfx(WeaponData weapon)
        {
            if (weapon == null || weapon.ImpactVfxPrefab == null || string.IsNullOrEmpty(weapon.WeaponId))
                return;

            string key = "ImpactVFX_" + weapon.WeaponId;
            bool alreadyRegistered = _objectPool.HasPrefab(key);
            _objectPool.RegisterPrefab(key, weapon.ImpactVfxPrefab);
            if (!alreadyRegistered && _poolConfig != null)
                _objectPool.Prewarm(key, _poolConfig.VfxPrewarm);
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
                EndGameplay,
                (pos, key) => _vfxSystem?.SpawnImpact(pos, key));
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
            _updateManager.Register(_vfxSystem);
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
            _updateManager.Unregister(_vfxSystem);
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

            // VFX activos al pool: corre con la instancia ACTUAL de _vfxSystem, antes de que
            // RebuildRunSystems la reemplace. Cubre Restart y ReturnToMainMenu (ambos pasan
            // por acá). Evita VFX activos acumulados y views perdidas del pool entre runs.
            _vfxSystem?.ReturnAllActive();
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
            // Reactivar player + escenario ANTES de resetear posición/spawn y arrancar.
            // Idempotente: en Restart ya están activos, así que esto no los apaga.
            SetGameplaySceneActive(true);

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
        /// Placeholder seguro de Options. Todavía no hay menú real de opciones.
        /// La invoca UIManager desde el botón Options.
        /// </summary>
        public void OpenOptions()
        {
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
            // Vuelta al Menu: player y escenario se apagan de nuevo (no hacen falta hasta Start).
            SetGameplaySceneActive(false);
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

            // ImpactVFX por arma: registro lazy simétrico al del proyectil. Cubre armas
            // temporales de pickup (el arma base ya se registró/prewarmeó en InitializePools).
            // Si la key es específica y aún falta, se registra el prefab del arma activa.
            // Es cosmético: si el prefab faltara, NO se aborta el disparo (a diferencia del
            // proyectil); el impacto simplemente no muestra VFX (view null, sin crash).
            if (projectile.ImpactVfxPoolKey != "ImpactVFX" && !_objectPool.HasPrefab(projectile.ImpactVfxPoolKey))
            {
                var impactPrefab = _playerSystem.ActiveImpactVfxPrefab;
                if (impactPrefab != null)
                    _objectPool.RegisterPrefab(projectile.ImpactVfxPoolKey, impactPrefab);
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

        // Agrupa los prefabs pooled en un solo campo serializado. Cuenta como UN campo
        // expuesto de GameManager (respeta el límite <=10) pero expone N referencias en el
        // Inspector.
        [Serializable]
        private class PrefabReferences
        {
            public GameObject EnemyPrefab;
            public GameObject ProjectilePrefab;
            // Opcional: si queda sin asignar, los drops no se muestran (gameplay sigue).
            public GameObject PickupPrefab;
            // Opcional: si queda sin asignar, los VFX de impacto no se muestran (gameplay sigue).
            public GameObject ImpactVfxPrefab;
        }

        // Roots de escena que se apagan/encienden según Menu vs Playing. Un solo campo
        // serializado en GameManager (respeta <=10) que expone 2 referencias en el Inspector.
        [Serializable]
        private class GameplaySceneReferences
        {
            public GameObject PlayerRoot;
            public GameObject RoomRoot;
        }

        // Enciende/apaga los roots visuales de gameplay. Null-safe: si un root no está
        // asignado, degrada en silencio (sin crash, sin log por frame, sin bloquear Start).
        private void SetGameplaySceneActive(bool active)
        {
            if (_sceneRefs.PlayerRoot != null)
                _sceneRefs.PlayerRoot.SetActive(active);
            if (_sceneRefs.RoomRoot != null)
                _sceneRefs.RoomRoot.SetActive(active);
        }
    }
}
