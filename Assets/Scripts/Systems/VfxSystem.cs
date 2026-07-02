using System.Collections.Generic;
using OptimizationGame.Interfaces;
using OptimizationGame.Models;
using UnityEngine;

namespace OptimizationGame.Systems
{
    /// <summary>
    /// Sistema puro (NO MonoBehaviour) + ITickable. Maneja el ciclo de vida de los VFX
    /// pooled (impacto de bala por ahora; explosión de muerte reutilizará la misma key
    /// más adelante). Toma EntityView del ObjectPool, la reproduce y, al agotarse su
    /// lifetime, la devuelve al pool. No conoce GameManager, Orchestrator ni CombatSystem;
    /// no toca ParticleSystem directo (usa EntityView). No hace Instantiate/Destroy.
    /// </summary>
    public class VfxSystem : ITickable
    {
        private const string ImpactVfxPoolKey = "ImpactVFX";
        // Duración por defecto del VFX de impacto. Ajustable desde código por ahora;
        // conviene alinearla con la Duration del ParticleSystem del prefab.
        private const float DefaultImpactLifetime = 0.6f;

        private readonly ObjectPool _objectPool;
        private readonly List<VfxModel> _activeVfx = new List<VfxModel>();

        public VfxSystem(ObjectPool objectPool)
        {
            _objectPool = objectPool;
        }

        /// <summary>
        /// Spawnea un VFX de impacto en la posición dada usando la key del arma/proyectil.
        /// Si impactPoolKey es null/empty, cae al fallback "ImpactVFX". Degrada silenciosamente
        /// si no hay pool o si la key no está configurada (view null): no crashea ni spamea.
        /// </summary>
        public void SpawnImpact(Vector3 position, string impactPoolKey)
        {
            string key = string.IsNullOrEmpty(impactPoolKey) ? ImpactVfxPoolKey : impactPoolKey;
            Spawn(key, position, DefaultImpactLifetime);
        }

        /// <summary>
        /// Core genérico de spawn de VFX pooled: toma una EntityView del pool por 'poolKey',
        /// la reproduce y la registra con su lifetime para que Tick la devuelva al agotarse.
        /// No conoce armas ni enemigos: solo key + posición + duración. Base para DeathVFX
        /// (bloque futuro). Degrada sin crashear si falta pool/key.
        /// </summary>
        public void Spawn(string poolKey, Vector3 position, float lifetime)
        {
            if (_objectPool == null || string.IsNullOrEmpty(poolKey))
                return;

            var view = _objectPool.Spawn(poolKey, position);
            if (view == null)
                return;

            view.SetPosition(position);
            // Clear antes de Play: la view viene del pool y pudo quedar con partículas de un
            // uso anterior. Reusar solo hace SetActive(true), que NO re-dispara Play On Awake.
            view.ClearParticles();
            view.PlayParticles();

            _activeVfx.Add(new VfxModel(poolKey, view, lifetime));
        }

        public void Tick(float deltaTime)
        {
            if (_activeVfx.Count == 0)
                return;

            // Iteración reversa: permite remover in-place sin saltear índices.
            for (int i = _activeVfx.Count - 1; i >= 0; i--)
            {
                var vfx = _activeVfx[i];
                vfx.RemainingLifetime -= deltaTime;

                if (vfx.RemainingLifetime <= 0f)
                {
                    vfx.View.ClearParticles();
                    _objectPool.Despawn(vfx.PoolKey, vfx.View);
                    _activeVfx.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Devuelve TODOS los VFX activos al pool y limpia la lista. Para el cleanup de
        /// Restart/ReturnToMainMenu (se conectará en el bloque de GameManager): evita VFX
        /// activos acumulados y views perdidas del pool entre runs.
        /// </summary>
        public void ReturnAllActive()
        {
            if (_objectPool == null)
            {
                _activeVfx.Clear();
                return;
            }

            for (int i = _activeVfx.Count - 1; i >= 0; i--)
            {
                var vfx = _activeVfx[i];
                vfx.View.ClearParticles();
                _objectPool.Despawn(vfx.PoolKey, vfx.View);
            }

            _activeVfx.Clear();
        }
    }
}
