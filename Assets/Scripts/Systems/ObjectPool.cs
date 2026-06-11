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
            if (!_prefabs.ContainsKey(poolKey))
            {
                _prefabs[poolKey] = prefab;
                _pools[poolKey] = new Queue<EntityView>();
            }
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
                var view = instance.GetComponent<EntityView>();
                if (view != null)
                {
                    var poolable = view as IPoolable;
                    poolable?.OnDespawned();
                    queue.Enqueue(view);
                }
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
                view = instance.GetComponent<EntityView>();
            }

            if (view != null)
            {
                view.gameObject.SetActive(true);
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

            view.gameObject.SetActive(false);
            _pools[poolKey].Enqueue(view);
        }
    }
}
