using OptimizationGame.Core;
using OptimizationGame.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OptimizationGame.MonoBehaviours
{
    // Lee el Input System nuevo y reenvía datos/acciones a GameManager.
    // No tiene Update propio: la lectura recurrente corre por ITickable.Tick(dt),
    // registrado en CustomUpdateManager (único frame callback de gameplay).
    // OnEnable/OnDisable solo habilitan/deshabilitan/dispose de las InputActions.
    public class InputReader : MonoBehaviour, ITickable
    {
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private Camera _mainCamera;

        private InputActionMap _gameplayActions;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _fireAction;
        private InputAction _pauseAction;

        private void OnEnable()
        {
            CreateInputActions();
            _gameplayActions.Enable();
        }

        private void OnDisable()
        {
            _gameplayActions.Disable();
            _gameplayActions.Dispose();
            _gameplayActions = null;
        }

        private void CreateInputActions()
        {
            _gameplayActions = new InputActionMap("Gameplay");

            // Movimiento: WASD (2D Vector)
            _moveAction = _gameplayActions.AddAction("Move", InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            // Apuntar: Posición del mouse
            _lookAction = _gameplayActions.AddAction("Look", InputActionType.Value);
            _lookAction.AddBinding("<Mouse>/position");

            // Disparar: Click izquierdo
            _fireAction = _gameplayActions.AddAction("Fire", InputActionType.Button);
            _fireAction.AddBinding("<Mouse>/leftButton");

            // Pausa: Escape (preparado para futuro)
            _pauseAction = _gameplayActions.AddAction("Pause", InputActionType.Button);
            _pauseAction.AddBinding("<Keyboard>/escape");
        }

        // Reemplaza al antiguo Update(): lo tickea el CustomUpdateManager.
        // deltaTime no se usa (la lectura de input no depende del dt).
        public void Tick(float deltaTime)
        {
            // Guard defensivo: si OnEnable aún no creó las acciones (o ya se hizo
            // dispose en OnDisable), no leer para evitar excepciones.
            if (_gameplayActions == null)
                return;

            ReadMovementInput();
            ReadLookInput();
            ReadFireInput();
        }

        private void ReadMovementInput()
        {
            Vector2 moveInput = _moveAction.ReadValue<Vector2>();
            Vector3 moveInput3D = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            _gameManager.SetPlayerInput(moveInput3D);
        }

        private void ReadLookInput()
        {
            Vector2 mousePos = _lookAction.ReadValue<Vector2>();
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.origin + ray.direction * enter;
                Vector3 lookDirection = (hitPoint - _gameManager.GetPlayerModel().Position).normalized;
                _gameManager.SetPlayerLook(lookDirection);
            }
        }

        private void ReadFireInput()
        {
            if (_fireAction.WasPressedThisFrame())
            {
                _gameManager.FireProjectile();
            }
        }
    }
}
