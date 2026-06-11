namespace OptimizationGame.Models
{
    public class EnemyModel
    {
        public int ID { get; }
        public float Health { get; private set; }
        public float MaxHealth { get; }
        public UnityEngine.Vector3 Position { get; set; }
        public float MoveSpeed { get; }
        public float Damage { get; }

        public EnemyModel(int id, float maxHealth, float moveSpeed, float damage)
        {
            ID = id;
            MaxHealth = maxHealth;
            Health = maxHealth;
            MoveSpeed = moveSpeed;
            Damage = damage;
            Position = UnityEngine.Vector3.zero;
        }

        public void TakeDamage(float damage)
        {
            Health -= damage;
            if (Health < 0) Health = 0;
        }

        public bool IsAlive => Health > 0;
    }
}
