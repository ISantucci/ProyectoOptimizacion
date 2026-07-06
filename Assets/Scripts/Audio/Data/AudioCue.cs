using UnityEngine;

namespace OptimizationGame.Data
{
    /// <summary>
    /// Datos de configuración de UN sonido dentro de la AudioLibrary.
    ///
    /// NO es un sistema, NO reproduce audio, NO escucha eventos, NO tiene Update ni
    /// lógica de gameplay. Es sólo un contenedor serializable.
    ///
    /// Agrupa todo lo que pertenece a un sonido (id + clips + parámetros) para evitar
    /// listas paralelas frágiles en la librería (una lista de AudioCue en vez de
    /// listas separadas de ids/clips/volumenes).
    /// </summary>
    [System.Serializable]
    public class AudioCue
    {
        [Tooltip("Intención sonora que este cue configura.")]
        [SerializeField] private SoundId _id = SoundId.None;

        [Tooltip("Variantes del sonido. Si hay varias, el AudioManager elige una al azar.")]
        [SerializeField] private AudioClip[] _clips;

        [Tooltip("Volumen base (0..1).")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 1f;

        [Tooltip("Pitch base de reproducción.")]
        [Range(-3f, 3f)]
        [SerializeField] private float _pitch = 1f;

        [Tooltip("Variación aleatoria de pitch aplicada como +/- sobre el pitch base.")]
        [Range(0f, 1f)]
        [SerializeField] private float _randomPitchRange = 0f;

        [Tooltip("Categoría informativa. No agrega comportamiento en Etapa 1.")]
        [SerializeField] private AudioCategory _category = AudioCategory.SFX;

        public SoundId Id => _id;
        public AudioClip[] Clips => _clips;
        public float Volume => _volume;
        public float Pitch => _pitch;
        public float RandomPitchRange => _randomPitchRange;
        public AudioCategory Category => _category;

        /// <summary>True si el cue tiene al menos un clip asignado (no null).</summary>
        public bool HasClips
        {
            get
            {
                if (_clips == null || _clips.Length == 0) return false;
                for (int i = 0; i < _clips.Length; i++)
                {
                    if (_clips[i] != null) return true;
                }
                return false;
            }
        }
    }

    /// <summary>
    /// Categoría informativa del sonido. En Etapa 1 sólo sirve para organizar la
    /// librería en el Inspector. No hay mixer ni buses todavía (no sobrearquitecturar).
    /// </summary>
    public enum AudioCategory
    {
        UI,
        SFX,
        Music
    }
}
