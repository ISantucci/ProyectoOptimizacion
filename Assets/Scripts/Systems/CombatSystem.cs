using System.Collections.Generic;
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
            // Guard: un proyectil no puede impactar a un enemigo ya muerto (las muertes se
            // procesan al final de HandleCombat, así que muertos pueden seguir en la lista
            // durante el frame). Evita "gastar" un proyectil contra un cadáver pendiente.
            if (projectile == null || enemy == null || !enemy.IsAlive)
                return false;

            float distance = Vector3.Distance(projectile.Position, enemy.Position);
            if (distance <= ProjectileCollisionRadius)
            {
                enemy.TakeDamage(projectile.Damage);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Aplica daño en área alrededor de un punto de impacto. Lógica pura: SOLO aplica
        /// daño, NO remueve enemigos (el despawn lo coordina el orquestador). Usa distancia
        /// XZ (top-down): ignora Y. No daña al enemigo excluido (el del impacto directo, que
        /// ya recibió daño) ni a enemigos muertos. Robusto si enemies es null o radius <= 0.
        /// </summary>
        public void ApplyAreaDamage(Vector3 center, float radius, float damage,
            IEnumerable<EnemyModel> enemies, EnemyModel excludedEnemy)
        {
            if (radius <= 0f || enemies == null)
                return;

            float sqrRadius = radius * radius;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy == excludedEnemy || !enemy.IsAlive)
                    continue;

                float dx = enemy.Position.x - center.x;
                float dz = enemy.Position.z - center.z;
                if (dx * dx + dz * dz <= sqrRadius)
                    enemy.TakeDamage(damage);
            }
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
