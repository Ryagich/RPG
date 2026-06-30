using GameModes;
using MessagePipe;
using Messages;
using UniRx;
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
        private readonly IPublisher<PauseInputMessage> pauseInputPublisher;
        private readonly IPublisher<TargetLockInputMessage> targetLockInputPublisher;
        private readonly IPublisher<ShowStatsInputMessage> showStatsInputPublisher;
        private readonly IPublisher<FastSlotInputMessage> fastSlotInputPublisher;
        private readonly IPublisher<WeaponSlotInputMessage> weaponSlotInputPublisher;
        private readonly GameModesController gameModesController;
        private readonly ISubscriber<PlayerDiedMessage> playerDiedSubscriber;

        private Vector2 currentMoveDirection;
        private bool isRunPressed;
        private bool isPlayerDead;
        private bool pollTargetLockToggleFallback;
        private bool pollTargetLockNextFallback;
        private bool pollTargetLockPreviousFallback;

        private InputHandler
            (
                InputConfig inputConfig,
                IPublisher<PlayerMoveMessage> playerMovePublisher,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
                IPublisher<InteractableInputMessage> interactableInputPublisher,
                IPublisher<MouseDown> mouseDown,
                IPublisher<MouseUp> mouseUp,
                IPublisher<PauseInputMessage> pauseInputPublisher,
                IPublisher<TargetLockInputMessage> targetLockInputPublisher,
                IPublisher<ShowStatsInputMessage> showStatsInputPublisher,
                IPublisher<FastSlotInputMessage> fastSlotInputPublisher,
                IPublisher<WeaponSlotInputMessage> weaponSlotInputPublisher,
                GameModesController gameModesController,
                ISubscriber<PlayerDiedMessage> playerDiedSubscriber
            )
        {
            this.inputConfig = inputConfig;
            this.playerMovePublisher = playerMovePublisher;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.interactableInputPublisher = interactableInputPublisher;
            this.mouseDown = mouseDown;
            this.mouseUp = mouseUp;
            this.pauseInputPublisher = pauseInputPublisher;
            this.targetLockInputPublisher = targetLockInputPublisher;
            this.showStatsInputPublisher = showStatsInputPublisher;
            this.fastSlotInputPublisher = fastSlotInputPublisher;
            this.weaponSlotInputPublisher = weaponSlotInputPublisher;
            this.gameModesController = gameModesController;
            this.playerDiedSubscriber = playerDiedSubscriber;
        }

        public void Start()
        {
            inputConfig.Movement.action.performed += OnMove;
            inputConfig.Movement.action.canceled += OnMove;
            inputConfig.Run.action.performed += OnRun;
            inputConfig.Run.action.canceled += OnRun;
            inputConfig.Interactable.action.started += Interactable;
            inputConfig.Inventory.action.started += OpenInventory;
            SubscribeMapAction("Map");
            inputConfig.LeftClick.action.started += LeftMouseDown;
            inputConfig.LeftClick.action.canceled += LeftMouseUp;
            inputConfig.RightClick.action.started += RightMouseDown;
            inputConfig.RightClick.action.canceled += RightMouseUp;
            SubscribeFastSlotAction("FastSlot1", 1);
            SubscribeFastSlotAction("FastSlot2", 2);
            SubscribeFastSlotAction("FastSlot3", 3);
            SubscribeFastSlotAction("FastSlot4", 4);
            SubscribePauseAction("Pause");
            SubscribeTargetLockAction("TargetLock", TargetLockCommand.Toggle, ref pollTargetLockToggleFallback);
            SubscribeTargetLockAction("TargetLockNext", TargetLockCommand.Next, ref pollTargetLockNextFallback);
            SubscribeTargetLockAction("TargetLockPrevious", TargetLockCommand.Previous, ref pollTargetLockPreviousFallback);

            if (inputConfig.ShowStats != null && inputConfig.ShowStats.action != null)
            {
                inputConfig.ShowStats.action.started += ShowStatsPressed;
                inputConfig.ShowStats.action.canceled += ShowStatsReleased;
            }
            else
            {
                Observable.EveryUpdate().Subscribe(_ => PollShowStatsFallback());
            }

            Observable.EveryUpdate().Subscribe(_ => PollWeaponSlotInputs());
            Observable.EveryUpdate().Subscribe(_ => PollTargetLockFallbackInputs());
            playerDiedSubscriber.Subscribe(OnPlayerDied);
        }
        
        private void OnMove(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            currentMoveDirection = context.ReadValue<Vector2>();
            PublishPlayerMove();
        }

        private void OnRun(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            isRunPressed = context.ReadValueAsButton();
            PublishPlayerMove();
        }

        private void PublishPlayerMove()
        {
            playerMovePublisher.Publish(new PlayerMoveMessage(currentMoveDirection, isRunPressed));
        }
        
        private void Interactable(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            switch (gameModesController.GameMode)
            {
                case GameMode.Game:
                    interactableInputPublisher.Publish(new());
                    return;
                case GameMode.Dialogue:
                case GameMode.Looting:
                    changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                    return;
                case GameMode.Trade:
                    changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
                    return;
                default:
                    return;
            }
        }
        
        private void OpenInventory(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            if (gameModesController.GameMode == GameMode.Trade)
            {
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Inventory));
                return;
            }

            if (gameModesController.GameMode == GameMode.Map)
            {
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Inventory));
                return;
            }

            if (gameModesController.GameMode == GameMode.Inventory)
            {
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                return;
            }

            if (gameModesController.GameMode == GameMode.Game || gameModesController.GameMode == GameMode.Dialogue)
            {
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Inventory));
            }
        }

        private void OpenMap(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            switch (gameModesController.GameMode)
            {
                case GameMode.Game:
                case GameMode.Inventory:
                    changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Map));
                    break;
                case GameMode.Map:
                    changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                    break;
            }
        }

        private void LeftMouseDown(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            mouseDown.Publish(new(MouseButtonType.Left));
        }

        private void LeftMouseUp(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            mouseUp.Publish(new(MouseButtonType.Left));
        }

        private void RightMouseDown(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            mouseDown.Publish(new(MouseButtonType.Right));
        }

        private void RightMouseUp(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            mouseUp.Publish(new(MouseButtonType.Right));
        }

        private void ShowStatsPressed(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            showStatsInputPublisher.Publish(new ShowStatsInputMessage(true));
        }

        private void ShowStatsReleased(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            showStatsInputPublisher.Publish(new ShowStatsInputMessage(false));
        }

        private void SubscribeFastSlotAction(string actionName, int slotIndex)
        {
            var actionMap = inputConfig.Movement?.action?.actionMap;
            var action = actionMap?.FindAction(actionName, false);
            if (action == null)
            {
                return;
            }

            action.started += _ =>
            {
                if (!isPlayerDead)
                {
                    fastSlotInputPublisher.Publish(new FastSlotInputMessage(slotIndex));
                }
            };
        }

        private void SubscribeMapAction(string actionName)
        {
            var actionMap = inputConfig.Movement?.action?.actionMap;
            var action = actionMap?.FindAction(actionName, false);
            if (action == null)
            {
                return;
            }

            action.started += OpenMap;
        }

        private void SubscribePauseAction(string actionName)
        {
            var actionMap = inputConfig.Movement?.action?.actionMap;
            var action = actionMap?.FindAction(actionName, false);
            if (action == null)
            {
                return;
            }

            action.started += Pause;
        }

        private void SubscribeTargetLockAction(
            string actionName,
            TargetLockCommand command,
            ref bool useFallback)
        {
            var actionMap = inputConfig.Movement?.action?.actionMap;
            var action = actionMap?.FindAction(actionName, false);
            if (action == null)
            {
                useFallback = true;
                return;
            }

            action.started += _ => PublishTargetLockCommand(command);
        }

        private void Pause(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            pauseInputPublisher.Publish(new PauseInputMessage());
        }

        private void PublishTargetLockCommand(TargetLockCommand command)
        {
            if (isPlayerDead || gameModesController.GameMode != GameMode.Game)
            {
                return;
            }

            targetLockInputPublisher.Publish(new TargetLockInputMessage(command));
        }

        private void PollShowStatsFallback()
        {
            if (isPlayerDead)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                showStatsInputPublisher.Publish(new ShowStatsInputMessage(true));
            }

            if (keyboard.tabKey.wasReleasedThisFrame)
            {
                showStatsInputPublisher.Publish(new ShowStatsInputMessage(false));
            }
        }

        private void PollWeaponSlotInputs()
        {
            if (isPlayerDead)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                weaponSlotInputPublisher.Publish(new WeaponSlotInputMessage(1));
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                weaponSlotInputPublisher.Publish(new WeaponSlotInputMessage(2));
            }
        }

        private void PollTargetLockFallbackInputs()
        {
            if (isPlayerDead)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (pollTargetLockToggleFallback && keyboard.qKey.wasPressedThisFrame)
            {
                PublishTargetLockCommand(TargetLockCommand.Toggle);
            }

            if (pollTargetLockNextFallback && keyboard.eKey.wasPressedThisFrame)
            {
                PublishTargetLockCommand(TargetLockCommand.Next);
            }

            if (pollTargetLockPreviousFallback && keyboard.zKey.wasPressedThisFrame)
            {
                PublishTargetLockCommand(TargetLockCommand.Previous);
            }
        }

        private void OnPlayerDied(PlayerDiedMessage _)
        {
            isPlayerDead = true;
            currentMoveDirection = Vector2.zero;
            isRunPressed = false;
            PublishPlayerMove();
            showStatsInputPublisher.Publish(new ShowStatsInputMessage(false));
        }
    }
}
