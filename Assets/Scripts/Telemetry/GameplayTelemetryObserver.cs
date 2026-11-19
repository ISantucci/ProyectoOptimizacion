using System;
using OptimizationGame.Core;
using OptimizationGame.Data;

namespace OptimizationGame.Telemetry
{
    /// <summary>
    /// Traduce eventos del GameManager a los eventos de MET-001.1 (mismo patrón que
    /// GameplayAudioObserver). No toca gameplay ni UI y no tiene Update: solo escucha.
    ///
    ///   GameStarted ........ run_start (y run_end "abandon" si había otra partida en curso)
    ///   WaveChanged ........ wave_end de la anterior + wave_start de la nueva
    ///   Victory ............ wave_end de la última + run_end "victory"
    ///   Defeat ............. player_death + run_end "defeat"
    ///   ReturnedToMenu ..... run_end "abandon" si la partida seguía
    ///   PickupCollected .... pickup_collected
    ///   EnemyKilled / PlayerDamagedBy / HealthChanged: contadores, sin evento propio
    /// </summary>
    public sealed class GameplayTelemetryObserver : IDisposable
    {
        private readonly GameManager _gameManager;
        private readonly TelemetrySystem _telemetry;

        private bool _runActive;
        private bool _paused;
        private int _runIndex;
        private int _waveIndex = -1;
        private string _roomId;
        private float _waveStartSeconds;
        private int _waveKills;
        private int _runKills;
        private int _wavesCleared;
        private float _health = 1f;
        private float _maxHealth = 1f;
        private string _lastDamageSource = "unknown";
        private bool _disposed;

        public GameplayTelemetryObserver(GameManager gameManager, TelemetrySystem telemetry)
        {
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

            _gameManager.GameStarted += OnGameStarted;
            _gameManager.Victory += OnVictory;
            _gameManager.Defeat += OnDefeat;
            _gameManager.ReturnedToMenu += OnReturnedToMenu;
            _gameManager.PauseChanged += OnPauseChanged;
            _gameManager.WaveChanged += OnWaveChanged;
            _gameManager.HealthChanged += OnHealthChanged;
            _gameManager.EnemyKilled += OnEnemyKilled;
            _gameManager.PlayerDamagedBy += OnPlayerDamagedBy;
            _gameManager.PickupCollected += OnPickupCollected;
        }

        /// <summary>Boot directo a gameplay (sin menú): no hay GameStarted, se arranca a mano.</summary>
        public void BeginRunFromBoot()
        {
            OnGameStarted();
        }

        private void OnGameStarted()
        {
            if (_runActive)
                EndRun("abandon");

            _runActive = true;
            _paused = false;
            _runIndex++;
            _waveIndex = -1;
            _roomId = null;
            _waveKills = 0;
            _runKills = 0;
            _wavesCleared = 0;
            _lastDamageSource = "unknown";

            _telemetry.NotifyRunStarted();
            _telemetry.SetSampling(true);
            _telemetry.Track(new TelemetryEvent("run_start").With("run_index", _runIndex));
        }

        private void OnWaveChanged(string waveName, int currentIndex, int totalWaves, bool isFinalWave)
        {
            if (!_runActive)
                return;

            // -1: la sala todavía no arrancó su primera oleada (WaveSystem.StartRoom).
            if (currentIndex < 0)
                return;

            string roomId = _gameManager.CurrentRoomId ?? string.Empty;
            if (currentIndex == _waveIndex && roomId == _roomId)
                return;

            if (_waveIndex >= 0)
                EmitWaveEnd();

            // Índice más allá de la última: la sala terminó; no hay oleada nueva que abrir.
            if (totalWaves > 0 && currentIndex >= totalWaves)
            {
                _waveIndex = -1;
                return;
            }

            _waveIndex = currentIndex;
            _roomId = roomId;
            _waveStartSeconds = _telemetry.RunSeconds;
            _waveKills = 0;

            _telemetry.Track(new TelemetryEvent("wave_start")
                .With("wave_index", currentIndex + 1)
                .With("room_id", roomId));
        }

        private void OnVictory()
        {
            if (!_runActive)
                return;

            if (_waveIndex >= 0)
                EmitWaveEnd();
            EndRun("victory");
        }

        private void OnDefeat()
        {
            if (!_runActive)
                return;

            _telemetry.Track(new TelemetryEvent("player_death")
                .With("wave_index", _waveIndex + 1)
                .With("killer_enemy_id", _lastDamageSource)
                .With("run_time_s", _telemetry.RunSeconds));
            EndRun("defeat");
        }

        private void OnReturnedToMenu()
        {
            if (_runActive)
                EndRun("abandon");
        }

        private void OnPauseChanged(bool paused)
        {
            _paused = paused;
            _telemetry.SetSampling(_runActive && !_paused);
        }

        private void OnHealthChanged(float current, float max)
        {
            _health = current;
            _maxHealth = max;
        }

        private void OnEnemyKilled(string enemyId)
        {
            _waveKills++;
            _runKills++;
        }

        private void OnPlayerDamagedBy(string enemyId)
        {
            _lastDamageSource = string.IsNullOrEmpty(enemyId) ? "unknown" : enemyId;
        }

        private void OnPickupCollected(PickupKind kind)
        {
            if (!_runActive)
                return;

            _telemetry.Track(new TelemetryEvent("pickup_collected")
                .With("kind", KindName(kind))
                .With("wave_index", _waveIndex + 1));
        }

        private void EmitWaveEnd()
        {
            int hpPct = _maxHealth > 0f ? (int)Math.Round(100.0 * Math.Max(0f, _health) / _maxHealth) : 0;
            _telemetry.Track(new TelemetryEvent("wave_end")
                .With("wave_index", _waveIndex + 1)
                .With("duration_s", Math.Max(0f, _telemetry.RunSeconds - _waveStartSeconds))
                .With("hp_pct", hpPct)
                .With("kills", _waveKills));
            _wavesCleared++;
            _telemetry.EmitPerfSampleNow();
        }

        private void EndRun(string result)
        {
            _telemetry.Track(new TelemetryEvent("run_end")
                .With("result", result)
                .With("waves_cleared", _wavesCleared)
                .With("run_time_s", _telemetry.RunSeconds)
                .With("kills", _runKills));

            _runActive = false;
            _telemetry.SetSampling(false);
            _telemetry.Flush();
        }

        private static string KindName(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Heal: return "heal";
                case PickupKind.Shield: return "shield";
                case PickupKind.Speed: return "speed";
                case PickupKind.Weapon: return "weapon";
                default: return "other";
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _gameManager.GameStarted -= OnGameStarted;
            _gameManager.Victory -= OnVictory;
            _gameManager.Defeat -= OnDefeat;
            _gameManager.ReturnedToMenu -= OnReturnedToMenu;
            _gameManager.PauseChanged -= OnPauseChanged;
            _gameManager.WaveChanged -= OnWaveChanged;
            _gameManager.HealthChanged -= OnHealthChanged;
            _gameManager.EnemyKilled -= OnEnemyKilled;
            _gameManager.PlayerDamagedBy -= OnPlayerDamagedBy;
            _gameManager.PickupCollected -= OnPickupCollected;
            _disposed = true;
        }
    }
}
