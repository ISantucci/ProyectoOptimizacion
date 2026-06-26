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

        // Speed boost temporal (pickup Speed). No stackea: recoger otro refresca/reemplaza.
        private float _speedMultiplier = 1f;
        private float _speedBoostTimer;
        // Metadatos para la UI del powerup activo (no afectan la simulación).
        private float _speedBoostDuration;
        private string _speedBoostName;
        private Sprite _speedBoostIcon;

        // Arma temporal (pickup Weapon). No stackea: recoger otra reemplaza y reinicia timer.
        // _baseWeapon nunca se pierde; al expirar _activeWeapon vuelve a _baseWeapon.
        // Independiente del speed boost: ambos pueden coexistir con timers separados.
        private float _weaponTimer;
        private float _weaponDuration;
        private string _weaponName;
        private Sprite _weaponIcon;

        public PlayerModel Model => _model;

        // Prefab del proyectil del arma activa (puede ser null = usar proyectil default).
        // GameManager lo consulta para registrar lazy el prefab en el ObjectPool. PlayerSystem
        // NO conoce el ObjectPool: solo expone el dato.
        public GameObject ActiveProjectilePrefab => _activeWeapon != null ? _activeWeapon.ProjectilePrefab : null;

        // --- Estado del speed boost expuesto para la UI (solo lectura) ---
        public bool HasActiveSpeedBoost => _speedBoostTimer > 0f;
        public float SpeedBoostRemaining => _speedBoostTimer;
        public float SpeedBoostDuration => _speedBoostDuration;
        public string SpeedBoostName => _speedBoostName;
        public Sprite SpeedBoostIcon => _speedBoostIcon;

        // --- Estado del arma temporal expuesto para la UI futura (Sub-bloque 5). Solo lectura. ---
        public bool HasTemporaryWeapon => _weaponTimer > 0f;
        public float TemporaryWeaponRemaining => _weaponTimer;
        public float TemporaryWeaponDuration => _weaponDuration;
        public string TemporaryWeaponName => _weaponName;
        public Sprite TemporaryWeaponIcon => _weaponIcon;

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
            UpdateSpeedBoost(deltaTime);
            UpdateTemporaryWeapon(deltaTime);
            UpdatePosition(deltaTime);
            UpdateFireCooldown(deltaTime);
        }

        /// <summary>
        /// Aplica/refresca un speed boost temporal. multiplier es factor sobre MoveSpeed;
        /// duration en segundos. No stackea: el nuevo reemplaza al anterior.
        /// </summary>
        public void ApplySpeedBoost(float multiplier, float duration, string displayName, Sprite icon)
        {
            if (multiplier <= 0f || duration <= 0f)
                return;
            _speedMultiplier = multiplier;
            _speedBoostTimer = duration;
            _speedBoostDuration = duration;
            _speedBoostName = displayName;
            _speedBoostIcon = icon;
        }

        private void UpdateSpeedBoost(float deltaTime)
        {
            if (_speedBoostTimer > 0f)
            {
                _speedBoostTimer -= deltaTime;
                if (_speedBoostTimer <= 0f)
                {
                    _speedBoostTimer = 0f;
                    _speedMultiplier = 1f;
                    _speedBoostDuration = 0f;
                    _speedBoostName = null;
                    _speedBoostIcon = null;
                }
            }
        }

        /// <summary>
        /// Aplica/reemplaza un arma temporal por 'duration' segundos. No stackea: la nueva
        /// reemplaza a la anterior y reinicia el timer. _baseWeapon no se toca. displayName/icon
        /// se guardan para la UI futura (Sub-bloque 5), no se consumen todavía.
        /// </summary>
        public void ApplyTemporaryWeapon(WeaponData weapon, float duration, string displayName, Sprite icon)
        {
            if (weapon == null)
            {
                Debug.LogWarning("PlayerSystem.ApplyTemporaryWeapon: weapon null. No se aplica arma temporal.");
                return;
            }
            if (duration <= 0f)
            {
                Debug.LogWarning("PlayerSystem.ApplyTemporaryWeapon: duration <= 0. No se aplica arma temporal.");
                return;
            }

            _activeWeapon = weapon;
            _weaponTimer = duration;
            _weaponDuration = duration;
            // Nombre robusto para el Weapon HUD futuro: prioriza el displayName del PickupData;
            // si está vacío, cae al DisplayName del arma; si también, al WeaponId.
            _weaponName = ResolveTemporaryWeaponName(weapon, displayName);
            _weaponIcon = icon;
        }

        // Resuelve el nombre a mostrar: displayName (PickupData) > weapon.DisplayName > WeaponId.
        private static string ResolveTemporaryWeaponName(WeaponData weapon, string displayName)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;
            if (!string.IsNullOrWhiteSpace(weapon.DisplayName))
                return weapon.DisplayName;
            return weapon.WeaponId;
        }

        private void UpdateTemporaryWeapon(float deltaTime)
        {
            if (_weaponTimer > 0f)
            {
                _weaponTimer -= deltaTime;
                if (_weaponTimer <= 0f)
                {
                    // Expiró: volver al arma base y limpiar metadatos. El speed boost no se toca.
                    _weaponTimer = 0f;
                    _weaponDuration = 0f;
                    _weaponName = null;
                    _weaponIcon = null;
                    _activeWeapon = _baseWeapon;
                }
            }
        }

        private void UpdatePosition(float deltaTime)
        {
            _model.Position += _moveInput * _config.MoveSpeed * _speedMultiplier * deltaTime;
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
            // AreaRadius ya viaja en el modelo, pero el daño en área NO se aplica todavía
            // (bloque futuro). Por ahora solo se transporta el dato.
            float areaRadius = _activeWeapon != null ? _activeWeapon.AreaRadius : 0f;
            string poolKey = ResolvePoolKey();

            var projectile = new ProjectileModel(
                projectileId,
                speed,
                damage,
                maxDistance,
                areaRadius,
                poolKey
            );
            projectile.Position = _model.Position;
            projectile.Direction = GetFireDirection();
            return projectile;
        }

        // Deriva la pool key del arma activa. Solo usa una key específica si el arma tiene
        // ProjectilePrefab Y WeaponId no vacío; en cualquier otro caso, fallback seguro a
        // "Projectile" (proyectil default). Garantiza que si la key es específica, el prefab
        // existe (GameManager lo registra lazy).
        private string ResolvePoolKey()
        {
            if (_activeWeapon != null &&
                _activeWeapon.ProjectilePrefab != null &&
                !string.IsNullOrEmpty(_activeWeapon.WeaponId))
            {
                return "Projectile_" + _activeWeapon.WeaponId;
            }
            return "Projectile";
        }
    }
}
