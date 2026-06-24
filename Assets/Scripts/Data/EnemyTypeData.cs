using UnityEngine;

namespace OptimizationGame.Data
{
    [CreateAssetMenu(fileName = "EnemyTypeData", menuName = "OptimizationGame/Enemy Type Data")]
    public class EnemyTypeData : ScriptableObject
    {
        public string EnemyId = "BasicEnemy";
        public string DisplayName = "Basic Enemy";
        public float MaxHealth = 30f;
        public float MoveSpeed = 8f;
        public float Damage = 10f;
        public float StoppingDistance = 1.2f;
        public bool IsBoss = false;
        public Color DebugColor = Color.red;
    }
}
