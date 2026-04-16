using GameModes;
using MessagePipe;
using Messages;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Input
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class InputHandler : IStartable
    {
        private readonly InputConfig inputConfig;
        private readonly IPublisher<PlayerMoveMessage> playerMovePublisher;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly IPublisher<InteractableInputMessage> interactableInputPublisher;
        private readonly IPublisher<MouseDown> mouseDown;
        private readonly IPublisher<MouseUp> mouseUp;
        private readonly GameModesController gameModesController;

        private Vector2 currentMoveDirection;
        private bool isRunPressed;

        private InputHandler
            (
                InputConfig inputConfig,
                IPublisher<PlayerMoveMessage> playerMovePublisher,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
                IPublisher<InteractableInputMessage> interactableInputPublisher,
                IPublisher<MouseDown> mouseDown,
                IPublisher<MouseUp> mouseUp,
                GameModesController gameModesController
            )
        {
            this.inputConfig = inputConfig;
            this.playerMovePublisher = playerMovePublisher;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.interactableInputPublisher = interactableInputPublisher;
            this.mouseDown = mouseDown;
            this.mouseUp = mouseUp;
            this.gameModesController = gameModesController;
        }

        public void Start()
        {
            inputConfig.Movement.action.performed += OnMove;
            inputConfig.Movement.action.canceled += OnMove;
            inputConfig.Run.action.performed += OnRun;
            inputConfig.Run.action.canceled += OnRun;
            inputConfig.Interactable.action.started += Interactable;
            inputConfig.Inventory.action.started += OpenInventory;
            inputConfig.LeftClick.action.started += LeftMouseDown;
            inputConfig.LeftClick.action.canceled += LeftMouseUp;
            inputConfig.RightClick.action.started += RightMouseDown;
            inputConfig.RightClick.action.canceled += RightMouseUp;
        }
        
        private void OnMove(InputAction.CallbackContext context)
        {
            currentMoveDirection = context.ReadValue<Vector2>();
            PublishPlayerMove();
        }

        private void OnRun(InputAction.CallbackContext context)
        {
            isRunPressed = context.ReadValueAsButton();
            PublishPlayerMove();
        }

        private void PublishPlayerMove()
        {
            playerMovePublisher.Publish(new PlayerMoveMessage(currentMoveDirection, isRunPressed));
        }
        
        private void Interactable(InputAction.CallbackContext context)
        {
            interactableInputPublisher.Publish(new());
        }
        
        private void OpenInventory(InputAction.CallbackContext context)
        {
            if (gameModesController.GameMode == GameMode.Trade)
            {
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
                return;
            }

            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Inventory));
        }

        private void LeftMouseDown(InputAction.CallbackContext context)
        {
            mouseDown.Publish(new(MouseButtonType.Left));
        }

        private void LeftMouseUp(InputAction.CallbackContext context)
        {
            mouseUp.Publish(new(MouseButtonType.Left));
        }

        private void RightMouseDown(InputAction.CallbackContext context)
        {
            mouseDown.Publish(new(MouseButtonType.Right));
        }

        private void RightMouseUp(InputAction.CallbackContext context)
        {
            mouseUp.Publish(new(MouseButtonType.Right));
        }
    }
}
