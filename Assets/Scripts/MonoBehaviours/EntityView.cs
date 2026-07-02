using OptimizationGame.Interfaces;
using UnityEngine;

namespace OptimizationGame.MonoBehaviours
{
    // Wrapper visual PURO (NO MonoBehaviour): lo crea ObjectPool envolviendo el GameObject
    // instanciado de un prefab pooled. Encapsula Transform/Renderer/SpriteRenderer para que
    // los sistemas sincronicen la vista sin tocar Unity visual directamente.
    // Sin mensajes Unity, sin [SerializeField]: el ciclo de vida lo maneja el pool.
    public class EntityView : IPoolable
    {
        private readonly GameObject _gameObject;
        private readonly Transform _transform;
        private readonly Renderer _renderer;
        // Cache opcional: solo si el prefab usa SpriteRenderer (pickups 2D). Puede quedar null.
        private SpriteRenderer _spriteRenderer;
        private bool _spriteRendererCached;
        // Cache opcional: solo si el prefab usa ParticleSystem (VFX). Puede quedar null.
        private ParticleSystem _particleSystem;
        private bool _particleSystemCached;

        public GameObject GameObject => _gameObject;
        public Transform Transform => _transform;

        public EntityView(GameObject gameObject)
        {
            _gameObject = gameObject;
            _transform = gameObject.transform;
            // Antes se cacheaba en OnEnable; ahora una sola vez al construir. GetComponent
            // funciona aunque el GameObject esté inactivo, así que el prewarm no lo afecta.
            _renderer = gameObject.GetComponent<Renderer>();
        }

        public void SetPosition(Vector3 position)
        {
            _transform.position = position;
        }

        public void SetRotation(Quaternion rotation)
        {
            _transform.rotation = rotation;
        }

        public void SetScale(Vector3 scale)
        {
            _transform.localScale = scale;
        }

        public void SetColor(Color color)
        {
            if (_renderer != null && _renderer.material != null)
            {
                _renderer.material.color = color;
            }
        }

        /// <summary>
        /// Cambia el sprite si el prefab tiene un SpriteRenderer. Si no lo tiene, no-op
        /// (no crashea). El cache se resuelve una sola vez. No afecta a SetColor (que usa Renderer).
        /// </summary>
        public void SetSprite(Sprite sprite)
        {
            if (!_spriteRendererCached)
            {
                _spriteRenderer = _gameObject.GetComponent<SpriteRenderer>();
                _spriteRendererCached = true;
            }

            if (_spriteRenderer != null && sprite != null)
                _spriteRenderer.sprite = sprite;
        }

        /// <summary>
        /// Muestra/oculta el visual SIN desactivar el GameObject (no rompe el pool ni
        /// OnSpawned/OnDespawned). Apaga el Renderer y/o el SpriteRenderer si existen.
        /// Si no hay ninguno, no-op (no crashea).
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_renderer != null)
                _renderer.enabled = visible;

            if (!_spriteRendererCached)
            {
                _spriteRenderer = _gameObject.GetComponent<SpriteRenderer>();
                _spriteRendererCached = true;
            }

            if (_spriteRenderer != null)
                _spriteRenderer.enabled = visible;
        }

        // Resuelve el cache del ParticleSystem una sola vez. Búsqueda LOCAL sobre el
        // GameObject envuelto (incluye hijos e inactivos): NO es GameObject.Find ni
        // FindObjectOfType. Queda null si el prefab no tiene VFX (enemigos/proyectiles/pickups).
        private void EnsureParticleSystemCached()
        {
            if (_particleSystemCached)
                return;

            _particleSystem = _gameObject.GetComponentInChildren<ParticleSystem>(true);
            _particleSystemCached = true;
        }

        /// <summary>
        /// Reproduce el VFX desde cero. Necesario porque reusar del pool solo hace
        /// SetActive(true), que NO re-dispara "Play On Awake". Limpia partículas de un uso
        /// anterior antes de reproducir. No-op si el prefab no tiene ParticleSystem.
        /// </summary>
        public void PlayParticles()
        {
            EnsureParticleSystemCached();
            if (_particleSystem == null)
                return;

            _particleSystem.Clear(true);
            _particleSystem.Play(true);
        }

        /// <summary>
        /// Detiene la emisión pero deja vivir las partículas ya emitidas hasta que expiren.
        /// No-op si el prefab no tiene ParticleSystem.
        /// </summary>
        public void StopParticles()
        {
            EnsureParticleSystemCached();
            if (_particleSystem == null)
                return;

            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>
        /// Detiene la emisión y limpia las partículas vivas (retorno limpio al pool).
        /// No-op si el prefab no tiene ParticleSystem.
        /// </summary>
        public void ClearParticles()
        {
            EnsureParticleSystemCached();
            if (_particleSystem == null)
                return;

            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void OnSpawned()
        {
            _gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            _gameObject.SetActive(false);
        }
    }
}
