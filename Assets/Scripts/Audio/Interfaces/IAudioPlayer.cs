using OptimizationGame.Data;

namespace OptimizationGame.Interfaces
{
    /// <summary>
    /// Contrato mínimo para reproducir audio por intención.
    ///
    /// Permite que otras capas (UI, y en Etapa 2 el GameplayAudioObserver) pidan un
    /// sonido sin depender del tipo concreto AudioManager. Se inyecta por referencia
    /// desde el composition root cuando llegue la integración.
    ///
    /// Deliberadamente chico: sin PlayAt, música, fade, pausa ni mixer todavía.
    /// </summary>
    public interface IAudioPlayer
    {
        void Play(SoundId id);
    }
}
