using System.Collections.Generic;
using OptimizationGame.Interfaces;
using OptimizationGame.MonoBehaviours;
using UnityEngine;

namespace OptimizationGame.Systems
{
    public class ObjectPool
    {
        private Dictionary<string, Queue<EntityView>> _pools = new();
        private Dictionary<string, GameObject> _prefabs = new();

        public void RegisterPrefab(string poolKey, GameObject prefab)
        {
            // Idempotente: si la key ya existe, NO se pisa el prefab ni se vacía la cola.
            // Seguro para registro lazy repetido (mismo arma disparando varias veces).
            if (!_prefabs.ContainsKey(poolKey))
            {
                _prefabs[poolKey] = prefab;
                _pools[poolKey] = new Queue<EntityView>();
            }
        }

        // Permite al composition root decidir si hace falta registrar lazy un prefab nuevo.
        public bool HasPrefab(string poolKey)
        {
            return _prefabs.ContainsKey(poolKey);
        }

        public void Prewarm(string poolKey, int count)
        {
            if (!_pools.ContainsKey(poolKey))
                return;

            var queue = _pools[poolKey];
            var prefab = _prefabs[poolKey];

            for (int i = 0; i < count; i++)
            {
                var instance = Object.Instantiate(prefab);
                // EntityView ahora es wrapper puro: lo crea el pool envolviendo el GameObject.
                var view = new EntityView(instance);
                var poolable = view as IPoolable;
                poolable?.OnDespawned();
                // Red de seguridad: garantiza que el objeto prewarmeado quede inactivo aunque
                // OnDespawned falte/falle. Idempotente si OnDespawned ya lo apagó. Evita que los
                // prefabs (VFX playOnAwake, etc.) queden vivos en el menú durante el prewarm.
                instance.SetActive(false);
                queue.Enqueue(view);
            }
        }

        public EntityView Spawn(string poolKey, Vector3 position)
        {
            if (!_pools.ContainsKey(poolKey))
                return null;

            var queue = _pools[poolKey];
            EntityView view = null;

            if (queue.Count > 0)
            {
                view = queue.Dequeue();
            }
            else
            {
                var prefab = _prefabs[poolKey];
                var instance = Object.Instantiate(prefab);
                view = new EntityView(instance);
            }

            if (view != null)
            {
                view.GameObject.SetActive(true);
                view.SetPosition(position);
                var poolable = view as IPoolable;
                poolable?.OnSpawned();
            }

            return view;
        }

        public void Despawn(string poolKey, EntityView view)
        {
            if (!_pools.ContainsKey(poolKey))
                return;

            var poolable = view as IPoolable;
            poolable?.OnDespawned();

            view.GameObject.SetActive(false);
            _pools[poolKey].Enqueue(view);
        }
    }
}
