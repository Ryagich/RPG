using Dialogue;
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
        private readonly IPublisher<InventoryInputMessage> inventoryInputPublisher;
        private readonly IPublisher<MapInputMessage> mapInputPublisher;
        private readonly IPublisher<QuestLogInputMessage> questLogInputPublisher;
        private readonly IPublisher<MouseDown> mouseDown;
        private readonly IPublisher<MouseUp> mouseUp;
        private readonly IPublisher<DodgeInputMessage> dodgeInputPublisher;
        private readonly IPublisher<RollInputMessage> rollInputPublisher;
        private readonly IPublisher<LessonSkipInputMessage> lessonSkipInputPublisher;
        private readonly IPublisher<LessonEvasionInputMessage> lessonEvasionInputPublisher;
        private readonly IPublisher<LessonAttackInputMessage> lessonAttackInputPublisher;
        private readonly IPublisher<PauseInputMessage> pauseInputPublisher;
        private readonly IPublisher<TargetLockInputMessage> targetLockInputPublisher;
        private readonly IPublisher<ShowStatsInputMessage> showStatsInputPublisher;
        private readonly IPublisher<FastSlotInputMessage> fastSlotInputPublisher;
        private readonly IPublisher<WeaponSlotInputMessage> weaponSlotInputPublisher;
        private readonly GameModesController gameModesController;
        private readonly DialogueContext dialogueContext;
        private readonly ISubscriber<PlayerDiedMessage> playerDiedSubscriber;

        private Vector2 currentMoveDirection;
        private bool isRunPressed;
        private bool isPlayerDead;

        private InputHandler
            (
                InputConfig inputConfig,
                IPublisher<PlayerMoveMessage> playerMovePublisher,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
                IPublisher<InteractableInputMessage> interactableInputPublisher,
                IPublisher<InventoryInputMessage> inventoryInputPublisher,
                IPublisher<MapInputMessage> mapInputPublisher,
                IPublisher<QuestLogInputMessage> questLogInputPublisher,
                IPublisher<MouseDown> mouseDown,
                IPublisher<MouseUp> mouseUp,
                IPublisher<DodgeInputMessage> dodgeInputPublisher,
                IPublisher<RollInputMessage> rollInputPublisher,
                IPublisher<LessonSkipInputMessage> lessonSkipInputPublisher,
                IPublisher<LessonEvasionInputMessage> lessonEvasionInputPublisher,
                IPublisher<LessonAttackInputMessage> lessonAttackInputPublisher,
                IPublisher<PauseInputMessage> pauseInputPublisher,
                IPublisher<TargetLockInputMessage> targetLockInputPublisher,
                IPublisher<ShowStatsInputMessage> showStatsInputPublisher,
                IPublisher<FastSlotInputMessage> fastSlotInputPublisher,
                IPublisher<WeaponSlotInputMessage> weaponSlotInputPublisher,
                GameModesController gameModesController,
                DialogueContext dialogueContext,
                ISubscriber<PlayerDiedMessage> playerDiedSubscriber
            )
        {
            this.inputConfig = inputConfig;
            this.playerMovePublisher = playerMovePublisher;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.interactableInputPublisher = interactableInputPublisher;
            this.inventoryInputPublisher = inventoryInputPublisher;
            this.mapInputPublisher = mapInputPublisher;
            this.questLogInputPublisher = questLogInputPublisher;
            this.mouseDown = mouseDown;
            this.mouseUp = mouseUp;
            this.dodgeInputPublisher = dodgeInputPublisher;
            this.rollInputPublisher = rollInputPublisher;
            this.lessonSkipInputPublisher = lessonSkipInputPublisher;
            this.lessonEvasionInputPublisher = lessonEvasionInputPublisher;
            this.lessonAttackInputPublisher = lessonAttackInputPublisher;
            this.pauseInputPublisher = pauseInputPublisher;
            this.targetLockInputPublisher = targetLockInputPublisher;
            this.showStatsInputPublisher = showStatsInputPublisher;
            this.fastSlotInputPublisher = fastSlotInputPublisher;
            this.weaponSlotInputPublisher = weaponSlotInputPublisher;
            this.gameModesController = gameModesController;
            this.dialogueContext = dialogueContext;
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
            inputConfig.Map.action.started += OpenMap;
            inputConfig.QuestLog.action.started += OpenQuestLog;
            inputConfig.LeftClick.action.started += LeftMouseDown;
            inputConfig.LeftClick.action.canceled += LeftMouseUp;
            inputConfig.RightClick.action.started += RightMouseDown;
            inputConfig.RightClick.action.canceled += RightMouseUp;
            inputConfig.Dodge.action.started += Dodge;
            inputConfig.Roll.action.started += Roll;
            SubscribeLessonSkipAction();
            inputConfig.FastSlot1.action.started += _ => PublishFastSlot(1);
            inputConfig.FastSlot2.action.started += _ => PublishFastSlot(2);
            inputConfig.FastSlot3.action.started += _ => PublishFastSlot(3);
            inputConfig.FastSlot4.action.started += _ => PublishFastSlot(4);
            SubscribeWeaponSlot(inputConfig.WeaponSlot1, 1);
            SubscribeWeaponSlot(inputConfig.WeaponSlot2, 2);
            inputConfig.Pause.action.started += Pause;
            inputConfig.TargetLock.action.started += _ => PublishTargetLockCommand(TargetLockCommand.Toggle);
            inputConfig.TargetLockNext.action.started += _ => PublishTargetLockCommand(TargetLockCommand.Next);
            inputConfig.TargetLockPrevious.action.started += _ => PublishTargetLockCommand(TargetLockCommand.Previous);
            inputConfig.ShowStats.action.started += ShowStatsPressed;
            inputConfig.ShowStats.action.canceled += ShowStatsReleased;
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
                    if (!dialogueContext.CanExitDialogue)
                    {
                        return;
                    }

                    changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                    return;
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

            inventoryInputPublisher.Publish(new InventoryInputMessage());
        }

        private void OpenMap(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            mapInputPublisher.Publish(new MapInputMessage());
        }

        private void OpenQuestLog(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            questLogInputPublisher.Publish(new QuestLogInputMessage());
        }

        private void LeftMouseDown(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            if (gameModesController.GameMode == GameMode.Lesson)
            {
                lessonAttackInputPublisher.Publish(new LessonAttackInputMessage(MouseButtonType.Left));
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

            if (gameModesController.GameMode == GameMode.Lesson)
            {
                lessonAttackInputPublisher.Publish(new LessonAttackInputMessage(MouseButtonType.Right));
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

        private void Dodge(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            if (gameModesController.GameMode == GameMode.Lesson)
            {
                lessonEvasionInputPublisher.Publish(new LessonEvasionInputMessage(LessonEvasionAction.Dodge));
                return;
            }

            dodgeInputPublisher.Publish(new DodgeInputMessage());
        }

        private void Roll(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            if (gameModesController.GameMode == GameMode.Lesson)
            {
                lessonEvasionInputPublisher.Publish(new LessonEvasionInputMessage(LessonEvasionAction.Roll));
                return;
            }

            rollInputPublisher.Publish(new RollInputMessage());
        }

        private void SkipLesson(InputAction.CallbackContext context)
        {
            if (!isPlayerDead && gameModesController.GameMode == GameMode.Lesson)
            {
                lessonSkipInputPublisher.Publish(new LessonSkipInputMessage());
            }
        }

        private void SubscribeLessonSkipAction()
        {
            var lessonSkipAction = inputConfig.LessonSkip?.action;

            if (lessonSkipAction == null)
            {
                Debug.LogError("LessonSkip input action is not configured.");
                return;
            }

            lessonSkipAction.started += SkipLesson;
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

        private void PublishFastSlot(int slotIndex)
        {
            if (!isPlayerDead)
            {
                fastSlotInputPublisher.Publish(new FastSlotInputMessage(slotIndex));
            }
        }

        private void PublishWeaponSlot(int slotIndex)
        {
            if (!isPlayerDead)
            {
                weaponSlotInputPublisher.Publish(new WeaponSlotInputMessage(slotIndex));
            }
        }

        private void SubscribeWeaponSlot(InputActionReference actionReference, int slotIndex)
        {
            var action = actionReference?.action;
            if (action == null)
            {
                Debug.LogError($"Input action WeaponSlot{slotIndex} is not configured.");
                return;
            }

            action.started += _ => PublishWeaponSlot(slotIndex);
        }

        private void Pause(InputAction.CallbackContext context)
        {
            if (isPlayerDead)
            {
                return;
            }

            if (gameModesController.GameMode == GameMode.Lesson)
            {
                lessonSkipInputPublisher.Publish(new LessonSkipInputMessage());
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
