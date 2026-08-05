using UnityEngine;
using OptimizationGame.Data;

namespace OptimizationGame.Models
{
    /// <summary>
    /// Modelo puro (NO MonoBehaviour, NO depende de GameObject) del telegraph de spawn:
    /// la "marca" que aparece en el piso avisando DÓNDE va a spawnear el próximo enemigo.
    /// Descuenta un countdown fijo y titila cada vez más rápido a medida que se acaba el
    /// tiempo. Cuando el countdown llega a 0 (IsFinished) el enemigo debe spawnear ahí.
    ///
    /// La parte visual se maneja afuera con una EntityView del pool: este modelo solo
    /// expone estado (ShouldBeVisible, IsFinished) que el orquestador lee. Mismo patrón
    /// de titileo que PickupModel, pero acelerando de forma continua en vez de por tramos.
    /// </summary>
    public class SpawnMarkerModel
    {
        // Duración del aviso antes de que aparezca el enemigo. 1s según el diseño.
        public const float DefaultDuration = 1f;

        // Medio-período de parpadeo al inicio (lento) y al final (rápido) del countdown.
        // El titileo interpola entre estos dos según cuánto tiempo queda: al principio
        // parpadea lento, y se acelera progresivamente hasta el spawn.
        private const float SlowBlinkHalfPeriod = 0.18f;
        private const float FastBlinkHalfPeriod = 0.035f;

        public EnemyTypeData EnemyType { get; }
        public Vector3 Position { get; }
        public float Duration { get; }

        // Countdown / titileo. ShouldBeVisible lo lee el orquestador para EntityView.SetVisible.
        public float RemainingTime { get; private set; }
        public bool IsFinished => RemainingTime <= 0f;
        public bool ShouldBeVisible { get; private set; }
        private float _blinkTimer;

        public SpawnMarkerModel(EnemyTypeData enemyType, Vector3 position, float duration = DefaultDuration)
        {
            EnemyType = enemyType;
            Position = position;
            Duration = duration > 0f ? duration : DefaultDuration;
            RemainingTime = Duration;
            ShouldBeVisible = true;
        }

        /// <summary>
        /// Descuenta el countdown y recalcula la visibilidad (titileo que acelera).
        /// La llama SpawnTelegraphSystem.Tick(deltaTime). No toca EntityView (eso vive
        /// en el orquestador). Cuando RemainingTime llega a 0, IsFinished pasa a true.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (RemainingTime <= 0f)
                return;

            RemainingTime -= deltaTime;
            if (RemainingTime < 0f)
                RemainingTime = 0f;

            float halfPeriod = GetBlinkHalfPeriod(RemainingTime);

            _blinkTimer += deltaTime;
            if (_blinkTimer >= halfPeriod)
            {
                _blinkTimer = 0f;
                ShouldBeVisible = !ShouldBeVisible;
            }
        }

        // Medio-período de parpadeo según tiempo restante: interpola de lento (al inicio,
        // progress = 0) a rápido (al final, progress = 1). Cuanto menos tiempo queda, más
        // rápido el titileo.
        private float GetBlinkHalfPeriod(float remaining)
        {
            float progress = Duration > 0f ? 1f - Mathf.Clamp01(remaining / Duration) : 1f;
            return Mathf.Lerp(SlowBlinkHalfPeriod, FastBlinkHalfPeriod, progress);
        }
    }
}
