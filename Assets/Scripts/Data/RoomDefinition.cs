using System;
using System.Collections.Generic;

namespace OptimizationGame.Data
{
    [Serializable]
    public class RoomDefinition
    {
        public string RoomId;
        public string RoomName;
        public RoomType RoomType;
        public List<WaveDefinition> Waves = new();
        public float DelayBeforeNext;
        public bool IsBossRoom;
    }
}
