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

        public ProjectileModel(int id, float speed, float damage, float maxDistance)
        {
            ID = id;
            Speed = speed;
            Damage = damage;
            _maxDistance = maxDistance;
            Position = Vector3.zero;
            Direction = Vector3.forward;
            TraveledDistance = 0;
        }

        public bool HasReachedMaxDistance => TraveledDistance >= _maxDistance;
    }
}
