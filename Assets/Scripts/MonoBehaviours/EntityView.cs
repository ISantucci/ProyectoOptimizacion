using OptimizationGame.Interfaces;
using UnityEngine;

namespace OptimizationGame.MonoBehaviours
{
    public class EntityView : MonoBehaviour, IPoolable
    {
        private Renderer _renderer;
        // Cache opcional: solo si el prefab usa SpriteRenderer (pickups 2D). Puede quedar null.
        private SpriteRenderer _spriteRenderer;
        private bool _spriteRendererCached;

        private void OnEnable()
        {
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();
        }

        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }

        public void SetRotation(Quaternion rotation)
        {
            transform.rotation = rotation;
        }

        public void SetScale(Vector3 scale)
        {
            transform.localScale = scale;
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
                _spriteRenderer = GetComponent<SpriteRenderer>();
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
                _spriteRenderer = GetComponent<SpriteRenderer>();
                _spriteRendererCached = true;
            }

            if (_spriteRenderer != null)
                _spriteRenderer.enabled = visible;
        }

        public void OnSpawned()
        {
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            gameObject.SetActive(false);
        }
    }
}
