using System;
using OptimizationGame.Core;
using OptimizationGame.Data;
using OptimizationGame.Events;
using OptimizationGame.Interfaces;

namespace OptimizationGame.Systems
{
    /// <summary>
    /// Puente event-driven entre el gameplay y el audio. Clase pura (no MonoBehaviour):
    /// escucha eventos YA EXISTENTES del GameManager y los traduce a un SoundId, que
    /// delega en IAudioPlayer.Play. No conoce AudioClip ni AudioSource, no toca gameplay
    /// ni UI, no tiene Update y no es singleton.
    ///
    /// El GameManager (composition root) lo crea y es dueño de su ciclo de vida: debe
    /// llamar Dispose() para desuscribirse y no dejar el observer colgado de los eventos.
    ///
    /// Etapa 2 (mínima): solo mapea eventos de flujo (pausa / victoria / derrota).
    /// Disparo, impactos, enemigos, pickups y waves llegan en Etapa 3.
    /// </summary>
    public sealed class GameplayAudioObserver : IDisposable
    {
        private readonly GameManager _gameManager;
        private readonly IAudioPlayer _audioPlayer;
        private bool _disposed;

        public GameplayAudioObserver(GameManager gameManager, IAudioPlayer audioPlayer)
        {
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            _audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));

            _gameManager.PauseChanged += OnPauseChanged;
            _gameManager.Victory += OnVictory;
            _gameManager.Defeat += OnDefeat;
            _gameManager.WeaponFired += OnWeaponFired;
        }

        private void OnPauseChanged(bool paused)
        {
            _audioPlayer.Play(paused ? SoundId.UI_PauseOpen : SoundId.UI_PauseClose);
        }

        private void OnVictory() => _audioPlayer.Play(SoundId.Game_Victory);

        private void OnDefeat() => _audioPlayer.Play(SoundId.Game_Defeat);

        // El arma equipada ya eligió el SoundId; el observer solo lo reenvía al player.
        private void OnWeaponFired(WeaponFiredEvent evt) => _audioPlayer.Play(evt.FireSoundId);

        public void Dispose()
        {
            if (_disposed)
                return;

            _gameManager.PauseChanged -= OnPauseChanged;
            _gameManager.Victory -= OnVictory;
            _gameManager.Defeat -= OnDefeat;
            _gameManager.WeaponFired -= OnWeaponFired;
            _disposed = true;
        }
    }
}
