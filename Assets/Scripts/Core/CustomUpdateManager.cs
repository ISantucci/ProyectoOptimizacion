using System.Collections.Generic;
using OptimizationGame.Interfaces;
using UnityEngine;

namespace OptimizationGame.Core
{
    public class CustomUpdateManager : MonoBehaviour
    {
        // Tickables que se detienen durante la pausa/fin de juego (gameplay puro).
        private readonly List<ITickable> _pausableTickables = new();
        // Tickables que siguen corriendo aunque el juego esté pausado.
        // Caso clave: InputReader, para poder despausar con Escape.
        private readonly List<ITickable> _alwaysTickables = new();

        // Pausa del loop. La activa GameManager en pausa por Escape y en estados
        // finales (Defeat/Victory) para frenar la simulación de los sistemas puros.
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Registra un tickable. Si pauseWithGameplay es true (default) se detiene
        /// durante la pausa; si es false (InputReader) sigue tickeando siempre.
        /// </summary>
        public void Register(ITickable tickable, bool pauseWithGameplay = true)
        {
            if (tickable == null)
                return;

            if (pauseWithGameplay)
            {
                if (!_pausableTickables.Contains(tickable))
                    _pausableTickables.Add(tickable);
            }
            else
            {
                if (!_alwaysTickables.Contains(tickable))
                    _alwaysTickables.Add(tickable);
            }
        }

        public void Unregister(ITickable tickable)
        {
            _pausableTickables.Remove(tickable);
            _alwaysTickables.Remove(tickable);
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            // Siempre-activos primero (InputReader): permiten despausar con Escape.
            for (int i = 0; i < _alwaysTickables.Count; i++)
                _alwaysTickables[i].Tick(deltaTime);

            if (IsPaused)
                return;

            for (int i = 0; i < _pausableTickables.Count; i++)
                _pausableTickables[i].Tick(deltaTime);
        }
    }
}
