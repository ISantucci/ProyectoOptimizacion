using OptimizationGame.Interfaces;
using UnityEngine;

namespace OptimizationGame.MonoBehaviours
{
    public class EntityView : MonoBehaviour, IPoolable
    {
        private Renderer _renderer;

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
