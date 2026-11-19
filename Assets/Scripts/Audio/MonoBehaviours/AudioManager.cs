using OptimizationGame.Data;
using OptimizationGame.Interfaces;
using UnityEngine;

namespace OptimizationGame.MonoBehaviours
{
    /// <summary>
    /// Reproductor de SFX event-driven. Responsabilidad única: recibir un SoundId y
    /// reproducir el sonido correspondiente resolviéndolo contra una AudioLibrary.
    ///
    /// Es MonoBehaviour porque necesita AudioSources. NO tiene Update (audio por evento),
    /// NO usa coroutines, NO es singleton, NO usa FindObjectOfType ni Service Locator, NO
    /// conoce Player/Enemy/Pickup/Wave ni decide qué sonido corresponde a cada evento.
    /// Se asigna por referencia desde el Inspector / composition root.
    ///
    /// Voces (pool de AudioSource): igual que el pool de proyectiles reutiliza objetos,
    /// acá se reutilizan AudioSources creados UNA vez en Awake. Sin Instantiate/Destroy
    /// en gameplay. Así los disparos rápidos no se cortan entre sí (cada uno usa una voz
    /// libre) y el pitch por voz no pisa a los otros sonidos en vuelo.
    /// </summary>
    public class AudioManager : MonoBehaviour, IAudioPlayer
    {
        [Tooltip("Librería que mapea SoundId -> AudioCue. Asignar por Inspector.")]
        [SerializeField] private AudioLibrary _library;

        [Tooltip("Cantidad de AudioSources reutilizables para SFX simultáneos.")]
        [Range(1, 32)]
        [SerializeField] private int _voiceCount = 8;

        // Pool de voces creado en Awake. Nunca se instancia/destruye en gameplay.
        private AudioSource[] _voices;
        // dspTime del último uso de cada voz: permite robar la más antigua si no hay libres.
        private double[] _voiceLastUsed;

        private void Awake()
        {
            if (_voiceCount < 1)
                _voiceCount = 1;

            _voices = new AudioSource[_voiceCount];
            _voiceLastUsed = new double[_voiceCount];

            for (int i = 0; i < _voiceCount; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;
                _voices[i] = voice;
            }

            if (_library == null)
                Debug.LogWarning($"[AudioManager] Sin AudioLibrary asignada en '{name}'. No sonará nada hasta configurarla.");
        }

        /// <summary>
        /// Reproduce el sonido asociado al SoundId. No-op para None y tolerante a
        /// configuración faltante (loguea warning y no crashea).
        /// </summary>
        public void Play(SoundId id)
        {
            if (id == SoundId.None)
                return;

            if (_library == null)
            {
                Debug.LogWarning($"[AudioManager] Play({id}) ignorado: no hay AudioLibrary asignada.");
                return;
            }

            if (!_library.TryGetCue(id, out AudioCue cue))
            {
                Debug.LogWarning($"[AudioManager] No hay AudioCue válido para '{id}' en la librería. Falta el cue o no tiene clips.");
                return;
            }

            AudioClip clip = PickClip(cue);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] El cue '{id}' no tiene clips no-nulos para reproducir.");
                return;
            }

            AudioSource voice = GetVoice();
            voice.pitch = ResolvePitch(cue);
            voice.PlayOneShot(clip, cue.Volume);
        }

        /// <summary>
        /// Devuelve una voz libre (no reproduciendo). Si todas están ocupadas, roba la
        /// que se usó hace más tiempo. Sin Update: la decisión se toma en el momento.
        /// </summary>
        private AudioSource GetVoice()
        {
            int oldestIndex = 0;
            double oldestTime = double.MaxValue;

            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].isPlaying)
                {
                    _voiceLastUsed[i] = AudioSettings.dspTime;
                    return _voices[i];
                }

                if (_voiceLastUsed[i] < oldestTime)
                {
                    oldestTime = _voiceLastUsed[i];
                    oldestIndex = i;
                }
            }

            // Todas ocupadas: reutiliza la más antigua (se corta su sonido en curso).
            _voiceLastUsed[oldestIndex] = AudioSettings.dspTime;
            return _voices[oldestIndex];
        }

        private static AudioClip PickClip(AudioCue cue)
        {
            AudioClip[] clips = cue.Clips;
            if (clips == null || clips.Length == 0)
                return null;

            if (clips.Length == 1)
                return clips[0];

            // Elige una variante al azar. Reintenta acotado por si hay huecos null.
            for (int attempt = 0; attempt < clips.Length; attempt++)
            {
                AudioClip candidate = clips[Random.Range(0, clips.Length)];
                if (candidate != null)
                    return candidate;
            }
            return null;
        }

        private static float ResolvePitch(AudioCue cue)
        {
            if (cue.RandomPitchRange <= 0f)
                return cue.Pitch;

            return cue.Pitch + Random.Range(-cue.RandomPitchRange, cue.RandomPitchRange);
        }
    }
}
