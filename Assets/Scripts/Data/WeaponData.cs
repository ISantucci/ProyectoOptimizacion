using UnityEngine;

namespace OptimizationGame.Data
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "OptimizationGame/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string WeaponId;
        public string DisplayName;
        public int Damage;
        public float FireCooldown;
        public float ProjectileSpeed;
        public float ProjectileMaxDistance;
        // NOTA: este campo NO se usa para armas temporales. La duración del arma temporal
        // sale de PickupData.Duration. Se mantiene por compatibilidad con el arma base.
        public float Duration;
        public Color DebugColor = Color.white;

        // Prefab visual propio del proyectil de esta arma. OPCIONAL: si queda null, el
        // disparo usa el proyectil default (key "Projectile"). El pool le crea su EntityView.
        [SerializeField] private GameObject projectilePrefab;
        // Radio de daño en área al impactar. 0 = daño normal a un solo enemigo. >0 = área.
        [SerializeField] private float areaRadius;

        public GameObject ProjectilePrefab => projectilePrefab;
        public float AreaRadius => areaRadius;
        public bool HasAreaDamage => areaRadius > 0f;

        // Intención sonora del disparo de esta arma. Se resuelve a un clip en la
        // AudioLibrary; el arma NO conoce AudioClip ni AudioSource. None = sin sonido.
        [SerializeField] private SoundId _fireSoundId = SoundId.None;
        public SoundId FireSoundId => _fireSoundId;
    }
}
