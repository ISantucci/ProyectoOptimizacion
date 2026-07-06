using System.Collections.Generic;
using UnityEngine;

namespace OptimizationGame.Data
{
    /// <summary>
    /// ScriptableObject que guarda la configuración de sonidos del juego.
    ///
    /// Responsabilidad única: mapear SoundId -> AudioCue y permitir buscarlo.
    /// No reproduce audio (eso es del AudioManager) y no conoce gameplay.
    ///
    /// Tolerante a configuración incompleta: no crashea si falta un cue o si un cue
    /// no tiene clips; devuelve false y deja que el AudioManager loguee el warning.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "OptimizationGame/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        [Tooltip("Un AudioCue por SoundId. Evitar duplicados: gana el primero encontrado.")]
        [SerializeField] private List<AudioCue> _cues = new();

        // Índice para búsqueda O(1). Se construye perezosamente la primera vez y se
        // invalida si el asset se re-serializa en el Editor (OnValidate).
        private Dictionary<SoundId, AudioCue> _lookup;

        /// <summary>
        /// Busca el AudioCue asociado a un SoundId.
        /// Devuelve false para SoundId.None, cue inexistente o cue sin clips válidos.
        /// </summary>
        public bool TryGetCue(SoundId id, out AudioCue cue)
        {
            cue = null;

            if (id == SoundId.None)
                return false;

            EnsureLookup();

            if (!_lookup.TryGetValue(id, out cue) || cue == null)
            {
                cue = null;
                return false;
            }

            if (!cue.HasClips)
            {
                // El cue existe pero está mal configurado (sin clips). No es un match útil.
                cue = null;
                return false;
            }

            return true;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<SoundId, AudioCue>(_cues.Count);
            for (int i = 0; i < _cues.Count; i++)
            {
                AudioCue c = _cues[i];
                if (c == null || c.Id == SoundId.None)
                    continue;

                if (!_lookup.ContainsKey(c.Id))
                    _lookup[c.Id] = c;
                else
                    Debug.LogWarning($"[AudioLibrary] SoundId duplicado '{c.Id}' en {name}. Se ignora la copia.");
            }
        }

#if UNITY_EDITOR
        // Invalida el índice al editar el asset para que refleje cambios en Play Mode.
        private void OnValidate() => _lookup = null;
#endif
    }
}
