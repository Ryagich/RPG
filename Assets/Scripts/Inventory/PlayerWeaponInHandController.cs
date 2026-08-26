using System;
using Combat;
using GameModes;
using Inventory.Inventories;
using Inventory.Item;
using MessagePipe;
using Messages;
using Movement;
using TargetLock;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using Stats;

namespace Inventory
{
    public sealed class PlayerWeaponInHandController : IWeaponAnimationEventHandler, IEquippedWeaponVisual, IStartable, ITickable, IDisposable
    {
        private const string BeginMoveWeaponToRightHandEventName = "BeginMoveWeaponToRightHand";
        private const string TakeWeaponInHandEventName = "TakeWeaponInHand";
        private const string BeginMoveWeaponToBeltEventName = "BeginMoveWeaponToBelt";
        private const string PutWeaponOnBeltEventName = "PutWeaponOnBelt";
        private readonly GameModesController gameModesController;
        private readonly PlayerInventory playerInventory;
        private readonly PlayerWeaponAnimationEventReceiver animationEventReceiver;
        private readonly Animator animator;
        private readonly PlayerMovement playerMovement;
        private readonly CharacterDamageReceiver ownerDamageReceiver;
        private readonly CharacterActionState actionState;
        private readonly IPublisher<WeaponSheathedMessage> weaponSheathedPublisher;
        private readonly CompositeDisposable disposables = new();
        private readonly PlayerWeaponInputSubscriptions inputSubscriptions;
        private readonly PlayerWeaponVisualController weaponVisual;
        private readonly PlayerWeaponTransitionAnimator weaponTransitionAnimator;
        private readonly PlayerWeaponCombatActionController combatActions;
        private readonly int weaponAnimationLayerIndex;

        private int selectedWeaponSlotIndex = 1;
        private bool isInitialized;
        private bool hasPendingRefresh;
        private readonly PlayerWeaponTransitionState weaponTransitionState = new();

        private bool isWeaponDrawn
        {
            get => weaponTransitionState.IsWeaponDrawn;
            set => weaponTransitionState.SetWeaponDrawn(value);
        }

        private bool isWeaponAnimationInProgress => weaponTransitionState.IsAnimationInProgress;
        private bool shouldPreservePoseForCurrentDraw => weaponTransitionState.ShouldPreservePoseForDraw;
        private WeaponAnimationKind currentAnimationKind => weaponTransitionState.CurrentKind;
        private GameObject currentWeaponInstance => weaponVisual.Instance;
        private ItemConfig currentWeaponItemConfig => weaponVisual.ItemConfig;
        private int currentRenderedSlotIndex => weaponVisual.SlotIndex;
        private WeaponDisplayMode currentDisplayMode => weaponVisual.DisplayMode;

        public PlayerWeaponInHandController(
            PlayerInventory playerInventory,
            PlayerWeaponHandAnchor handAnchor,
            PlayerWeaponAnimationEventReceiver animationEventReceiver,
            Animator animator,
            CharacterRootMotionController rootMotionController,
            GameModesController gameModesController,
            PlayerMovement playerMovement,
            PlayerMovementConfig playerMovementConfig,
            PlayerAnimationController playerAnimationController,
            TargetLockController targetLockController,
            CharacterDamageReceiver ownerDamageReceiver,
            CharacterActionState actionState,
            StatsController statsController,
            IPublisher<PlayerEvasionCompletedMessage> evasionCompletedPublisher,
            IPublisher<WeaponSheathedMessage> weaponSheathedPublisher,
            ISubscriber<WeaponSlotInputMessage> weaponSlotInputSubscriber,
            ISubscriber<MouseDown> mouseDownSubscriber,
            ISubscriber<DodgeInputMessage> dodgeInputSubscriber,
            ISubscriber<RollInputMessage> rollInputSubscriber,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.gameModesController = gameModesController;
            this.playerInventory = playerInventory;
            this.animationEventReceiver = animationEventReceiver;
            this.animator = animator;
            this.playerMovement = playerMovement;
            this.ownerDamageReceiver = ownerDamageReceiver;
            this.actionState = actionState;
            this.weaponSheathedPublisher = weaponSheathedPublisher;
            weaponTransitionAnimator = new PlayerWeaponTransitionAnimator(animator);
            var weaponCombatAnimator = new PlayerWeaponCombatAnimator(animator);
            var damageWindow = new EquippedWeaponDamageWindowController();
            weaponAnimationLayerIndex = weaponTransitionAnimator.LayerIndex;
            weaponVisual = new PlayerWeaponVisualController(handAnchor, animator, weaponAnimationLayerIndex);
            combatActions = new PlayerWeaponCombatActionController(
                animator,
                weaponCombatAnimator,
                rootMotionController,
                gameModesController,
                playerMovement,
                playerMovementConfig,
                playerAnimationController,
                targetLockController,
                ownerDamageReceiver,
                actionState,
                statsController,
                evasionCompletedPublisher,
                damageWindow,
                () => currentWeaponInstance,
                () => currentWeaponItemConfig,
                RequestWeaponTransitionReversal);
            animationEventReceiver?.Bind(this);

            inputSubscriptions = new PlayerWeaponInputSubscriptions(
                weaponSlotInputSubscriber,
                mouseDownSubscriber,
                dodgeInputSubscriber,
                rollInputSubscriber,
                gameModeChangedSubscriber,
                OnWeaponSlotInput,
                OnMouseDown,
                OnDodgeInput,
                OnRollInput,
                OnGameModeChanged);
            playerInventory.Changed.Subscribe(_ => RefreshWeaponAfterInitialization()).AddTo(disposables);
            playerInventory.HandSlot.Subscribe(_ => RefreshWeaponAfterInitialization()).AddTo(disposables);
        }

        public void Start()
        {
            ResetAnimatorRequests();
            combatActions.Start();

            // The initial item set is applied before this entry point starts. Its inventory
            // notifications must establish the initial belt presentation, not begin a draw
            // transition before the player has requested one.
            isInitialized = true;
            RefreshWeaponInHand();
            UpdateRunningAvailability();
        }

        public void Dispose()
        {
            combatActions.Dispose();
            weaponVisual.Dispose();
            inputSubscriptions.Dispose();
            disposables.Dispose();
        }

        public void Tick()
        {
            combatActions.Tick();
            SynchronizeSheatheCompletionWithAnimatorState();

            if (hasPendingRefresh
             && !isWeaponAnimationInProgress
             && !actionState.IsActionBlocked
             && !combatActions.IsAttackBlockingWeaponChanges)
            {
                hasPendingRefresh = false;
                RefreshWeaponInHand();
            }
        }

        public void BeginMoveWeaponToRightHandFromAnimationEvent()
        {
            if (!weaponTransitionAnimator.IsStateExpectedForAnimationEvent(WeaponAnimationKind.Draw))
            {
                return;
            }

            SynchronizeWeaponTransitionWithAnimationEvent(
                WeaponAnimationKind.Draw,
                preservePoseForDraw: weaponVisual.IsAttachedTo(WeaponDisplayMode.RightHand));

            if (!weaponTransitionState.TryBeginAttachmentBlend(WeaponAnimationKind.Draw))
            {
                return;
            }

            if (shouldPreservePoseForCurrentDraw)
            {
                MoveCurrentWeaponToRightHandPreservingPose();
                CleanupSpawnedWeaponInstancesExceptCurrent();
                return;
            }

            StartWeaponAttachmentBlend(
                WeaponDisplayMode.RightHand,
                BeginMoveWeaponToRightHandEventName,
                TakeWeaponInHandEventName);
            CleanupSpawnedWeaponInstancesExceptCurrent();
        }

        public void TakeWeaponInHandFromAnimationEvent()
        {
            if (!weaponTransitionAnimator.IsStateExpectedForAnimationEvent(WeaponAnimationKind.Draw)
                || currentWeaponItemConfig == null)
            {
                return;
            }

            SynchronizeWeaponTransitionWithAnimationEvent(WeaponAnimationKind.Draw);

            FinalizeWeaponRender(
                currentWeaponItemConfig,
                currentRenderedSlotIndex,
                WeaponDisplayMode.RightHand,
                snapToAttachmentTransform: true);
            CleanupSpawnedWeaponInstancesExceptCurrent();
            isWeaponDrawn = true;
            CompleteWeaponAnimationFromEvent(WeaponAnimationKind.Draw);
        }

        public void BeginMoveWeaponToBeltFromAnimationEvent()
        {
            if (!weaponTransitionAnimator.IsStateExpectedForAnimationEvent(WeaponAnimationKind.Sheathe))
            {
                return;
            }

            SynchronizeWeaponTransitionWithAnimationEvent(WeaponAnimationKind.Sheathe);

            if (!weaponTransitionState.TryBeginAttachmentBlend(WeaponAnimationKind.Sheathe))
            {
                return;
            }

            if (TryStartNextWeaponDuringSheathe())
            {
                return;
            }

            MoveCurrentWeaponToBeltPreservingPose();
        }

        public void PutWeaponOnBeltFromAnimationEvent()
        {
            if (!weaponTransitionAnimator.IsStateExpectedForAnimationEvent(WeaponAnimationKind.Sheathe)
                || currentWeaponItemConfig == null)
            {
                return;
            }

            SynchronizeWeaponTransitionWithAnimationEvent(WeaponAnimationKind.Sheathe);

            FinalizeWeaponRender(
                currentWeaponItemConfig,
                currentRenderedSlotIndex,
                WeaponDisplayMode.Belt,
                snapToAttachmentTransform: false);
            CleanupSpawnedWeaponInstancesExceptCurrent();

            var selectedItemConfig = GetSelectedWeaponItemConfig();
            if (selectedItemConfig == null)
            {
                RenderWeapon(null, 0, WeaponDisplayMode.None);
                CompleteWeaponAnimationFromEvent(WeaponAnimationKind.Sheathe);
                PublishWeaponSheathed();
                return;
            }

            if (currentRenderedSlotIndex != selectedWeaponSlotIndex
             || currentWeaponItemConfig != selectedItemConfig)
            {
                RenderWeapon(selectedItemConfig, selectedWeaponSlotIndex, WeaponDisplayMode.Belt);
            }

            CompleteWeaponAnimationFromEvent(WeaponAnimationKind.Sheathe);
            PublishWeaponSheathed();
        }

        public void HoldAttackReadyFromAnimationEvent()
        {
        }

        public void AttackStartedFromAnimationEvent()
        {
            combatActions.AttackStartedFromAnimationEvent();
        }

        public void BeginDamageWindowFromAnimationEvent()
        {
            combatActions.BeginDamageWindowFromAnimationEvent();
        }

        public void EndDamageWindowFromAnimationEvent()
        {
            combatActions.EndDamageWindowFromAnimationEvent();
        }

        public bool IsWeaponSheathed => !isWeaponAnimationInProgress
                                       && currentDisplayMode != WeaponDisplayMode.RightHand;

        public bool IsWeaponDrawn => isWeaponDrawn;

        public bool CanProcessWeaponSlotInput => isInitialized
                                                  && !actionState.IsActionBlocked
                                                  && !combatActions.IsAttackBlockingWeaponChanges;

        private bool IsWeaponInHand => isWeaponDrawn
                                      && currentDisplayMode == WeaponDisplayMode.RightHand
                                      && currentWeaponItemConfig != null;

        public int ActiveWeaponSlotIndex => selectedWeaponSlotIndex;

        /// <summary>
        /// Indicates that the ordinary sheathing animation can begin without replacing an
        /// active full-body action or another weapon transition.
        /// </summary>
        public bool CanStartWeaponSheathing => IsWeaponSheathed
                                              || (!isWeaponAnimationInProgress && !combatActions.IsAttackRootMotionStateActive);

        /// <summary>
        /// True from the animation's LockMovement event until its matching UnlockMovement event.
        /// </summary>
        public bool IsCombatActionLocked => combatActions.IsCombatActionLocked;

        /// <summary>
        /// True while the Roll request, transition, or its full-body animation is active.
        /// Consumers that synchronize with a roll must wait for this to become false rather
        /// than relying on the earlier UnlockMovement animation event.
        /// </summary>
        public bool IsRollAnimationActive => combatActions.IsRollAnimationActive;

        /// <summary>
        /// Starts the normal sheathing transition. An explicit sheathe command takes precedence
        /// over an interrupted attack so the weapon cannot remain in hand when external gameplay
        /// control has already been removed.
        /// </summary>
        public void RequestSheatheWeapon()
        {
            if (!isWeaponDrawn && currentDisplayMode != WeaponDisplayMode.RightHand)
            {
                return;
            }

            combatActions.Cancel(restoreMovement: false);
            isWeaponDrawn = false;
            StartSheatheAnimation(currentRenderedSlotIndex, currentWeaponItemConfig, ignoreActiveCombatAction: true);
        }

        public void EnableDamageImmunityFromAnimationEvent()
        {
            combatActions.EnableDamageImmunityFromAnimationEvent();
        }

        public void DisableDamageImmunityFromAnimationEvent()
        {
            combatActions.DisableDamageImmunityFromAnimationEvent();
        }

        public void LockMovementFromAnimationEvent()
        {
            combatActions.LockMovementFromAnimationEvent();
        }

        public void UnlockMovementFromAnimationEvent()
        {
            combatActions.UnlockMovementFromAnimationEvent();
        }

        public void AttackFinishedFromAnimationEvent()
        {
            combatActions.AttackFinishedFromAnimationEvent();
            UpdateRunningAvailability();
            RefreshWeaponInHand();
        }

        public void ResetAttackRequestFromAnimationEvent()
        {
            // Legacy event endpoint: ResetAttackRequest.
            // Its meaning is now "reset animation requests": both mutually exclusive
            // attack request bools are cleared together.
            combatActions.ResetAnimationRequests();
        }

        public void InterruptByHitReaction()
        {
            combatActions.InterruptByHitReaction();
            ResetAnimatorRequests();
            RequestWeaponTransitionReversal();
        }

        public bool TryGetCurrentWeaponSlot(out Inventory.Slot.SlotModel slot)
        {
            slot = currentRenderedSlotIndex switch
            {
                1 => playerInventory.LeftWeaponSlot,
                2 => playerInventory.RightWeaponSlot,
                _ => null
            };

            if (slot?.ItemConfig?.ItemType == ItemType.Weapon)
            {
                return true;
            }

            var selectedSlot = selectedWeaponSlotIndex == 1
                ? playerInventory.LeftWeaponSlot
                : playerInventory.RightWeaponSlot;

            if (selectedSlot?.ItemConfig?.ItemType == ItemType.Weapon)
            {
                slot = selectedSlot;
                return true;
            }

            if (playerInventory.LeftWeaponSlot?.ItemConfig?.ItemType == ItemType.Weapon)
            {
                slot = playerInventory.LeftWeaponSlot;
                return true;
            }

            if (playerInventory.RightWeaponSlot?.ItemConfig?.ItemType == ItemType.Weapon)
            {
                slot = playerInventory.RightWeaponSlot;
                return true;
            }

            slot = null;
            return false;
        }

        public bool TryGetCurrentWeaponPose(out Vector3 position, out Quaternion rotation)
        {
            return weaponVisual.TryGetPose(out position, out rotation);
        }

        private void OnWeaponSlotInput(WeaponSlotInputMessage message)
        {
            LogWeaponSlotInput(message.SlotIndex, "Received weapon-slot input");

            if (gameModesController.GameMode == GameMode.Dialogue)
            {
                // Modal pages must never begin a weapon draw. A player who entered the page
                // with a weapon in hand can still use either weapon-slot command to stow it.
                if (isWeaponDrawn || currentDisplayMode == WeaponDisplayMode.RightHand)
                {
                    RequestSheatheWeapon();
                }

                return;
            }

            if (!CanProcessWeaponSlotInput)
            {
                LogWeaponSlotInput(message.SlotIndex, "Ignored weapon-slot input because weapon changes are blocked");
                return;
            }

            if (message.SlotIndex is < 1 or > 2)
            {
                LogWeaponSlotInput(message.SlotIndex, "Ignored weapon-slot input because the slot is invalid");
                return;
            }

            if (selectedWeaponSlotIndex == message.SlotIndex)
            {
                var selectedItemConfig = GetSelectedWeaponItemConfig();
                if (selectedItemConfig == null)
                {
                    LogWeaponSlotInput(message.SlotIndex, "Active slot has no weapon; refreshing display");
                    RefreshWeaponInHand();
                    return;
                }

                isWeaponDrawn = !isWeaponDrawn;
                LogWeaponSlotInput(message.SlotIndex, "Toggled active weapon slot");
                if (TryHandleWeaponSlotInputDuringAnimation())
                {
                    LogWeaponSlotInput(message.SlotIndex, "Queued active-slot change during weapon animation");
                    return;
                }

                RefreshWeaponInHand();
                LogWeaponSlotInput(message.SlotIndex, "Applied active-slot change");
                return;
            }

            selectedWeaponSlotIndex = message.SlotIndex;
            isWeaponDrawn = true;
            LogWeaponSlotInput(message.SlotIndex, "Selected a different weapon slot");

            if (TryHandleWeaponSlotInputDuringAnimation())
            {
                LogWeaponSlotInput(message.SlotIndex, "Queued different-slot change during weapon animation");
                return;
            }

            RefreshWeaponInHand();
            LogWeaponSlotInput(message.SlotIndex, "Applied different-slot change");
        }

        private void LogWeaponSlotInput(int requestedSlotIndex, string eventName)
        {
            Debug.Log($"[PlayerWeaponSlotInput] {eventName}. RequestedSlot={requestedSlotIndex}, ActiveSlot={selectedWeaponSlotIndex}, IsWeaponDrawn={isWeaponDrawn}, IsWeaponInHand={IsWeaponInHand}, DisplayMode={currentDisplayMode}, WeaponAnimationInProgress={isWeaponAnimationInProgress}, ActionBlocked={actionState.IsActionBlocked}.");
        }

        private void OnMouseDown(MouseDown message)
        {
            if (message.Button is not (MouseButtonType.Left or MouseButtonType.Right))
            {
                return;
            }

            if (!isInitialized)
            {
                return;
            }

            if (gameModesController.GameMode != GameMode.Game)
            {
                return;
            }

            // A hit reaction may also block the player, but only an active weapon attack
            // is allowed to receive a replacement request while movement is locked.
            if (actionState.IsActionBlocked && !combatActions.IsHitAttackInProgress)
            {
                return;
            }

            var selectedItemConfig = ResolveActiveWeaponSelection();
            if (selectedItemConfig == null)
            {
                return;
            }

            if (!isWeaponDrawn || !IsSelectedWeaponInHand(selectedItemConfig))
            {
                // A click on an unavailable selected weapon always means "ready the weapon",
                // never "attack". When a sheath/draw transition is already playing, preserve
                // that intent and apply it as soon as the current transition completes.
                isWeaponDrawn = true;

                if (isWeaponAnimationInProgress)
                {
                    hasPendingRefresh = true;
                    return;
                }

                RefreshWeaponInHand();
                return;
            }

            if (isWeaponAnimationInProgress)
            {
                return;
            }

            combatActions.TryTriggerAttack(message.Button);
        }

        private void OnGameModeChanged(GameModeChangedMessage message)
        {
            combatActions.HandleGameModeChanged(message);
        }

        private void OnDodgeInput(DodgeInputMessage _)
        {
            combatActions.TryRequestDodge();
        }

        private void OnRollInput(RollInputMessage _)
        {
            combatActions.TryRequestRoll();
        }

        private void RefreshWeaponInHand()
        {
            if (actionState.IsActionBlocked || combatActions.IsAttackBlockingWeaponChanges)
            {
                hasPendingRefresh = true;
                return;
            }

            var selectedItemConfig = ResolveActiveWeaponSelection();
            if (isWeaponAnimationInProgress)
            {
                hasPendingRefresh = true;
                return;
            }

            if (selectedItemConfig == null)
            {
                combatActions.Cancel();
                HandleEmptySelectedSlot();
                return;
            }

            if (!isWeaponDrawn)
            {
                combatActions.Cancel();
                HandleDesiredHolsteredWeapon(selectedItemConfig);
                return;
            }

            HandleDesiredWeaponInHand(selectedItemConfig);
        }

        private void RefreshWeaponAfterInitialization()
        {
            if (isInitialized)
            {
                RefreshWeaponInHand();
            }
        }

        private void HandleEmptySelectedSlot()
        {
            if (currentDisplayMode == WeaponDisplayMode.RightHand && currentWeaponItemConfig != null)
            {
                StartSheatheAnimation(currentRenderedSlotIndex, currentWeaponItemConfig);
                return;
            }

            RenderWeapon(null, 0, WeaponDisplayMode.None);
        }

        private void HandleDesiredWeaponInHand(ItemConfig selectedItemConfig)
        {
            if (currentDisplayMode == WeaponDisplayMode.RightHand)
            {
                if (currentRenderedSlotIndex == selectedWeaponSlotIndex
                 && currentWeaponItemConfig == selectedItemConfig)
                {
                    return;
                }

                StartSheatheAnimation(currentRenderedSlotIndex, currentWeaponItemConfig);
                return;
            }

            if (currentDisplayMode != WeaponDisplayMode.Belt
             || currentRenderedSlotIndex != selectedWeaponSlotIndex
             || currentWeaponItemConfig != selectedItemConfig)
            {
                RenderWeapon(selectedItemConfig, selectedWeaponSlotIndex, WeaponDisplayMode.Belt);
            }

            StartDrawAnimation(selectedWeaponSlotIndex, selectedItemConfig);
        }

        private bool IsSelectedWeaponInHand(ItemConfig selectedItemConfig)
        {
            return currentDisplayMode == WeaponDisplayMode.RightHand
                   && currentWeaponItemConfig == selectedItemConfig;
        }

        private void HandleDesiredHolsteredWeapon(ItemConfig selectedItemConfig)
        {
            if (currentDisplayMode == WeaponDisplayMode.RightHand && currentWeaponItemConfig != null)
            {
                StartSheatheAnimation(currentRenderedSlotIndex, currentWeaponItemConfig);
                return;
            }

            if (currentDisplayMode == WeaponDisplayMode.Belt
             && currentRenderedSlotIndex == selectedWeaponSlotIndex
             && currentWeaponItemConfig == selectedItemConfig)
            {
                return;
            }

            RenderWeapon(selectedItemConfig, selectedWeaponSlotIndex, WeaponDisplayMode.Belt);
        }

        private void StartDrawAnimation(int slotIndex, ItemConfig itemConfig, bool preserveCurrentVisual = false)
        {
            if (itemConfig == null || combatActions.IsAttackBlockingWeaponChanges)
            {
                return;
            }

            var canPreserveCurrentVisual =
                preserveCurrentVisual
             && currentWeaponInstance != null
             && currentWeaponItemConfig == itemConfig
             && currentRenderedSlotIndex == slotIndex
             && currentDisplayMode != WeaponDisplayMode.None;

            if (!canPreserveCurrentVisual
             && (currentDisplayMode != WeaponDisplayMode.Belt
              || currentRenderedSlotIndex != slotIndex
              || currentWeaponItemConfig != itemConfig))
            {
                RenderWeapon(itemConfig, slotIndex, WeaponDisplayMode.Belt);
            }

            weaponTransitionState.Begin(WeaponAnimationKind.Draw, preserveCurrentVisual);
            hasPendingRefresh = false;
            UpdateRunningAvailability();

            if (animator == null)
            {
                FinalizeWeaponRender(itemConfig, slotIndex, WeaponDisplayMode.RightHand, snapToAttachmentTransform: true);
                CompleteWeaponAnimationFromEvent(WeaponAnimationKind.Draw);
                return;
            }

            weaponTransitionAnimator.Request(WeaponAnimationKind.Draw);
        }

        private void StartSheatheAnimation(
            int slotIndex,
            ItemConfig itemConfig,
            bool ignoreActiveCombatAction = false)
        {
            if (itemConfig == null)
            {
                RenderWeapon(null, 0, WeaponDisplayMode.None);
                return;
            }

            if (!ignoreActiveCombatAction && combatActions.IsAttackBlockingWeaponChanges)
            {
                return;
            }

            weaponTransitionState.Begin(WeaponAnimationKind.Sheathe);
            hasPendingRefresh = false;
            UpdateRunningAvailability();

            if (animator == null)
            {
                FinalizeWeaponRender(itemConfig, slotIndex, WeaponDisplayMode.Belt, snapToAttachmentTransform: false);
                PutWeaponOnBeltFromAnimationEvent();
                return;
            }

            weaponTransitionAnimator.Request(WeaponAnimationKind.Sheathe);
        }

        private void CompleteWeaponAnimationFromEvent(WeaponAnimationKind expectedAnimationKind)
        {
            if (!isWeaponAnimationInProgress || currentAnimationKind != expectedAnimationKind)
            {
                return;
            }

            if (expectedAnimationKind == WeaponAnimationKind.Sheathe)
            {
                ConfirmWeaponSheathed();
            }

            weaponTransitionState.Complete(expectedAnimationKind);
            ResetAnimatorRequests();
            UpdateRunningAvailability();

            if (!hasPendingRefresh)
            {
                return;
            }

            hasPendingRefresh = false;
            RefreshWeaponInHand();
        }

        private void ConfirmWeaponSheathed()
        {
            // The animation event is the authoritative boundary: once the weapon reaches the
            // belt, its visual and gameplay states must commit together. This also covers
            // forced sheathing, where a session owns the request but the weapon controller
            // still owns the final state transition.
            isWeaponDrawn = false;
            combatActions.Cancel(restoreMovement: false);
            UpdateRunningAvailability();
        }

        private void SynchronizeSheatheCompletionWithAnimatorState()
        {
            if (!isWeaponAnimationInProgress
                || currentAnimationKind != WeaponAnimationKind.Sheathe
                || animator == null
                || weaponAnimationLayerIndex < 0)
            {
                return;
            }

            if (IsSheatheWeaponAnimationStateActive())
            {
                weaponTransitionState.MarkSheatheStateEntered();
                return;
            }

            if (!weaponTransitionState.CanSynchronizeSheathe()
                || currentWeaponItemConfig == null
                || !weaponVisual.IsAttachedTo(WeaponDisplayMode.Belt))
            {
                return;
            }

            FinalizeWeaponRender(
                currentWeaponItemConfig,
                currentRenderedSlotIndex,
                WeaponDisplayMode.Belt,
                snapToAttachmentTransform: false);
            CompleteWeaponAnimationFromEvent(WeaponAnimationKind.Sheathe);
            PublishWeaponSheathed();
        }

        private void PublishWeaponSheathed()
        {
            weaponSheathedPublisher?.Publish(new WeaponSheathedMessage(ownerDamageReceiver?.OwnerTransform));
        }

        private bool IsSheatheWeaponAnimationStateActive()
        {
            return weaponTransitionAnimator.IsStateActive(WeaponAnimationKind.Sheathe);
        }

        private void RequestWeaponTransitionReversal()
        {
            if (weaponVisual.IsAttachedTo(WeaponDisplayMode.RightHand)
                && weaponTransitionAnimator.IsStateActive(WeaponAnimationKind.Sheathe))
            {
                weaponTransitionAnimator.Request(WeaponAnimationKind.Draw);
                return;
            }

            if (weaponVisual.IsAttachedTo(WeaponDisplayMode.Belt)
                && weaponTransitionAnimator.IsStateActive(WeaponAnimationKind.Draw))
            {
                weaponTransitionAnimator.Request(WeaponAnimationKind.Sheathe);
            }
        }

        private void SynchronizeWeaponTransitionWithAnimationEvent(
            WeaponAnimationKind animationKind,
            bool preservePoseForDraw = false)
        {
            if (currentAnimationKind != animationKind)
            {
                weaponTransitionState.Begin(animationKind, preservePoseForDraw);
            }
        }

        private bool TryHandleWeaponSlotInputDuringAnimation()
        {
            if (!isWeaponAnimationInProgress)
            {
                return false;
            }

            var selectedItemConfig = GetSelectedWeaponItemConfig();

            switch (currentAnimationKind)
            {
                case WeaponAnimationKind.Sheathe:
                    if (!isWeaponDrawn || selectedItemConfig == null)
                    {
                        return true;
                    }

                    if (currentWeaponItemConfig == selectedItemConfig
                     && currentRenderedSlotIndex == selectedWeaponSlotIndex)
                    {
                        StartDrawAnimation(selectedWeaponSlotIndex, selectedItemConfig, preserveCurrentVisual: true);
                        return true;
                    }

                    if (TryStartNextWeaponDuringSheatheIfBeginEventPassed())
                    {
                        return true;
                    }

                    return true;

                case WeaponAnimationKind.Draw:
                    if (isWeaponDrawn
                     && selectedItemConfig != null
                     && currentWeaponItemConfig == selectedItemConfig
                     && currentRenderedSlotIndex == selectedWeaponSlotIndex)
                    {
                        return true;
                    }

                    StartSheatheAnimation(currentRenderedSlotIndex, currentWeaponItemConfig);
                    return true;

                default:
                    hasPendingRefresh = true;
                    return true;
            }
        }

        private bool TryStartNextWeaponDuringSheathe()
        {
            var selectedItemConfig = GetSelectedWeaponItemConfig();
            if (!ShouldSwapWeaponOnBeginMoveToBeltEvent(selectedItemConfig))
            {
                return false;
            }

            DestroyCurrentWeaponInstance();

            RenderWeapon(selectedItemConfig, selectedWeaponSlotIndex, WeaponDisplayMode.Belt);
            StartDrawAnimation(selectedWeaponSlotIndex, selectedItemConfig, preserveCurrentVisual: true);
            return true;
        }

        private void MoveCurrentWeaponToBeltPreservingPose()
        {
            weaponVisual.MovePreservingPose(WeaponDisplayMode.Belt);
            UpdateRunningAvailability();
        }

        private void MoveCurrentWeaponToRightHandPreservingPose()
        {
            weaponVisual.MovePreservingPose(WeaponDisplayMode.RightHand);
            UpdateRunningAvailability();
        }

        private bool TryStartNextWeaponDuringSheatheIfBeginEventPassed()
        {
            if (!weaponTransitionAnimator.TryGetEventNormalizedTime(
                    WeaponAnimationKind.Sheathe,
                    BeginMoveWeaponToBeltEventName,
                    out var beginMoveNormalizedTime))
            {
                return false;
            }

            if (animator == null || weaponAnimationLayerIndex < 0)
            {
                return false;
            }

            var stateInfo = animator.GetCurrentAnimatorStateInfo(weaponAnimationLayerIndex);
            if (stateInfo.fullPathHash != weaponTransitionAnimator.GetStateHash(WeaponAnimationKind.Sheathe)
             || stateInfo.normalizedTime < beginMoveNormalizedTime)
            {
                return false;
            }

            return TryStartNextWeaponDuringSheathe();
        }

        private bool ShouldSwapWeaponOnBeginMoveToBeltEvent(ItemConfig selectedItemConfig)
        {
            return currentAnimationKind == WeaponAnimationKind.Sheathe
                && currentDisplayMode == WeaponDisplayMode.RightHand
                && currentWeaponItemConfig != null
                && isWeaponDrawn
                && selectedItemConfig != null
                && (currentRenderedSlotIndex != selectedWeaponSlotIndex
                 || currentWeaponItemConfig != selectedItemConfig);
        }

        private ItemConfig GetSelectedWeaponItemConfig()
        {
            var selectedSlot = selectedWeaponSlotIndex == 1
                ? playerInventory.LeftWeaponSlot
                : playerInventory.RightWeaponSlot;
            var itemConfig = selectedSlot?.ItemConfig;

            return itemConfig?.ItemType == ItemType.Weapon
                ? itemConfig
                : null;
        }

        private ItemConfig ResolveActiveWeaponSelection()
        {
            return GetSelectedWeaponItemConfig();
        }

        private void RenderWeapon(ItemConfig itemConfig, int slotIndex, WeaponDisplayMode displayMode)
        {
            EndCurrentWeaponDamageWindow();
            weaponVisual.Render(itemConfig, slotIndex, displayMode);
            UpdateRunningAvailability();
        }

        private void FinalizeWeaponRender(
            ItemConfig itemConfig,
            int slotIndex,
            WeaponDisplayMode displayMode,
            bool snapToAttachmentTransform)
        {
            weaponVisual.FinalizeRender(itemConfig, slotIndex, displayMode, snapToAttachmentTransform);
            UpdateRunningAvailability();
        }

        private void DestroyCurrentWeaponInstance()
        {
            EndCurrentWeaponDamageWindow();
            weaponVisual.Destroy();
        }

        private void CleanupSpawnedWeaponInstancesExceptCurrent()
        {
            weaponVisual.CleanupExceptCurrent();
        }

        private void EndCurrentWeaponDamageWindow()
        {
            combatActions.EndDamageWindowFromAnimationEvent();
        }

        private void StartWeaponAttachmentBlend(
            WeaponDisplayMode targetMode,
            string startEventName,
            string finishEventName)
        {
            weaponVisual.StartAttachmentBlend(
                targetMode,
                weaponTransitionAnimator.GetClip(currentAnimationKind),
                GetCurrentAnimationStateHash(),
                startEventName,
                finishEventName);
        }

        private int GetCurrentAnimationStateHash()
        {
            return currentAnimationKind == WeaponAnimationKind.None
                ? 0
                : weaponTransitionAnimator.GetStateHash(currentAnimationKind);
        }

        private void ResetAnimatorRequests()
        {
            if (animator == null)
            {
                return;
            }

            weaponTransitionAnimator.ResetRequests();
            combatActions.ResetAnimationRequests();
        }

        private void UpdateRunningAvailability()
        {
            var shouldAllowRunning =
                !combatActions.IsAttackRootMotionStateActive
             && currentAnimationKind != WeaponAnimationKind.Draw
             && (currentDisplayMode != WeaponDisplayMode.RightHand || currentWeaponItemConfig == null);

            playerMovement?.SetRunAllowed(shouldAllowRunning);
        }

    }
}
