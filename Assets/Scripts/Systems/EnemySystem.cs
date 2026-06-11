using System.Collections.Generic;
using OptimizationGame.Data;
using OptimizationGame.Interfaces;
using OptimizationGame.Models;
using UnityEngine;

namespace OptimizationGame.Systems
{
    public class EnemySystem : ITickable
    {
        private List<EnemyModel> _enemies;
        private EnemyTypeData _config;
        private int _nextEnemyId;

        public List<EnemyModel> Enemies => _enemies;

        public EnemySystem(EnemyTypeData config)
        {
            _config = config;
            _enemies = new List<EnemyModel>();
            _nextEnemyId = 0;
        }

        public EnemyModel CreateEnemy()
        {
            var enemy = new EnemyModel(
                _nextEnemyId++,
                _config.MaxHealth,
                _config.MoveSpeed,
                _config.Damage
            );
            _enemies.Add(enemy);
            return enemy;
        }

        public void RemoveEnemy(EnemyModel enemy)
        {
            _enemies.Remove(enemy);
        }

        public void Tick(float deltaTime)
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (!_enemies[i].IsAlive)
                {
                    _enemies.RemoveAt(i);
                }
            }
        }

        public void MoveTowardTarget(EnemyModel enemy, Vector3 targetPosition, float deltaTime)
        {
            Vector3 direction = (targetPosition - enemy.Position).normalized;
            float distance = Vector3.Distance(enemy.Position, targetPosition);

            if (distance > _config.StoppingDistance)
            {
                enemy.Position += direction * enemy.MoveSpeed * deltaTime;
            }
        }

        public int AliveCount => _enemies.Count;
    }
}
