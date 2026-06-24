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
        public float Duration;
        public Color DebugColor = Color.white;
    }
}
