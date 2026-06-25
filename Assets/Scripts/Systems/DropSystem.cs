using OptimizationGame.Data;
using UnityEngine;

namespace OptimizationGame.Systems
{
    /// <summary>
    /// Clase pura (NO MonoBehaviour, NO ITickable en este bloque).
    /// Resuelve la ruleta de drops de dos niveles. Nunca crashea ante config invalida.
    /// </summary>
    public class DropSystem
    {
        /// <summary>
        /// Intenta resolver un drop para el tipo de enemigo dado.
        /// Devuelve true y un PickupData via out si hay drop; false en caso contrario.
        /// </summary>
        public bool TryRollDrop(EnemyTypeData enemyType, out PickupData pickup)
        {
            pickup = null;

            if (enemyType == null)
                return false;

            DropTableData table = enemyType.DropTable;
            if (table == null)
                return false;

            // Nivel 0: chance global de dropear algo.
            if (table.OverallDropChance <= 0f)
                return false;

            if (table.OverallDropChance < 1f && Random.value > table.OverallDropChance)
                return false;

            // Nivel 1: elegir categoria por peso.
            DropTableData.CategoryEntry category = PickCategory(table);
            if (category == null)
                return false;

            // Nivel 2: elegir pickup por peso dentro de la categoria.
            PickupData chosen = PickPickup(category);
            if (chosen == null)
                return false;

            pickup = chosen;
            return true;
        }

        private DropTableData.CategoryEntry PickCategory(DropTableData table)
        {
            var entries = table.CategoryEntries;
            if (entries == null || entries.Count == 0)
                return null;

            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.Weight > 0f)
                    total += e.Weight;
            }

            if (total <= 0f)
                return null;

            float roll = Random.Range(0f, total);
            float acc = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.Weight <= 0f)
                    continue;

                acc += e.Weight;
                if (roll <= acc)
                    return e;
            }

            return null;
        }

        private PickupData PickPickup(DropTableData.CategoryEntry category)
        {
            var drops = category.Drops;
            if (drops == null || drops.Count == 0)
                return null;

            float total = 0f;
            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                if (d != null && d.Pickup != null && d.Weight > 0f)
                    total += d.Weight;
            }

            if (total <= 0f)
                return null;

            float roll = Random.Range(0f, total);
            float acc = 0f;
            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                if (d == null || d.Pickup == null || d.Weight <= 0f)
                    continue;

                acc += d.Weight;
                if (roll <= acc)
                    return d.Pickup;
            }

            return null;
        }
    }
}
