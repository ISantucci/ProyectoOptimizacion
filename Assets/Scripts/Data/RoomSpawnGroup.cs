using System;
using System.Collections.Generic;
using UnityEngine;

namespace OptimizationGame.Data
{
    /// <summary>
    /// Define DÓNDE ocurre una sala: ids y puntos de spawn en escena.
    /// Es data serializable (no MonoBehaviour). No contiene lógica de gameplay.
    /// </summary>
    [Serializable]
    public class RoomSpawnGroup
    {
        public string RoomId;
        public List<Transform> SpawnPoints = new List<Transform>();
        public Transform PlayerStart;
    }
}
