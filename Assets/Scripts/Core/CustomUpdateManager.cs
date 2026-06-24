using System.Collections.Generic;
using OptimizationGame.Interfaces;
using UnityEngine;

namespace OptimizationGame.Core
{
    public class CustomUpdateManager : MonoBehaviour
    {
        private List<ITickable> _tickables = new();

        // Pausa simple del loop. GameManager la activa en estados finales
        // (Defeat/Victory) para frenar la simulación de los sistemas puros.
        public bool IsPaused { get; set; }

        public void Register(ITickable tickable)
        {
            if (!_tickables.Contains(tickable))
                _tickables.Add(tickable);
        }

        public void Unregister(ITickable tickable)
        {
            _tickables.Remove(tickable);
        }

        private void Update()
        {
            if (IsPaused)
                return;

            float deltaTime = Time.deltaTime;
            for (int i = 0; i < _tickables.Count; i++)
            {
                _tickables[i].Tick(deltaTime);
            }
        }
    }
}
