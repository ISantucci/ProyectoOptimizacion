using System;
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
        private Func<Vector3> _getTargetPosition;

        private const float SeparationDistance = 4.5f;
        private const float SeparationWeight = 0.7f;

        public List<EnemyModel> Enemies => _enemies;

        public EnemySystem(EnemyTypeData config, Func<Vector3> getTargetPosition)
        {
            _config = config;
            _getTargetPosition = getTargetPosition;
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
            Vector3 targetPosition = _getTargetPosition();

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];

                if (!enemy.IsAlive)
                {
                    _enemies.RemoveAt(i);
                }
                else
                {
                    MoveTowardTargetWithSeparation(enemy, targetPosition, deltaTime);
                }
            }
        }

        private void MoveTowardTargetWithSeparation(EnemyModel enemy, Vector3 targetPosition, float deltaTime)
        {
            float distance = Vector3.Distance(enemy.Position, targetPosition);

            if (distance > _config.StoppingDistance)
            {
                Vector3 targetDirection = (targetPosition - enemy.Position).normalized;
                Vector3 separationForce = CalculateSeparation(enemy);

                Vector3 finalDirection = (targetDirection + separationForce * SeparationWeight).normalized;
                enemy.Position += finalDirection * enemy.MoveSpeed * deltaTime;
            }
        }

        private Vector3 CalculateSeparation(EnemyModel enemy)
        {
            Vector3 separationForce = Vector3.zero;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyModel other = _enemies[i];

                // Ignorar a sí mismo
                if (ReferenceEquals(enemy, other))
                    continue;

                // Ignorar enemigos muertos
                if (!other.IsAlive)
                    continue;

                Vector3 diff = enemy.Position - other.Position;
                float distToOther = diff.magnitude;

                // Si está dentro del radio de separación
                if (distToOther < SeparationDistance && distToOther > 0.001f)
                {
                    // Fuerza proporcional: más cerca = más fuerte
                    // Escala de 0 (máxima cercanía) a 1 (límite del radio)
                    float strength = 1f - (distToOther / SeparationDistance);

                    // Normalizar dirección y aplicar fuerza proporcional
                    Vector3 awayFromOther = (diff / distToOther) * strength;
                    separationForce += awayFromOther;
                }
                else if (distToOther <= 0.001f)
                {
                    // Si están casi exactamente en la misma posición, usar dirección estable basada en ID
                    // Evita jitter y NaN
                    Vector3 fallbackDirection = Vector3.right * ((enemy.ID * 73) % 100) * 0.01f +
                                               Vector3.forward * ((enemy.ID * 131) % 100) * 0.01f;
                    separationForce += fallbackDirection.normalized;
                }
            }

            return separationForce;
        }

        public void MoveTowardTarget(EnemyModel enemy, Vector3 targetPosition, float deltaTime)
        {
            MoveTowardTargetWithSeparation(enemy, targetPosition, deltaTime);
        }

        public int AliveCount => _enemies.Count;
    }
}
