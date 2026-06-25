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
        // Tiempo de vida del pickup en el piso. Constante interna por ahora (no en Inspector).
        public const float DefaultLifetime = 3f;

        // Umbrales de titileo (segundos restantes) y medio-período de parpadeo en cada tramo.
        private const float StableThreshold = 1.5f;   // > 1.5s: visible estable
        private const float FastThreshold = 0.7f;     // <= 0.7s: titileo rápido
        private const float SlowBlinkHalfPeriod = 0.15f;
        private const float FastBlinkHalfPeriod = 0.05f;

        public int Id { get; }
        public PickupData Data { get; }
        public Vector3 Position { get; }
        public float PickupRadius { get; }
        public bool IsActive { get; private set; }

        // Lifetime / titileo. ShouldBeVisible lo lee el orquestador para EntityView.SetVisible.
        public float RemainingLifetime { get; private set; }
        public bool IsExpired => RemainingLifetime <= 0f;
        public bool ShouldBeVisible { get; private set; }
        private float _blinkTimer;

        public PickupModel(int id, PickupData data, Vector3 position, float pickupRadius)
        {
            Id = id;
            Data = data;
            Position = position;
            PickupRadius = pickupRadius;
            IsActive = true;
            RemainingLifetime = DefaultLifetime;
            ShouldBeVisible = true;
        }

        /// <summary>
        /// Descuenta lifetime y recalcula visibilidad (titileo progresivo). La llama
        /// PickupSystem.Tick(deltaTime). No toca EntityView (eso vive en el orquestador).
        /// </summary>
        public void TickLifetime(float deltaTime)
        {
            if (RemainingLifetime <= 0f)
                return;

            RemainingLifetime -= deltaTime;
            if (RemainingLifetime < 0f)
                RemainingLifetime = 0f;

            float halfPeriod = GetBlinkHalfPeriod(RemainingLifetime);
            if (halfPeriod <= 0f)
            {
                // Tramo estable: siempre visible y reset del acumulador.
                _blinkTimer = 0f;
                ShouldBeVisible = true;
                return;
            }

            _blinkTimer += deltaTime;
            if (_blinkTimer >= halfPeriod)
            {
                _blinkTimer = 0f;
                ShouldBeVisible = !ShouldBeVisible;
            }
        }

        // Medio-período de parpadeo según tiempo restante. 0 => sin titileo (visible estable).
        private static float GetBlinkHalfPeriod(float remaining)
        {
            if (remaining > StableThreshold)
                return 0f;
            if (remaining > FastThreshold)
                return SlowBlinkHalfPeriod;
            return FastBlinkHalfPeriod;
        }

        /// <summary>Marca el pickup como recogido/inactivo.</summary>
        public void MarkCollected()
        {
            IsActive = false;
        }

        /// <summary>Marca el pickup como expirado/inactivo (no recogido, se agotó su lifetime).</summary>
        public void MarkExpired()
        {
            IsActive = false;
        }
    }
}
