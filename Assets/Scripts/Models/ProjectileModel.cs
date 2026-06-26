using UnityEngine;

namespace OptimizationGame.Models
{
    public class ProjectileModel
    {
        public int ID { get; }
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }
        public float Speed { get; }
        public float Damage { get; }
        public float TraveledDistance { get; set; }
        private float _maxDistance;

        // Radio de daño en área. 0 = daño normal a un solo enemigo. >0 = área al impactar.
        public float AreaRadius { get; }
        // Key con la que se devuelve la EntityView al ObjectPool. Fallback seguro: "Projectile".
        public string PoolKey { get; }

        public ProjectileModel(int id, float speed, float damage, float maxDistance,
            float areaRadius = 0f, string poolKey = "Projectile")
        {
            ID = id;
            Speed = speed;
            Damage = damage;
            _maxDistance = maxDistance;
            AreaRadius = areaRadius;
            PoolKey = string.IsNullOrEmpty(poolKey) ? "Projectile" : poolKey;
            Position = Vector3.zero;
            Direction = Vector3.forward;
            TraveledDistance = 0;
        }

        public bool HasReachedMaxDistance => TraveledDistance >= _maxDistance;
        public bool HasAreaDamage => AreaRadius > 0f;
    }
}
