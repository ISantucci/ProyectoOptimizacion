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
        private WeaponData _baseWeapon;
        private WeaponData _activeWeapon;
        private Vector3 _moveInput;
        private Vector3 _moveDirection;
        private float _fireCooldownTimer;

        public PlayerModel Model => _model;

        public PlayerSystem(PlayerModel model, PlayerConfig config, WeaponData baseWeapon)
        {
            _model = model;
            _config = config;
            _baseWeapon = baseWeapon;
            // Por ahora el arma activa es siempre la base. Pickups/armas temporales: bloque futuro.
            _activeWeapon = _baseWeapon;
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
            // Lee del arma activa si existe; fallback a PlayerConfig (legacy) si es null.
            _fireCooldownTimer = _activeWeapon != null ? _activeWeapon.FireCooldown : _config.FireCooldown;
        }

        public Vector3 GetFireDirection()
        {
            // Disparo siempre en plano XZ.
            Vector3 dir = _moveDirection;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        }

        public ProjectileModel CreateProjectile(int projectileId)
        {
            // Datos del proyectil desde el arma activa; fallback a PlayerConfig (legacy) si es null.
            float speed = _activeWeapon != null ? _activeWeapon.ProjectileSpeed : _config.ProjectileSpeed;
            float damage = _activeWeapon != null ? _activeWeapon.Damage : _config.ProjectileDamage;
            float maxDistance = _activeWeapon != null ? _activeWeapon.ProjectileMaxDistance : _config.ProjectileMaxDistance;

            var projectile = new ProjectileModel(
                projectileId,
                speed,
                damage,
                maxDistance
            );
            projectile.Position = _model.Position;
            projectile.Direction = GetFireDirection();
            return projectile;
        }
    }
}
