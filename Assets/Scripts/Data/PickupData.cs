using UnityEngine;

namespace OptimizationGame.Data
{
    [CreateAssetMenu(fileName = "PickupData", menuName = "OptimizationGame/Pickup Data")]
    public class PickupData : ScriptableObject
    {
        [SerializeField] private string pickupId;
        [SerializeField] private string displayName;
        [SerializeField] private PickupKind kind;
        [SerializeField] private float amount;
        [SerializeField] private float duration;
        [SerializeField] private WeaponData weaponData;
        [SerializeField] private Color debugColor = Color.white;

        public string PickupId => pickupId;
        public string DisplayName => displayName;
        public PickupKind Kind => kind;
        public float Amount => amount;
        public float Duration => duration;

        // Si Kind == Weapon puede estar asignado; si no, puede ser null.
        public WeaponData WeaponData => weaponData;
        public Color DebugColor => debugColor;
    }
}
