using UnityEngine;
using OptimizationGame.Data;

namespace OptimizationGame.Models
{
    public class EnemyModel
    {
        public int ID { get; }
        public string EnemyId { get; }
        public float Health { get; private set; }
        public float MaxHealth { get; }
        public Vector3 Position { get; set; }
        public float MoveSpeed { get; }
        public float Damage { get; }
        public float StoppingDistance { get; }
        public bool IsBoss { get; }

        // Copia los stats desde EnemyTypeData al crearse.
        // No guarda referencia al ScriptableObject: no depende del asset en runtime.
        public EnemyModel(int id, EnemyTypeData data)
        {
            ID = id;
            EnemyId = data.EnemyId;
            MaxHealth = data.MaxHealth;
            Health = data.MaxHealth;
            MoveSpeed = data.MoveSpeed;
            Damage = data.Damage;
            StoppingDistance = data.StoppingDistance;
            IsBoss = data.IsBoss;
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
