using System;
using System.Collections;
using System.Collections.Generic;
using OptimizationGame.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OptimizationGame.MonoBehaviours
{
    /// <summary>
    /// Capa de presentación del HUD (Room 1). NO contiene lógica de gameplay.
    /// Modelo push: se suscribe a eventos de GameManager y solo actualiza un texto/barra
    /// cuando el dato cambia. No tiene Update: cero reasignaciones de texto por frame,
    /// para evitar Canvas Rebuilds innecesarios.
    /// HUD separado por frecuencia de cambio: un Canvas por dato dinámico.
    ///
    /// Lectura estricta de la consigna (máx. 10 campos serializados/públicos): solo se
    /// serializan 3 raíces (_gameManager, _hudRoot, _overlayRoot). El resto de referencias
    /// de UI se resuelven UNA sola vez en Start() mediante búsqueda LOCAL/scoped dentro de
    /// esos roots (Transform.Find + GetComponent). No se usa GameObject.Find/FindObjectOfType.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private Transform _hudRoot;
        [SerializeField] private Transform _overlayRoot;

        // --- Referencias runtime (NO serializadas): resueltas en ResolveReferences() ---

        // Canvas dinámicos del HUD.
        private Canvas _healthCanvas;
        private Canvas _waveCanvas;
        private Canvas _enemiesLeftCanvas;
        private Canvas _weaponCanvas;

        // Textos del HUD / overlay.
        private TMP_Text _healthValueText;
        private TMP_Text _waveText;
        private TMP_Text _enemiesLeftText;
        private TMP_Text _weaponText;
        private TMP_Text _endTitleText;

        // Imágenes con fillAmount.
        private Image _healthBar;
        private Image _powerUpIconFill;
        private Image _weaponPickupIconFill;

        // Paneles de flujo y HUDs opcionales (CanvasGroup).
        private CanvasGroup _startPanel;
        private CanvasGroup _pausePanel;
        private CanvasGroup _endPanel;
        private CanvasGroup _powerUpCanvasGroup;
        private CanvasGroup _weaponPickupCanvasGroup;

        // Botones (delegan flujo al GameManager; no deciden estado acá).
        private Button _pauseIconButton;
        private Button _resumeButton;
        private Button _pauseRestartButton;
        private Button _pauseOptionsButton;
        private Button _pauseReturnToMainMenuButton;
        private Button _endRestartButton;
        private Button _endReturnToMainMenuButton;
        private Button _startGameButton;
        private Button _startOptionsButton;
        private Button _quitButton;

        // Cache de los últimos strings/valores aplicados para no reasignar si no cambió.
        private float _lastHealthTargetFill = float.NaN;
        private string _lastHealthValueString;
        private string _lastWaveString;
        private string _lastEnemiesLeftString;
        private string _lastWeaponString;

        private bool _subscribed;

        // Animación progresiva de la barra de vida (no depende de timeScale).
        private const float HealthBarAnimationDuration = 0.3f;
        private Coroutine _healthBarRoutine;

        // Acumulador de rutas no resueltas: se loguea una sola advertencia al final.
        private readonly List<string> _missingRefs = new List<string>();

        private void Start()
        {
            if (_gameManager == null)
            {
                Debug.LogError("UIManager: falta referencia a GameManager. El HUD no se actualizará.");
                return;
            }

            ResolveReferences();

            Subscribe();
            WireButtons();

            // Estado inicial de los paneles de flujo: todos ocultos salvo el Start Menu
            // si el GameManager arrancó en ese estado.
            SetPanel(_pausePanel, false);
            SetPanel(_endPanel, false);
            SetPanel(_startPanel, _gameManager.IsStartMenuActive);

            // HUD oculto mientras se está en el Main Menu; visible si se arranca jugando.
            // Al tocar Start, OnGameStarted lo vuelve a mostrar.
            SetHudVisible(!_gameManager.IsStartMenuActive);

            // Empujar estado inicial después de suscribir, sin depender del orden de Start.
            _gameManager.BroadcastInitialUiState();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnwireButtons();
        }

        // --- Resolución de referencias (búsqueda LOCAL, una sola vez en Start) ---

        /// <summary>
        /// Resuelve todas las referencias de UI dentro de _hudRoot y _overlayRoot mediante
        /// Transform.Find (scoped, NO global). Los duplicados de nombre (RestartButton,
        /// ReturnToMenuButton) se desambiguan por su parent (PauseMenuParent vs EndParent).
        /// Fail-safe: lo que no se encuentra queda null y se acumula en _missingRefs.
        /// </summary>
        private void ResolveReferences()
        {
            if (_overlayRoot == null)
                _missingRefs.Add("(campo) _overlayRoot");
            if (_hudRoot == null)
                _missingRefs.Add("(campo) _hudRoot");

            // --- Overlay: paneles de flujo (start / pause / end) ---
            _startPanel = FindComponent<CanvasGroup>(_overlayRoot, "StartMenuParent");
            _startGameButton = FindComponent<Button>(_overlayRoot, "StartMenuParent/StartGameButton");
            _startOptionsButton = FindComponent<Button>(_overlayRoot, "StartMenuParent/OptionsButton");
            _quitButton = FindComponent<Button>(_overlayRoot, "StartMenuParent/QuitButton");

            _pausePanel = FindComponent<CanvasGroup>(_overlayRoot, "PauseMenuParent");
            _resumeButton = FindComponent<Button>(_overlayRoot, "PauseMenuParent/ResumeButton");
            _pauseRestartButton = FindComponent<Button>(_overlayRoot, "PauseMenuParent/RestartButton");
            _pauseOptionsButton = FindComponent<Button>(_overlayRoot, "PauseMenuParent/OptionsButton");
            _pauseReturnToMainMenuButton = FindComponent<Button>(_overlayRoot, "PauseMenuParent/ReturnToMenuButton");

            _endPanel = FindComponent<CanvasGroup>(_overlayRoot, "EndParent");
            _endTitleText = FindComponent<TMP_Text>(_overlayRoot, "EndParent/EndTitleText");
            _endRestartButton = FindComponent<Button>(_overlayRoot, "EndParent/RestartButton");
            _endReturnToMainMenuButton = FindComponent<Button>(_overlayRoot, "EndParent/ReturnToMenuButton");

            // --- HUD: botón de pausa (typo real en la jerarquía: "PuseIconButton") ---
            _pauseIconButton = FindComponent<Button>(_hudRoot, "HUDStaticCanvas/PuseIconButton");

            // --- HUD: canvas dinámicos + sus textos/barras ---
            _healthCanvas = FindComponent<Canvas>(_hudRoot, "HealthCanvas");
            _healthBar = FindComponent<Image>(_hudRoot, "HealthCanvas/healthFrame/HealthBarFill");
            _healthValueText = FindComponent<TMP_Text>(_hudRoot, "HealthCanvas/HealthText");

            _waveCanvas = FindComponent<Canvas>(_hudRoot, "WaveCanvas");
            _waveText = FindComponent<TMP_Text>(_hudRoot, "WaveCanvas/WaveText");

            _enemiesLeftCanvas = FindComponent<Canvas>(_hudRoot, "EnemiesLeftCanvas");
            _enemiesLeftText = FindComponent<TMP_Text>(_hudRoot, "EnemiesLeftCanvas/EnemiesLeftText");

            _weaponCanvas = FindComponent<Canvas>(_hudRoot, "WeaponCanvas");
            _weaponText = FindComponent<TMP_Text>(_hudRoot, "WeaponCanvas/WeaponText");

            // --- HUD: powerup temporal + arma temporal ---
            _powerUpCanvasGroup = FindComponent<CanvasGroup>(_hudRoot, "PowerUpPanel");
            _powerUpIconFill = FindComponent<Image>(_hudRoot, "PowerUpPanel/PowerUpIcon");

            _weaponPickupCanvasGroup = FindComponent<CanvasGroup>(_hudRoot, "WeaponPickUp");
            _weaponPickupIconFill = FindComponent<Image>(_hudRoot, "WeaponPickUp/WeaponPickUpIcon");

            if (_missingRefs.Count > 0)
            {
                Debug.LogWarning(
                    "UIManager: referencias de UI no resueltas (verificar nombres/jerarquía en el prefab UI): "
                    + string.Join(", ", _missingRefs));
            }
        }

        /// <summary>
        /// Busca un hijo por path relativo a root (búsqueda LOCAL, no global).
        /// Devuelve null y acumula la ruta faltante si no existe. No tira NullReference.
        /// </summary>
        private Transform FindChild(Transform root, string path)
        {
            if (root == null)
                return null;

            Transform child = root.Find(path);
            if (child == null)
                _missingRefs.Add($"{root.name}/{path}");

            return child;
        }

        /// <summary>
        /// Igual que FindChild pero devuelve el componente T del hijo. Acumula la ruta
        /// faltante si no existe el hijo o el componente. No tira NullReference.
        /// </summary>
        private T FindComponent<T>(Transform root, string path) where T : Component
        {
            Transform child = FindChild(root, path);
            if (child == null)
                return null;

            T component = child.GetComponent<T>();
            if (component == null)
                _missingRefs.Add($"{root.name}/{path} : falta {typeof(T).Name}");

            return component;
        }

        // --- Wiring de botones del Pause Menu (sin Update, sin lógica de gameplay) ---

        private void WireButtons()
        {
            if (_pauseIconButton != null)
                _pauseIconButton.onClick.AddListener(OnPauseClicked);
            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(OnResumeClicked);
            if (_pauseRestartButton != null)
                _pauseRestartButton.onClick.AddListener(OnRestartClicked);
            if (_pauseOptionsButton != null)
                _pauseOptionsButton.onClick.AddListener(OnOptionsClicked);
            if (_pauseReturnToMainMenuButton != null)
                _pauseReturnToMainMenuButton.onClick.AddListener(OnReturnToMainMenuClicked);

            // Botones del EndParent: reutilizan los mismos callbacks de flujo del GameManager.
            if (_endRestartButton != null)
                _endRestartButton.onClick.AddListener(OnRestartClicked);
            if (_endReturnToMainMenuButton != null)
                _endReturnToMainMenuButton.onClick.AddListener(OnReturnToMainMenuClicked);

            // Botones del Start/Main Menu: wiring por código (no OnClick de Inspector).
            if (_startGameButton != null)
                _startGameButton.onClick.AddListener(OnStartGameClicked);
            if (_startOptionsButton != null)
                _startOptionsButton.onClick.AddListener(OnOptionsClicked);
            if (_quitButton != null)
                _quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void UnwireButtons()
        {
            if (_pauseIconButton != null)
                _pauseIconButton.onClick.RemoveListener(OnPauseClicked);
            if (_resumeButton != null)
                _resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (_pauseRestartButton != null)
                _pauseRestartButton.onClick.RemoveListener(OnRestartClicked);
            if (_pauseOptionsButton != null)
                _pauseOptionsButton.onClick.RemoveListener(OnOptionsClicked);
            if (_pauseReturnToMainMenuButton != null)
                _pauseReturnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);

            if (_endRestartButton != null)
                _endRestartButton.onClick.RemoveListener(OnRestartClicked);
            if (_endReturnToMainMenuButton != null)
                _endReturnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);

            if (_startGameButton != null)
                _startGameButton.onClick.RemoveListener(OnStartGameClicked);
            if (_startOptionsButton != null)
                _startOptionsButton.onClick.RemoveListener(OnOptionsClicked);
            if (_quitButton != null)
                _quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        // Cada callback solo delega al GameManager: UIManager no decide estado de juego.
        private void OnPauseClicked() => _gameManager.TogglePause();
        private void OnResumeClicked() => _gameManager.ResumeGame();
        private void OnRestartClicked() => _gameManager.RestartGame();
        private void OnOptionsClicked() => _gameManager.OpenOptions();
        private void OnReturnToMainMenuClicked() => _gameManager.ReturnToMainMenu();
        private void OnStartGameClicked() => _gameManager.StartGameFromUI();

        // Cierra la build; en Editor sale de Play Mode. El bloque UnityEditor solo compila
        // en el Editor (guardado por #if), así que no afecta la build final.
        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Subscribe()
        {
            if (_subscribed || _gameManager == null)
                return;

            _gameManager.HealthChanged += UpdateHealth;
            _gameManager.WaveChanged += UpdateWave;
            _gameManager.EnemiesLeftChanged += UpdateEnemiesLeft;
            _gameManager.WeaponChanged += UpdateWeapon;
            _gameManager.PowerUpChanged += UpdatePowerUp;
            _gameManager.TemporaryWeaponChanged += UpdateTemporaryWeapon;
            _gameManager.PauseChanged += OnPauseChanged;
            _gameManager.Victory += OnVictory;
            _gameManager.Defeat += OnDefeat;
            _gameManager.GameStarted += OnGameStarted;
            _gameManager.ReturnedToMenu += OnReturnedToMenu;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _gameManager == null)
                return;

            _gameManager.HealthChanged -= UpdateHealth;
            _gameManager.WaveChanged -= UpdateWave;
            _gameManager.EnemiesLeftChanged -= UpdateEnemiesLeft;
            _gameManager.WeaponChanged -= UpdateWeapon;
            _gameManager.PowerUpChanged -= UpdatePowerUp;
            _gameManager.TemporaryWeaponChanged -= UpdateTemporaryWeapon;
            _gameManager.PauseChanged -= OnPauseChanged;
            _gameManager.Victory -= OnVictory;
            _gameManager.Defeat -= OnDefeat;
            _gameManager.GameStarted -= OnGameStarted;
            _gameManager.ReturnedToMenu -= OnReturnedToMenu;
            _subscribed = false;
        }

        // --- Paneles de flujo (push por evento del GameManager) ---

        private void OnPauseChanged(bool paused)
        {
            // Al pausar, el EndPanel nunca debe quedar visible: pausePanel y endPanel
            // cuelgan del mismo OverlayCanvas y no deben solaparse. Lo ocultamos explícitamente
            // (simetría con ShowEndPanel, que oculta pausePanel al mostrar el End).
            if (paused)
                SetPanel(_endPanel, false);

            SetPanel(_pausePanel, paused);
        }

        private void OnVictory()
        {
            ShowEndPanel("VICTORY");
        }

        private void OnDefeat()
        {
            ShowEndPanel("DEFEAT");
        }

        // EndPanel reutilizable: Victory y Defeat usan el mismo panel cambiando solo el título.
        // Tapa la pausa si estaba visible.
        private void ShowEndPanel(string title)
        {
            SetPanel(_pausePanel, false);
            if (_endTitleText != null)
                _endTitleText.text = title;
            SetPanel(_endPanel, true);
        }

        // Una run empezó: desde el Main Menu (Start) o por Restart in-place desde Pause/End.
        // Por eso cierra TODOS los overlays (start/pause/end) y muestra el HUD, en vez de
        // ocultar solo el startPanel.
        private void OnGameStarted()
        {
            SetPanel(_startPanel, false);
            SetPanel(_pausePanel, false);
            SetPanel(_endPanel, false);
            SetHudVisible(true);
        }

        // Vuelta al Main Menu in-place (sin reload): mostrar el menú, cerrar pause/end y
        // ocultar el HUD. Empareja con OnGameStarted (estado opuesto).
        private void OnReturnedToMenu()
        {
            SetPanel(_startPanel, true);
            SetPanel(_pausePanel, false);
            SetPanel(_endPanel, false);
            SetHudVisible(false);
        }

        /// <summary>Muestra/oculta un panel vía CanvasGroup sin togglear GameObjects.</summary>
        private void SetPanel(CanvasGroup panel, bool visible)
        {
            if (panel == null)
                return;

            panel.alpha = visible ? 1f : 0f;
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
        }

        /// <summary>Muestra/oculta los Canvas dinámicos del HUD.</summary>
        public void SetHudVisible(bool visible)
        {
            SetCanvasActive(_healthCanvas, visible);
            SetCanvasActive(_waveCanvas, visible);
            SetCanvasActive(_enemiesLeftCanvas, visible);
            SetCanvasActive(_weaponCanvas, visible);

            // El botón de pausa vive dentro del HUDStaticCanvas: forma parte del HUD jugable.
            if (_pauseIconButton != null)
                _pauseIconButton.gameObject.SetActive(visible);
        }

        private static void SetCanvasActive(Canvas canvas, bool visible)
        {
            if (canvas != null && canvas.gameObject.activeSelf != visible)
                canvas.gameObject.SetActive(visible);
        }

        public void UpdateHealth(float current, float max)
        {
            // Texto numérico de vida (opcional). Actualiza instantáneo, independiente
            // de la animación de la barra. Solo reasigna si el string cambió.
            if (_healthValueText != null)
            {
                string s = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
                if (s != _lastHealthValueString)
                {
                    _lastHealthValueString = s;
                    _healthValueText.text = s;
                }
            }

            // La barra de vida (opcional) sigue siendo la representación principal.
            if (_healthBar == null)
                return;

            float targetFill = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (targetFill == _lastHealthTargetFill)
                return;

            _lastHealthTargetFill = targetFill;
            AnimateHealthBarTo(targetFill);
        }

        private void AnimateHealthBarTo(float targetFill)
        {
            // Si la barra estaba animando, cortar y reanudar desde el fill visible actual.
            if (_healthBarRoutine != null)
                StopCoroutine(_healthBarRoutine);

            // Si el objeto está inactivo no se pueden lanzar coroutines: setear directo.
            if (!isActiveAndEnabled)
            {
                _healthBar.fillAmount = targetFill;
                _healthBarRoutine = null;
                return;
            }

            _healthBarRoutine = StartCoroutine(AnimateHealthBarRoutine(targetFill));
        }

        private IEnumerator AnimateHealthBarRoutine(float targetFill)
        {
            float startFill = _healthBar.fillAmount;
            float elapsed = 0f;

            while (elapsed < HealthBarAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime; // independiente del timeScale (pausa futura)
                float t = Mathf.Clamp01(elapsed / HealthBarAnimationDuration);
                _healthBar.fillAmount = Mathf.Lerp(startFill, targetFill, t);
                yield return null;
            }

            _healthBar.fillAmount = targetFill;
            _healthBarRoutine = null;
        }

        public void UpdateWave(string waveName, int currentWaveIndex, int totalWaves, bool isFinalWave)
        {
            if (_waveText == null)
                return;

            // Solo el nombre real de la wave; se ignoran índice, total y flag final.
            string s = string.IsNullOrWhiteSpace(waveName) ? "Wave" : waveName;

            if (s != _lastWaveString)
            {
                _lastWaveString = s;
                _waveText.text = s;
            }
        }

        public void UpdateEnemiesLeft(int enemiesLeft)
        {
            if (_enemiesLeftText == null)
                return;

            int safeCount = Mathf.Max(0, enemiesLeft);
            string s = $"Enemies: {safeCount}";
            if (s != _lastEnemiesLeftString)
            {
                _lastEnemiesLeftString = s;
                _enemiesLeftText.text = s;
            }
        }

        /// <summary>
        /// Actualiza la UI del powerup temporal activo (Speed). Push por evento, sin Update.
        /// Si active es false, oculta el CanvasGroup. Si es true, lo muestra, setea el ícono
        /// y ajusta fillAmount = remaining/duration (clamp 0..1) para el vaciado vertical.
        /// </summary>
        public void UpdatePowerUp(bool active, string displayName, Sprite icon, float remaining, float duration)
        {
            if (_powerUpCanvasGroup != null)
            {
                _powerUpCanvasGroup.alpha = active ? 1f : 0f;
                _powerUpCanvasGroup.interactable = active;
                _powerUpCanvasGroup.blocksRaycasts = active;
            }

            if (!active || _powerUpIconFill == null)
                return;

            if (icon != null)
                _powerUpIconFill.sprite = icon;

            float fill = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
            _powerUpIconFill.fillAmount = fill;
        }

        /// <summary>
        /// Actualiza la UI del arma temporal activa. HUD separado del powerup de Speed.
        /// Push por evento, sin Update. Si active es false, oculta el CanvasGroup y vacía el fill.
        /// Si es true, lo muestra, setea el ícono (si hay) y ajusta fillAmount = remaining/duration.
        /// </summary>
        public void UpdateTemporaryWeapon(bool active, string displayName, Sprite icon, float remaining, float duration)
        {
            if (_weaponPickupCanvasGroup != null)
            {
                _weaponPickupCanvasGroup.alpha = active ? 1f : 0f;
                _weaponPickupCanvasGroup.interactable = false;
                _weaponPickupCanvasGroup.blocksRaycasts = false;
            }

            if (_weaponPickupIconFill == null)
                return;

            if (!active)
            {
                // Limpiar el sprite al ocultar evita que reaparezca un icono stale la próxima vez.
                _weaponPickupIconFill.sprite = null;
                _weaponPickupIconFill.fillAmount = 0f;
                return;
            }

            // Asignar siempre (incluido null): si el arma no tiene icono, limpia el anterior
            // en vez de dejar visible el de un arma previa.
            _weaponPickupIconFill.sprite = icon;

            float fill = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
            _weaponPickupIconFill.fillAmount = fill;
        }

        public void UpdateWeapon(string weaponName)
        {
            if (_weaponText == null)
                return;

            string s = $"Weapon: {weaponName}";
            if (s != _lastWeaponString)
            {
                _lastWeaponString = s;
                _weaponText.text = s;
            }
        }
    }
}
