using System.Collections.Generic;
using OptimizationGame.Interfaces;
using UnityEngine;

namespace OptimizationGame.Core
{
    public class CustomUpdateManager : MonoBehaviour
    {
        private List<ITickable> _tickables = new();

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
            float deltaTime = Time.deltaTime;
            for (int i = 0; i < _tickables.Count; i++)
            {
                _tickables[i].Tick(deltaTime);
            }
        }
    }
}
