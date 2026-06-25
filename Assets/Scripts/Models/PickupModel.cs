using UnityEngine;
using OptimizationGame.Data;

namespace OptimizationGame.Models
{
    /// <summary>
    /// Modelo puro de un pickup activo en el mundo (NO MonoBehaviour, NO depende de GameObject).
    /// La parte visual se maneja afuera con una EntityView del pool.
    /// </summary>
    public class PickupModel
    {
        public int Id { get; }
        public PickupData Data { get; }
        public Vector3 Position { get; }
        public float PickupRadius { get; }
        public bool IsActive { get; private set; }

        public PickupModel(int id, PickupData data, Vector3 position, float pickupRadius)
        {
            Id = id;
            Data = data;
            Position = position;
            PickupRadius = pickupRadius;
            IsActive = true;
        }

        /// <summary>Marca el pickup como recogido/inactivo.</summary>
        public void MarkCollected()
        {
            IsActive = false;
        }
    }
}
