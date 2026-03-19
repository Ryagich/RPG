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

        private InputHandler
            (
                InputConfig inputConfig,
                IPublisher<PlayerMoveMessage> playerMovePublisher,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
                IPublisher<InteractableInputMessage> interactableInputPublisher
            )
        {
            this.inputConfig = inputConfig;
            this.playerMovePublisher = playerMovePublisher;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.interactableInputPublisher = interactableInputPublisher;
        }

        public void Start()
        {
            inputConfig.Movement.action.performed += OnMove;
            inputConfig.Movement.action.canceled += OnMove;
            inputConfig.Interactable.action.started += Interactable;
            inputConfig.Inventory.action.started += OpenInventory;
        }
        
        private void OnMove(InputAction.CallbackContext context)
        {
            var dir = context.ReadValue<Vector2>();
            playerMovePublisher.Publish(new PlayerMoveMessage(dir));
        }
        
        private void Interactable(InputAction.CallbackContext context)
        {
            interactableInputPublisher.Publish(new InteractableInputMessage());
        }
        
        private void OpenInventory(InputAction.CallbackContext context)
        {   
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameModes.GameMode.Inventory));
        }
    }
}