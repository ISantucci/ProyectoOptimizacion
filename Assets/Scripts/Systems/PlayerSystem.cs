using OptimizationGame.Data;
using OptimizationGame.Interfaces;
using OptimizationGame.Models;
using UnityEngine;

namespace OptimizationGame.Systems
{
    public class PlayerSystem : ITickable
    {
        private PlayerModel _model;
        private PlayerConfig _config;
        private Vector3 _moveInput;
        private Vector3 _moveDirection;
        private float _fireCooldownTimer;

        public PlayerModel Model => _model;

        public PlayerSystem(PlayerModel model, PlayerConfig config)
        {
            _model = model;
            _config = config;
            _fireCooldownTimer = 0;
        }

        public void SetMoveInput(Vector3 input)
        {
            _moveInput = input.normalized;
        }

        public void SetLookDirection(Vector3 direction)
        {
            _moveDirection = direction.normalized;
        }

        public void Tick(float deltaTime)
        {
            UpdatePosition(deltaTime);
            UpdateFireCooldown(deltaTime);
        }

        private void UpdatePosition(float deltaTime)
        {
            _model.Position += _moveInput * _config.MoveSpeed * deltaTime;
        }

        private void UpdateFireCooldown(float deltaTime)
        {
            if (_fireCooldownTimer > 0)
                _fireCooldownTimer -= deltaTime;
        }

        public bool CanFire()
        {
            return _fireCooldownTimer <= 0;
        }

        public void Fire()
        {
            _fireCooldownTimer = _config.FireCooldown;
        }

        public Vector3 GetFireDirection()
        {
            return _moveDirection != Vector3.zero ? _moveDirection : Vector3.forward;
        }

        public ProjectileModel CreateProjectile(int projectileId)
        {
            var projectile = new ProjectileModel(
                projectileId,
                _config.ProjectileSpeed,
                _config.ProjectileDamage,
                _config.ProjectileMaxDistance
            );
            projectile.Position = _model.Position;
            projectile.Direction = GetFireDirection();
            return projectile;
        }
    }
}
