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
