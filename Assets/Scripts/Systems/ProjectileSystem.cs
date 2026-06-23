using System.Collections.Generic;
using OptimizationGame.Interfaces;
using OptimizationGame.Models;
using UnityEngine;

namespace OptimizationGame.Systems
{
    public class ProjectileSystem : ITickable
    {
        private List<ProjectileModel> _projectiles;
        private int _nextProjectileId;

        public List<ProjectileModel> Projectiles => _projectiles;

        public ProjectileSystem()
        {
            _projectiles = new List<ProjectileModel>();
            _nextProjectileId = 0;
        }

        public void AddProjectile(ProjectileModel projectile)
        {
            _projectiles.Add(projectile);
        }

        public void RemoveProjectile(ProjectileModel projectile)
        {
            _projectiles.Remove(projectile);
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _projectiles.Count; i++)
            {
                var projectile = _projectiles[i];
                projectile.Position += projectile.Direction * projectile.Speed * deltaTime;
                projectile.TraveledDistance += projectile.Speed * deltaTime;
            }
        }

        public bool CheckCollision(ProjectileModel projectile, Vector3 targetPosition, float collisionRadius = 1f)
        {
            float distance = Vector3.Distance(projectile.Position, targetPosition);
            return distance <= collisionRadius;
        }
    }
}
