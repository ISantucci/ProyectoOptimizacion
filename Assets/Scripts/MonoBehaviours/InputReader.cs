using System;
using OptimizationGame.Core;
using OptimizationGame.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OptimizationGame.MonoBehaviours
{
    // Lee el Input System nuevo y reenvía datos/acciones a GameManager.
    // Clase pura (NO MonoBehaviour): la crea/posee GameManager (composition root).
    // No tiene Update propio: la lectura recurrente corre por ITickable.Tick(dt),
    // registrado en CustomUpdateManager (único frame callback de gameplay).
    // Initialize() reemplaza al antiguo OnEnable; Dispose() al antiguo OnDisable.
    public class InputReader : ITickable, IDisposable
    {
        private readonly GameManager _gameManager;
        private readonly Camera _mainCamera;

        private InputActionMap _gameplayActions;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _fireAction;
        private InputAction _pauseAction;

        public InputReader(GameManager gameManager, Camera mainCamera)
        {
            _gameManager = gameManager;
            _mainCamera = mainCamera;
        }

        // Reemplaza al antiguo OnEnable(): lo llama GameManager en su inicialización.
        public void Initialize()
        {
            CreateInputActions();
            _gameplayActions.Enable();
        }

        // Reemplaza al antiguo OnDisable(): lo llama GameManager en OnDestroy.
        public void Dispose()
        {
            if (_gameplayActions == null)
                return;

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

            // Pausa: Escape (preparado para futuro, aún no funcional)
            _pauseAction = _gameplayActions.AddAction("Pause", InputActionType.Button);
            _pauseAction.AddBinding("<Keyboard>/escape");
        }

        // Reemplaza al antiguo Update(): lo tickea el CustomUpdateManager.
        // deltaTime no se usa (la lectura de input no depende del dt).
        public void Tick(float deltaTime)
        {
            // Guard defensivo: si aún no se llamó Initialize (o ya se hizo Dispose),
            // no leer para evitar excepciones.
            if (_gameplayActions == null)
                return;

            // Pausa primero: alterna pausa/reanudar con Escape. InputReader sigue
            // tickeando durante la pausa (registrado como always-tickable), por eso
            // este chequeo funciona aun con el gameplay detenido.
            ReadPauseInput();

            ReadMovementInput();
            ReadLookInput();
            ReadFireInput();
        }

        private void ReadPauseInput()
        {
            if (_pauseAction.WasPressedThisFrame())
            {
                _gameManager.TogglePause();
            }
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
