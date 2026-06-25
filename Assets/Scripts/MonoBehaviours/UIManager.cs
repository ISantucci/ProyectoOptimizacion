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
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Serializable]
        private class HudTextReferences
        {
            public Image HealthBar;
            public TMP_Text HealthValueText; // opcional: número de vida sobre la barra
            public TMP_Text WaveText;
            public TMP_Text EnemiesLeftText;
            public TMP_Text WeaponText;
        }

        [SerializeField] private GameManager _gameManager;
        [SerializeField] private HudTextReferences _texts = new HudTextReferences();

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
            // Empujar estado inicial después de suscribir, sin depender del orden de Start.
            _gameManager.BroadcastInitialUiState();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || _gameManager == null)
                return;

            _gameManager.HealthChanged += UpdateHealth;
            _gameManager.WaveChanged += UpdateWave;
            _gameManager.EnemiesLeftChanged += UpdateEnemiesLeft;
            _gameManager.WeaponChanged += UpdateWeapon;
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
            _subscribed = false;
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

            string name = string.IsNullOrWhiteSpace(waveName) ? "Wave" : waveName;

            // Ahora sí se usa el progreso real (índice/total) en vez de solo el nombre,
            // como pide la consigna ("progreso de la wave" en la UI in-game).
            string s = totalWaves > 0
                ? $"{name} ({currentWaveIndex + 1}/{totalWaves})"
                : name;

            if (isFinalWave)
                s += " - Final";

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