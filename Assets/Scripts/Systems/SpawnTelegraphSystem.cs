using System.Collections.Generic;
using OptimizationGame.Data;
using OptimizationGame.Interfaces;
using OptimizationGame.Models;
using UnityEngine;

namespace OptimizationGame.Systems
{
    /// <summary>
    /// Sistema puro + ITickable. Mantiene los "telegraphs" de spawn activos (las marcas en
    /// el piso), les descuenta el countdown y su titileo, y expone los que ya terminaron
    /// para que el orquestador spawnee el enemigo en esa posición y devuelva la view al pool.
    ///
    /// NO spawnea enemigos, NO toca UI, NO toca ObjectPool. Solo lógica de estado/tiempo.
    /// Mismo reparto de responsabilidades que PickupSystem: el sistema decide "qué está
    /// listo"; el orquestador ejecuta el efecto visual y el spawn real.
    /// </summary>
    public class SpawnTelegraphSystem : ITickable
    {
        private readonly List<SpawnMarkerModel> _activeMarkers = new List<SpawnMarkerModel>();
        private readonly List<SpawnMarkerModel> _readyMarkers = new List<SpawnMarkerModel>();

        /// <summary>Marcas todavía titilando en el piso (para sincronizar sus views).</summary>
        public IReadOnlyList<SpawnMarkerModel> ActiveMarkers => _activeMarkers;

        /// <summary>Marcas cuyo countdown terminó este frame: el enemigo ya debe spawnear.</summary>
        public IReadOnlyList<SpawnMarkerModel> ReadyMarkers => _readyMarkers;

        /// <summary>
        /// Cuenta las marcas en vuelo. El pacing de waves la suma al conteo de enemigos vivos
        /// para respetar MaxEnemiesAlive (una marca es un enemigo ya "reservado").
        /// </summary>
        public int ActiveCount => _activeMarkers.Count;

        public SpawnMarkerModel CreateMarker(EnemyTypeData enemyType, Vector3 position)
        {
            var marker = new SpawnMarkerModel(enemyType, position);
            _activeMarkers.Add(marker);
            return marker;
        }

        /// <summary>El orquestador la llama tras consumir ReadyMarkers en su Tick.</summary>
        public void ClearReadyMarkers()
        {
            _readyMarkers.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (_activeMarkers.Count == 0)
                return;

            for (int i = _activeMarkers.Count - 1; i >= 0; i--)
            {
                var marker = _activeMarkers[i];
                marker.Tick(deltaTime);

                if (marker.IsFinished)
                {
                    _activeMarkers.RemoveAt(i);
                    _readyMarkers.Add(marker);
                }
            }
        }
    }
}
