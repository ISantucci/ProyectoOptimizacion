using OptimizationGame.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OptimizationGame.MonoBehaviours
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private TextMeshProUGUI _waveText;
        [SerializeField] private TextMeshProUGUI _enemyCountText;
        [SerializeField] private Image _healthBar;

        private float _maxHealth;

        private void Start()
        {
            var playerModel = _gameManager.GetPlayerModel();
            _maxHealth = playerModel.MaxHealth;
        }

        private void Update()
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            var playerModel = _gameManager.GetPlayerModel();

            if (_healthText != null)
                _healthText.text = $"Health: {playerModel.Health:F0}";

            if (_healthBar != null)
                _healthBar.fillAmount = playerModel.Health / _maxHealth;
        }
    }
}
