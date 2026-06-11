using UnityEngine;

namespace OptimizationGame.Models
{
    public class PlayerModel
    {
        public float Health { get; private set; }
        public float MaxHealth { get; }
        public Vector3 Position { get; set; }
        public float MoveSpeed { get; }

        public PlayerModel(float maxHealth, float moveSpeed)
        {
            MaxHealth = maxHealth;
            Health = maxHealth;
            MoveSpeed = moveSpeed;
            Position = Vector3.zero;
        }

        public void TakeDamage(float damage)
        {
            Health -= damage;
            if (Health < 0) Health = 0;
        }

        public bool IsAlive => Health > 0;
    }
}
