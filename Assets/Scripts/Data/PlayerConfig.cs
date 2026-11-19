namespace OptimizationGame.Data
{
    public class PlayerConfig
    {
        public float MaxHealth = 10000000f;
        public float MoveSpeed = 15f;
        public float ProjectileSpeed = 50f;
        public float ProjectileDamage = 10f;
        public float ProjectileMaxDistance = 25f;
        public float FireCooldown = 0.1f;

        // Nombre de arma temporal para el HUD. Se migrará a WeaponData/WeaponSystem.
        public string WeaponName = "Basic Blaster";
    }
}
