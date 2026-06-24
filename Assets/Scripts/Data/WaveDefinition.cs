using System;
using System.Collections.Generic;

namespace OptimizationGame.Data
{
    [Serializable]
    public class WaveDefinition
    {
        public string WaveName;
        public List<WaveEnemyEntry> Enemies = new();
        public float SpawnDelay;
        public int MaxEnemiesAlive;
        public float DelayBeforeNextWave;
    }
}
