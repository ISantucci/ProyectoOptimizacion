namespace OptimizationGame.Data
{
    /// <summary>
    /// Identifica una INTENCIÓN sonora, no un archivo de audio.
    /// El código de gameplay/UI pide un SoundId; la AudioLibrary decide qué clip
    /// corresponde y el AudioManager lo reproduce. Así el gameplay nunca conoce clips.
    ///
    /// None = 0 es el valor por defecto/no-op: el AudioManager lo ignora.
    /// Etapa 1: la lista es un catálogo de intenciones. No todas están conectadas
    /// todavía a eventos; la conexión llega en Etapa 2 (GameplayAudioObserver).
    /// </summary>
    public enum SoundId
    {
        None = 0,

        // UI
        UI_Click,
        UI_Hover,
        UI_PauseOpen,
        UI_PauseClose,

        // Player
        Player_Shoot,
        Player_Hit,
        Player_Death,

        // Enemy
        Enemy_Hit,
        Enemy_Death,

        // Pickups
        Pickup_Health,
        Pickup_Speed,
        Pickup_Weapon,

        // Flow / Waves
        Wave_Start,
        Wave_Clear,
        Game_Victory,
        Game_Defeat
    }
}
