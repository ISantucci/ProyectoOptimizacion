using System;
using System.Collections.Generic;
using OptimizationGame.Data;
using OptimizationGame.Interfaces;
using OptimizationGame.Models;
using UnityEngine;

namespace OptimizationGame.Systems
{
    public class EnemySystem : ITickable
    {
        private List<EnemyModel> _enemies;
        private int _nextEnemyId;
        private Func<Vector3> _getTargetPosition;

        private const float SeparationDistance = 4.5f;
        private const float SeparationDistanceSqr = SeparationDistance * SeparationDistance;
        private const float SeparationWeight = 0.7f;

        // --- Grilla espacial (reemplaza el O(N²) de comparar contra TODOS los enemigos) ---
        // Tamaño de celda = SeparationDistance: matemáticamente, cualquier enemigo dentro
        // del radio de separación cae en la propia celda o en una de las 8 vecinas (3x3),
        // nunca más lejos. Así que revisar el 3x3 alrededor de cada enemigo es suficiente
        // y exacto — no es una aproximación.
        private const float CellSize = SeparationDistance;
        private readonly Dictionary<long, List<EnemyModel>> _grid = new();

        // --- Frecuencia de actualización reducida para CalculateSeparation ---
        // No hace falta recalcular la separación TODOS los frames: es una fuerza que
        // cambia gradualmente. Cada enemigo la recalcula 1 de cada N frames (repartidos
        // por ID para no concentrar el costo en un solo frame) y reutiliza el último
        // valor el resto del tiempo. Con N=3, el costo de CalculateSeparation baja a ~1/3.
        private const int SeparationUpdateInterval = 3;
        private readonly Dictionary<EnemyModel, Vector3> _separationCache = new();
        private int _frameCounter;

        public List<EnemyModel> Enemies => _enemies;

        public EnemySystem(Func<Vector3> getTargetPosition)
        {
            _getTargetPosition = getTargetPosition;
            _enemies = new List<EnemyModel>();
            _nextEnemyId = 0;
        }

        public EnemyModel CreateEnemy(EnemyTypeData enemyType)
        {
            var enemy = new EnemyModel(_nextEnemyId++, enemyType);
            _enemies.Add(enemy);
            return enemy;
        }

        public void RemoveEnemy(EnemyModel enemy)
        {
            _enemies.Remove(enemy);
            _separationCache.Remove(enemy);
        }

        public void Tick(float deltaTime)
        {
            RebuildGrid();
            _frameCounter++;

            Vector3 targetPosition = _getTargetPosition();

            for (int i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];

                if (!enemy.IsAlive)
                    continue;

                MoveTowardTargetWithSeparation(enemy, targetPosition, deltaTime);
            }
        }

        // Se reconstruye una vez por frame, ANTES de mover a nadie (separación contra
        // posiciones del frame anterior, estándar en flocking — un frame de diferencia
        // no se nota). Reutiliza las listas existentes (Clear, no new) para no generar
        // garbage: después del primer par de frames de "calentamiento", esto no aloca.
        private void RebuildGrid()
        {
            foreach (var list in _grid.Values)
                list.Clear();

            for (int i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                if (!enemy.IsAlive)
                    continue;

                long key = CellKey(enemy.Position);
                if (!_grid.TryGetValue(key, out var list))
                {
                    list = new List<EnemyModel>();
                    _grid[key] = list;
                }
                list.Add(enemy);
            }
        }

        private static long CellKey(Vector3 position)
        {
            int cx = Mathf.FloorToInt(position.x / CellSize);
            int cz = Mathf.FloorToInt(position.z / CellSize);
            return CellKey(cx, cz);
        }

        private static long CellKey(int cellX, int cellZ)
        {
            // Empaqueta dos int en un long. Evita ValueTuple como key de Dictionary
            // (más simple, y nos evitamos otra sorpresa de versión de C#, como la de
            // IsExternalInit hace unas semanas).
            return ((long)cellX << 32) | (uint)cellZ;
        }

        private void MoveTowardTargetWithSeparation(EnemyModel enemy, Vector3 targetPosition, float deltaTime)
        {
            float distance = Vector3.Distance(enemy.Position, targetPosition);

            if (distance > enemy.StoppingDistance)
            {
                Vector3 toTarget = targetPosition - enemy.Position;
                toTarget.y = 0f; // movimiento sólo en plano XZ
                Vector3 targetDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;

                Vector3 separationForce = GetSeparationForce(enemy); // ya viene en XZ

                Vector3 combined = targetDirection + separationForce * SeparationWeight;
                combined.y = 0f;
                Vector3 finalDirection = combined.sqrMagnitude > 0.0001f ? combined.normalized : Vector3.zero;

                enemy.Position += finalDirection * enemy.MoveSpeed * deltaTime;
            }
        }

        // Decide si a ESTE enemigo le toca recalcular la separación este frame, o si
        // reutiliza la última calculada. Reparte la carga: en cualquier frame dado,
        // solo ~1/SeparationUpdateInterval de los enemigos recalculan, nunca todos juntos.
        private Vector3 GetSeparationForce(EnemyModel enemy)
        {
            bool shouldRecalculate = (enemy.ID + _frameCounter) % SeparationUpdateInterval == 0;

            if (shouldRecalculate || !_separationCache.TryGetValue(enemy, out Vector3 cached))
            {
                cached = CalculateSeparation(enemy);
                _separationCache[enemy] = cached;
            }

            return cached;
        }

        // OPTIMIZADO (perfilado real confirmó: Scripts 46,2ms vs Rendering 0,5ms,
        // con CalculateSeparation/get_IsAlive/get_Position como top markers).
        // Antes: O(N²), comparaba cada enemigo contra TODOS los demás.
        // Ahora: solo compara contra la celda propia + 8 vecinas (3x3) de la grilla,
        // que matemáticamente contienen a todos los que pueden estar dentro del radio
        // de separación. Con enemigos repartidos por el mapa, esto pasa de revisar
        // N-1 enemigos por cabeza a revisar solo un puñado (los vecinos reales).
        private Vector3 CalculateSeparation(EnemyModel enemy)
        {
            Vector3 separationForce = Vector3.zero;

            int cx = Mathf.FloorToInt(enemy.Position.x / CellSize);
            int cz = Mathf.FloorToInt(enemy.Position.z / CellSize);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    long key = CellKey(cx + dx, cz + dz);
                    if (!_grid.TryGetValue(key, out var cellEnemies))
                        continue;

                    for (int i = 0; i < cellEnemies.Count; i++)
                    {
                        EnemyModel other = cellEnemies[i];

                        // Ignorar a sí mismo (ya filtramos muertos al construir la grilla)
                        if (ReferenceEquals(enemy, other))
                            continue;

                        Vector3 diff = enemy.Position - other.Position;
                        diff.y = 0f; // separación sólo en plano XZ, sin empuje en Y

                        float sqrDist = diff.sqrMagnitude;

                        // Filtro barato: descarta a los que, aun estando en una celda
                        // vecina, terminan fuera del radio real de separación.
                        if (sqrDist >= SeparationDistanceSqr)
                            continue;

                        if (sqrDist <= 0.000001f)
                        {
                            // Casi exactamente en la misma posición: dirección estable
                            // basada en ID. Evita jitter y NaN.
                            Vector3 fallbackDirection = Vector3.right * ((enemy.ID * 73) % 100) * 0.01f +
                                                       Vector3.forward * ((enemy.ID * 131) % 100) * 0.01f;
                            separationForce += fallbackDirection.normalized;
                            continue;
                        }

                        // Sólo acá, para los vecinos realmente cercanos, vale el sqrt.
                        float distToOther = Mathf.Sqrt(sqrDist);
                        float strength = 1f - (distToOther / SeparationDistance);
                        Vector3 awayFromOther = (diff / distToOther) * strength;
                        separationForce += awayFromOther;
                    }
                }
            }

            return separationForce;
        }

        // No se usa: es un wrapper público de MoveTowardTargetWithSeparation que no se
        // llama desde ningún lado (el movimiento real ocurre internamente en Tick()).
        // Se deja comentado por si en algún momento un sistema externo necesita forzar
        // el movimiento de un enemigo puntual fuera del Tick normal.
        //
        // public void MoveTowardTarget(EnemyModel enemy, Vector3 targetPosition, float deltaTime)
        // {
        //     MoveTowardTargetWithSeparation(enemy, targetPosition, deltaTime);
        // }

        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _enemies.Count; i++)
                {
                    if (_enemies[i].IsAlive)
                        count++;
                }
                return count;
            }
        }
    }
}