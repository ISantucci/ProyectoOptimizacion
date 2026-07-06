using OptimizationGame.Data;

namespace OptimizationGame.Events
{
    /// <summary>
    /// Evento de GAMEPLAY (no de audio): un arma disparó realmente y su proyectil fue
    /// generado. No representa el click de input, sino un disparo ya validado.
    ///
    /// readonly struct liviano: viaja por valor vía Action&lt;WeaponFiredEvent&gt;, sin pool
    /// de eventos ni EventBus. Transporta solo la intención sonora del disparo; el audio
    /// la escucha y la resuelve a un clip. El gameplay no conoce clips ni AudioSource.
    /// </summary>
    public readonly struct WeaponFiredEvent
    {
        public readonly SoundId FireSoundId;

        public WeaponFiredEvent(SoundId fireSoundId)
        {
            FireSoundId = fireSoundId;
        }
    }
}
