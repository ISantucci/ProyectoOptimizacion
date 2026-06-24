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

        // Cooldown de ataque por enemigo (estado puro, sin MonoBehaviour).
        // Inicia en 0 => el primer contacto pega de inmediato.
        private float _attackCooldownTimer;

        public bool CanAttack => _attackCooldownTimer <= 0f;

        public void TickAttackCooldown(float deltaTime)
        {
            if (_attackCooldownTimer > 0f)
                _attackCooldownTimer -= deltaTime;
        }

        public void RegisterAttack(float cooldown)
        {
            _attackCooldownTimer = cooldown;
        }

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
