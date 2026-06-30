using System;
using System.Collections;
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
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Serializable]
        private class HudCanvasReferences
        {
            public Canvas HealthCanvas;
            public Canvas WaveCanvas;
            public Canvas EnemiesLeftCanvas;
            public Canvas WeaponCanvas;
        }

        [Serializable]
        private class HudTextReferences
        {
            public Image HealthBar;
            public TMP_Text HealthValueText; // opcional: número de vida sobre la barra
            public TMP_Text WaveText;
            public TMP_Text EnemiesLeftText;
            public TMP_Text WeaponText;
        }

        // Referencias del Pause Menu agrupadas para mantener el Inspector ordenado.
        // No es un MonoBehaviour: es una clase serializable interna sin lógica.
        // El panel se sigue mostrando/ocultando por el evento PauseChanged del GameManager;
        // los botones solo delegan acciones de flujo al GameManager (sin decidir estado acá).
        [Serializable]
        private class PauseMenuReferences
        {
            public Button PauseIconButton;       // ícono in-game para abrir la pausa
            public Button ResumeButton;
            public Button RestartButton;
            public Button OptionsButton;
            public Button ReturnToMainMenuButton;
        }

        // Botones del EndParent (Victory/Defeat reutilizan el mismo panel).
        // Clase serializable interna sin lógica: solo agrupa referencias para el Inspector.
        // Los botones delegan flujo al GameManager, igual que el Pause Menu.
        [Serializable]
        private class EndMenuReferences
        {
            public Button RestartButton;
            public Button ReturnToMainMenuButton;
        }

        [SerializeField] private GameManager _gameManager;
        [SerializeField] private PauseMenuReferences _pauseMenu = new PauseMenuReferences();
        [SerializeField] private EndMenuReferences _endMenu = new EndMenuReferences();
        [SerializeField] private HudCanvasReferences _canvases = new HudCanvasReferences();
        [SerializeField] private HudTextReferences _texts = new HudTextReferences();

        // UI del powerup temporal activo (Speed). Opcionales: si quedan sin asignar, no crashea.
        // La Image debe configurarse en Unity como Filled / Vertical / Origin Top para vaciarse hacia abajo.
        [SerializeField] private CanvasGroup powerUpCanvasGroup;
        [SerializeField] private Image powerUpIconFill;

        // UI del arma temporal activa. HUD SEPARADO del powerup (Speed): no se reutiliza.
        // Opcionales: si quedan sin asignar, no crashea. La Image debe configurarse en Unity
        // como Filled / Vertical / Origin Top para vaciarse hacia abajo según Duration.
        [SerializeField] private CanvasGroup weaponPowerUpCanvasGroup;
        [SerializeField] private Image weaponPowerUpIconFill;

        // Paneles de flujo (pausa/victoria/derrota/inicio). Opcionales: si quedan sin
        // asignar, SetPanel no hace nada (no crashea). Mostrar/ocultar vía CanvasGroup
        // para no togglear GameObjects ni reconstruir jerarquías.
        [SerializeField] private CanvasGroup startPanel;
        [SerializeField] private CanvasGroup pausePanel;
        // EndPanel único reutilizado para Victory y Defeat: solo cambia endTitleText.
        [SerializeField] private CanvasGroup endPanel;
        [SerializeField] private TMP_Text endTitleText;

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

        private void Start()
        {
            if (_gameManager == null)
            {
                Debug.LogError("UIManager: falta referencia a GameManager. El HUD no se actualizará.");
                return;
            }

            Subscribe();
            WireButtons();

            // Estado inicial de los paneles de flujo: todos ocultos salvo el Start Menu
            // si el GameManager arrancó en ese estado.
            SetPanel(pausePanel, false);
            SetPanel(endPanel, false);
            SetPanel(startPanel, _gameManager.IsStartMenuActive);

            // Empujar estado inicial después de suscribir, sin depender del orden de Start.
            _gameManager.BroadcastInitialUiState();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnwireButtons();
        }

        // --- Wiring de botones del Pause Menu (sin Update, sin lógica de gameplay) ---

        private void WireButtons()
        {
            if (_pauseMenu.PauseIconButton != null)
                _pauseMenu.PauseIconButton.onClick.AddListener(OnPauseClicked);
            if (_pauseMenu.ResumeButton != null)
                _pauseMenu.ResumeButton.onClick.AddListener(OnResumeClicked);
            if (_pauseMenu.RestartButton != null)
                _pauseMenu.RestartButton.onClick.AddListener(OnRestartClicked);
            if (_pauseMenu.OptionsButton != null)
                _pauseMenu.OptionsButton.onClick.AddListener(OnOptionsClicked);
            if (_pauseMenu.ReturnToMainMenuButton != null)
                _pauseMenu.ReturnToMainMenuButton.onClick.AddListener(OnReturnToMainMenuClicked);

            // Botones del EndParent: reutilizan los mismos callbacks de flujo del GameManager.
            if (_endMenu.RestartButton != null)
                _endMenu.RestartButton.onClick.AddListener(OnRestartClicked);
            if (_endMenu.ReturnToMainMenuButton != null)
                _endMenu.ReturnToMainMenuButton.onClick.AddListener(OnReturnToMainMenuClicked);
        }

        private void UnwireButtons()
        {
            if (_pauseMenu.PauseIconButton != null)
                _pauseMenu.PauseIconButton.onClick.RemoveListener(OnPauseClicked);
            if (_pauseMenu.ResumeButton != null)
                _pauseMenu.ResumeButton.onClick.RemoveListener(OnResumeClicked);
            if (_pauseMenu.RestartButton != null)
                _pauseMenu.RestartButton.onClick.RemoveListener(OnRestartClicked);
            if (_pauseMenu.OptionsButton != null)
                _pauseMenu.OptionsButton.onClick.RemoveListener(OnOptionsClicked);
            if (_pauseMenu.ReturnToMainMenuButton != null)
                _pauseMenu.ReturnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);

            if (_endMenu.RestartButton != null)
                _endMenu.RestartButton.onClick.RemoveListener(OnRestartClicked);
            if (_endMenu.ReturnToMainMenuButton != null)
                _endMenu.ReturnToMainMenuButton.onClick.RemoveListener(OnReturnToMainMenuClicked);
        }

        // Cada callback solo delega al GameManager: UIManager no decide estado de juego.
        private void OnPauseClicked() => _gameManager.TogglePause();
        private void OnResumeClicked() => _gameManager.ResumeGame();
        private void OnRestartClicked() => _gameManager.RestartGame();
        private void OnOptionsClicked() => _gameManager.OpenOptions();
        private void OnReturnToMainMenuClicked() => _gameManager.ReturnToMainMenu();

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
            _subscribed = false;
        }

        // --- Paneles de flujo (push por evento del GameManager) ---

        private void OnPauseChanged(bool paused)
        {
            // Al pausar, el EndPanel nunca debe quedar visible: pausePanel y endPanel
            // cuelgan del mismo OverlayCanvas y no deben solaparse. Lo ocultamos explícitamente
            // (simetría con ShowEndPanel, que oculta pausePanel al mostrar el End).
            if (paused)
                SetPanel(endPanel, false);

            SetPanel(pausePanel, paused);
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
            SetPanel(pausePanel, false);
            if (endTitleText != null)
                endTitleText.text = title;
            SetPanel(endPanel, true);
        }

        private void OnGameStarted()
        {
            SetPanel(startPanel, false);
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
            SetCanvasActive(_canvases.HealthCanvas, visible);
            SetCanvasActive(_canvases.WaveCanvas, visible);
            SetCanvasActive(_canvases.EnemiesLeftCanvas, visible);
            SetCanvasActive(_canvases.WeaponCanvas, visible);
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
            if (_texts.HealthValueText != null)
            {
                string s = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
                if (s != _lastHealthValueString)
                {
                    _lastHealthValueString = s;
                    _texts.HealthValueText.text = s;
                }
            }

            // La barra de vida (opcional) sigue siendo la representación principal.
            if (_texts.HealthBar == null)
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
                _texts.HealthBar.fillAmount = targetFill;
                _healthBarRoutine = null;
                return;
            }

            _healthBarRoutine = StartCoroutine(AnimateHealthBarRoutine(targetFill));
        }

        private IEnumerator AnimateHealthBarRoutine(float targetFill)
        {
            float startFill = _texts.HealthBar.fillAmount;
            float elapsed = 0f;

            while (elapsed < HealthBarAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime; // independiente del timeScale (pausa futura)
                float t = Mathf.Clamp01(elapsed / HealthBarAnimationDuration);
                _texts.HealthBar.fillAmount = Mathf.Lerp(startFill, targetFill, t);
                yield return null;
            }

            _texts.HealthBar.fillAmount = targetFill;
            _healthBarRoutine = null;
        }

        public void UpdateWave(string waveName, int currentWaveIndex, int totalWaves, bool isFinalWave)
        {
            if (_texts.WaveText == null)
                return;

            // Solo el nombre real de la wave; se ignoran índice, total y flag final.
            string s = string.IsNullOrWhiteSpace(waveName) ? "Wave" : waveName;

            if (s != _lastWaveString)
            {
                _lastWaveString = s;
                _texts.WaveText.text = s;
            }
        }

        public void UpdateEnemiesLeft(int enemiesLeft)
        {
            if (_texts.EnemiesLeftText == null)
                return;

            int safeCount = Mathf.Max(0, enemiesLeft);
            string s = $"Enemies: {safeCount}";
            if (s != _lastEnemiesLeftString)
            {
                _lastEnemiesLeftString = s;
                _texts.EnemiesLeftText.text = s;
            }
        }

        /// <summary>
        /// Actualiza la UI del powerup temporal activo (Speed). Push por evento, sin Update.
        /// Si active es false, oculta el CanvasGroup. Si es true, lo muestra, setea el ícono
        /// y ajusta fillAmount = remaining/duration (clamp 0..1) para el vaciado vertical.
        /// </summary>
        public void UpdatePowerUp(bool active, string displayName, Sprite icon, float remaining, float duration)
        {
            if (powerUpCanvasGroup != null)
            {
                powerUpCanvasGroup.alpha = active ? 1f : 0f;
                powerUpCanvasGroup.interactable = active;
                powerUpCanvasGroup.blocksRaycasts = active;
            }

            if (!active || powerUpIconFill == null)
                return;

            if (icon != null)
                powerUpIconFill.sprite = icon;

            float fill = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
            powerUpIconFill.fillAmount = fill;
        }

        /// <summary>
        /// Actualiza la UI del arma temporal activa. HUD separado del powerup de Speed.
        /// Push por evento, sin Update. Si active es false, oculta el CanvasGroup y vacía el fill.
        /// Si es true, lo muestra, setea el ícono (si hay) y ajusta fillAmount = remaining/duration.
        /// </summary>
        public void UpdateTemporaryWeapon(bool active, string displayName, Sprite icon, float remaining, float duration)
        {
            if (weaponPowerUpCanvasGroup != null)
            {
                weaponPowerUpCanvasGroup.alpha = active ? 1f : 0f;
                weaponPowerUpCanvasGroup.interactable = false;
                weaponPowerUpCanvasGroup.blocksRaycasts = false;
            }

            if (weaponPowerUpIconFill == null)
                return;

            if (!active)
            {
                // Limpiar el sprite al ocultar evita que reaparezca un icono stale la próxima vez.
                weaponPowerUpIconFill.sprite = null;
                weaponPowerUpIconFill.fillAmount = 0f;
                return;
            }

            // Asignar siempre (incluido null): si el arma no tiene icono, limpia el anterior
            // en vez de dejar visible el de un arma previa.
            weaponPowerUpIconFill.sprite = icon;

            float fill = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
            weaponPowerUpIconFill.fillAmount = fill;
        }

        public void UpdateWeapon(string weaponName)
        {
            if (_texts.WeaponText == null)
                return;

            string s = $"Weapon: {weaponName}";
            if (s != _lastWeaponString)
            {
                _lastWeaponString = s;
                _texts.WeaponText.text = s;
            }
        }
    }
}
