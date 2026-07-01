using System;
using System.Collections.Generic;
using OptimizationGame.Interfaces;
using OptimizationGame.Models;
using UnityEngine;

namespace OptimizationGame.Systems
{
    /// <summary>
    /// Sistema puro + ITickable. Mantiene los pickups activos, detecta cuándo el jugador
    /// los recoge (por distancia) y expone la lista de recogidos para que el orquestador
    /// aplique efectos y devuelva las views al pool.
    ///
    /// NO aplica efectos, NO toca UI, NO toca ObjectPool. Solo lógica de estado/proximidad.
    /// </summary>
    public class PickupSystem : ITickable
    {
        private readonly List<PickupModel> _activePickups = new List<PickupModel>();
        private readonly List<PickupModel> _collectedPickups = new List<PickupModel>();
        private readonly List<PickupModel> _expiredPickups = new List<PickupModel>();
        private readonly Func<Vector3> _getPlayerPosition;

        public PickupSystem(Func<Vector3> getPlayerPosition)
        {
            _getPlayerPosition = getPlayerPosition;
        }

        public IReadOnlyList<PickupModel> ActivePickups => _activePickups;
        public IReadOnlyList<PickupModel> CollectedPickups => _collectedPickups;
        public IReadOnlyList<PickupModel> ExpiredPickups => _expiredPickups;

        public void AddPickup(PickupModel pickup)
        {
            if (pickup == null)
                return;
            _activePickups.Add(pickup);
        }

        public void ClearCollectedPickups()
        {
            _collectedPickups.Clear();
        }

        public void ClearExpiredPickups()
        {
            _expiredPickups.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (_activePickups.Count == 0)
                return;

            Vector3 playerPos = _getPlayerPosition();

            for (int i = _activePickups.Count - 1; i >= 0; i--)
            {
                var pickup = _activePickups[i];
                if (!pickup.IsActive)
                {
                    _activePickups.RemoveAt(i);
                    continue;
                }

                // 1) Descontar lifetime y recalcular titileo (estado en el modelo).
                pickup.TickLifetime(deltaTime);

                // 2) Recolección por proximidad (XZ). Tiene PRIORIDAD sobre la expiración:
                //    si en el mismo frame se recoge y vence, gana la recolección.
                Vector3 diff = pickup.Position - playerPos;
                diff.y = 0f;

                if (diff.sqrMagnitude <= pickup.PickupRadius * pickup.PickupRadius)
                {
                    pickup.MarkCollected();
                    _activePickups.RemoveAt(i);
                    _collectedPickups.Add(pickup);
                    continue;
                }

                // 3) Expiración: no recogido y se agotó su lifetime.
                if (pickup.IsExpired)
                {
                    pickup.MarkExpired();
                    _activePickups.RemoveAt(i);
                    _expiredPickups.Add(pickup);
                }
            }
        }
    }
}
