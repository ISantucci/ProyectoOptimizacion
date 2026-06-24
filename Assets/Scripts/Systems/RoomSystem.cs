using OptimizationGame.Data;

namespace OptimizationGame.Systems
{
    /// <summary>
    /// Controla CUÁL sala está activa dentro de un GameFlowConfig.
    /// Clase pura (no MonoBehaviour). No conoce Transform, ObjectPool,
    /// EntityView ni GameManager. No tiene lógica de spawn.
    /// </summary>
    public class RoomSystem
    {
        private readonly GameFlowConfig _config;
        private int _currentIndex;
        private bool _started;

        public RoomSystem(GameFlowConfig config)
        {
            _config = config;
            _currentIndex = -1;
            _started = false;
        }

        private int RoomCount =>
            (_config != null && _config.Rooms != null) ? _config.Rooms.Count : 0;

        public bool HasCurrentRoom =>
            _started && _currentIndex >= 0 && _currentIndex < RoomCount;

        public RoomDefinition CurrentRoom =>
            HasCurrentRoom ? _config.Rooms[_currentIndex] : null;

        public string CurrentRoomId =>
            CurrentRoom != null ? CurrentRoom.RoomId : null;

        public bool IsCompleted =>
            _started && _currentIndex >= RoomCount;

        /// <summary>Arranca en la primera sala. Si no hay salas, queda IsCompleted.</summary>
        public void StartFirstRoom()
        {
            _started = true;
            _currentIndex = 0; // si RoomCount == 0, IsCompleted (0 >= 0) será true
        }

        /// <summary>
        /// Avanza a la siguiente sala. Devuelve true si avanzó a una sala válida,
        /// false si ya no quedan salas (en ese caso pasa a estado IsCompleted).
        /// </summary>
        public bool TryAdvanceToNextRoom()
        {
            if (!_started)
                return false;

            if (_currentIndex + 1 < RoomCount)
            {
                _currentIndex++;
                return true;
            }

            _currentIndex = RoomCount; // no hay más salas -> completado
            return false;
        }
    }
}
