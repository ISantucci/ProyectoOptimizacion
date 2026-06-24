using UnityEngine;

namespace OptimizationGame.Data
{
    [CreateAssetMenu(fileName = "PoolConfig", menuName = "OptimizationGame/Pool Config")]
    public class PoolConfig : ScriptableObject
    {
        public int EnemyPrewarm = 32;
        public int ProjectilePrewarm = 24;
        public int VfxPrewarm = 32;
        public int PickupPrewarm = 8;
    }
}
