using System.Collections.Generic;
using UnityEngine;

namespace OptimizationGame.Data
{
    [CreateAssetMenu(fileName = "GameFlowConfig", menuName = "OptimizationGame/Game Flow Config")]
    public class GameFlowConfig : ScriptableObject
    {
        public List<RoomDefinition> Rooms = new();
    }
}
