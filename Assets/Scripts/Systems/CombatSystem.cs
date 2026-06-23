using OptimizationGame.Models;
using UnityEngine;

namespace OptimizationGame.Systems
{
    public class CombatSystem
    {
        private const float ProjectileCollisionRadius = 1f;
        private const float EnemyDamageRadius = 2f;

        public bool ResolveProjectileEnemyCollision(ProjectileModel projectile, EnemyModel enemy)
        {
            float distance = Vector3.Distance(projectile.Position, enemy.Position);
            if (distance <= ProjectileCollisionRadius)
            {
                enemy.TakeDamage(projectile.Damage);
                return true;
            }
            return false;
        }

        public bool CheckPlayerEnemyCollision(Vector3 playerPos, Vector3 enemyPos, float collisionRadius = 1.5f)
        {
            float distance = Vector3.Distance(playerPos, enemyPos);
            return distance <= collisionRadius;
        }

        public void ApplyEnemyDamageToPlayer(EnemyModel enemy, PlayerModel player)
        {
            player.TakeDamage(enemy.Damage);
        }
    }
}
