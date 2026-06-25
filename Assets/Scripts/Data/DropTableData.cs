using System;
using System.Collections.Generic;
using UnityEngine;

namespace OptimizationGame.Data
{
    /// <summary>
    /// Tabla de drops de dos niveles (categoria -> pickup), configurable por peso.
    /// Los pesos NO necesitan sumar 100; se normalizan por total de pesos positivos.
    /// Clases internas [Serializable], ninguna es MonoBehaviour.
    /// </summary>
    [CreateAssetMenu(fileName = "DropTableData", menuName = "OptimizationGame/Drop Table Data")]
    public class DropTableData : ScriptableObject
    {
        [Serializable]
        public class DropEntry
        {
            [SerializeField] private PickupData pickup;
            [SerializeField] private float weight;

            public PickupData Pickup => pickup;
            public float Weight => weight;
        }

        [Serializable]
        public class CategoryEntry
        {
            [SerializeField] private DropCategory category;
            [SerializeField] private float weight;
            [SerializeField] private List<DropEntry> drops = new List<DropEntry>();

            public DropCategory Category => category;
            public float Weight => weight;
            public IReadOnlyList<DropEntry> Drops => drops;
        }

        [Range(0f, 1f)]
        [SerializeField] private float overallDropChance = 0.5f;
        [SerializeField] private List<CategoryEntry> categoryEntries = new List<CategoryEntry>();

        /// <summary>Probabilidad [0..1] de que el enemigo dropee algo.</summary>
        public float OverallDropChance => overallDropChance;
        public IReadOnlyList<CategoryEntry> CategoryEntries => categoryEntries;
    }
}
