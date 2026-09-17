using System;
using Combat;
using GameModes;
using Inventory.Inventories;
using Inventory.Item;
using MessagePipe;
using Messages;
using Movement;
using Stats;
using TargetLock;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Inventory
{
    /// <summary>
    /// Application coordinator for the player's equipped weapon. It accepts player intent and
    /// inventory changes, waits for gameplay-owned blocking states, and delegates the Animator
    /// and visual handoff to <see cref="PlayerWeaponTransitionController"/>.
    /// </summary>
    public sealed class PlayerWeaponInHandController : IWeaponAnimationEventHandler, IEquippedWeaponVisual, IStartable, ITickable, IDisposable
    {
        private readonly GameModesController gameModesController;
        private readonly PlayerInventory playerInventory;
        private readonly PlayerMovement playerMovement;
        private readonly CharacterDamageReceiver ownerDamageReceiver;
        private readonly CharacterActionState actionState;
        private readonly IPublisher<WeaponSheathedMessage> weaponSheathedPublisher;
        private readonly CompositeDisposable disposables = new();
        private readonly PlayerWeaponInputSubscriptions inputSubscriptions;
        private readonly PlayerWeaponCombatActionController combatActions;
        private readonly PlayerWeaponTransitionController weaponTransitions;
        private readonly PlayerWeaponDrawingBlockState weaponDrawingBlockState;
        private readonly PlayerWeaponIntentState weaponIntent = new();

        private bool isInitialized;
        private bool hasPendingPresentationReconciliation;

        public PlayerWeaponInHandController
            (
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
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber,
            PlayerWeaponDrawingBlockState weaponDrawingBlockState
            )
        {
            this.gameModesController = gameModesController;
            this.playerInventory = playerInventory;
            this.playerMovement = playerMovement;
            this.ownerDamageReceiver = ownerDamageReceiver;
            this.actionState = actionState;
            this.weaponSheathedPublisher = weaponSheathedPublisher;
            this.weaponDrawingBlockState = weaponDrawingBlockState;
            weaponDrawingBlockState.Changed += OnWeaponDrawingBlockChanged;
            disposables.Add(Disposable.Create(() => weaponDrawingBlockState.Changed -= OnWeaponDrawingBlockChanged));

            var transitionAnimator = new PlayerWeaponTransitionAnimator(animator);
            var weaponVisual = new PlayerWeaponVisualController(handAnchor, animator, transitionAnimator.LayerIndex);
            var weaponCombatAnimator = new PlayerWeaponCombatAnimator(animator);
            var damageWindow = new EquippedWeaponDamageWindowController();
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
                () => weaponVisual.Instance,
                () => weaponVisual.ItemConfig,
                HandleFullBodyActionRequested);
            weaponTransitions = new PlayerWeaponTransitionController(
                weaponVisual,
                transitionAnimator,
                combatActions.EndDamageWindowFromAnimationEvent);

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
            playerInventory.Changed.Subscribe(_ => ReconcilePresentationAfterInitialization()).AddTo(disposables);
            playerInventory.HandSlot.Subscribe(_ => ReconcilePresentationAfterInitialization()).AddTo(disposables);
        }

        public bool IsWeaponSheathed => weaponTransitions.IsSheathed;
        public bool IsWeaponDrawn => weaponIntent.IsDrawRequested;
        public bool CanProcessWeaponSlotInput => isInitialized
                                                  && !actionState.IsActionBlocked
                                                  && !combatActions.IsAttackBlockingWeaponChanges;
        public int ActiveWeaponSlotIndex => weaponIntent.SelectedSlotIndex;

        /// <summary>
        /// An ordinary sheathing transition can begin only when no full-body combat action owns
        /// the Animator. Callers that need to sequence this operation should wait for this value.
        /// </summary>
        public bool CanStartWeaponSheathing => IsWeaponSheathed
                                              || (!weaponTransitions.IsAnimationInProgress
                                                  && !combatActions.IsAttackRootMotionStateActive);

        public bool IsCombatActionLocked => combatActions.IsCombatActionLocked;
        public bool IsRollAnimationActive => combatActions.IsRollAnimationActive;

        public void Start()
        {
            ResetAnimatorRequests();
            combatActions.Start();

            // Initial inventory loading has already completed. Its notifications establish the
            // belt visual; an initial item set must not implicitly draw a weapon.
            isInitialized = true;
            ReconcileWeaponPresentation();
            UpdateRunningAvailability();
        }

        public void Tick()
        {
            combatActions.Tick();
            HandleTransitionOutcome(weaponTransitions.SynchronizeSheatheCompletionWithAnimatorState());

            if (hasPendingPresentationReconciliation && CanReconcileWeaponPresentation())
            {
                hasPendingPresentationReconciliation = false;
                ReconcileWeaponPresentation();
            }
        }

        public void Dispose()
        {
            combatActions.Dispose();
            weaponTransitions.Dispose();
            inputSubscriptions.Dispose();
            disposables.Dispose();
        }

        public void BeginMoveWeaponToRightHandFromAnimationEvent()
        {
            HandleTransitionOutcome(weaponTransitions.BeginMoveWeaponToRightHandFromAnimationEvent());
        }

        public void TakeWeaponInHandFromAnimationEvent()
        {
            HandleTransitionOutcome(weaponTransitions.TakeWeaponInHandFromAnimationEvent());
        }

        public void BeginMoveWeaponToBeltFromAnimationEvent()
        {
            HandleTransitionOutcome(weaponTransitions.BeginMoveWeaponToBeltFromAnimationEvent(
                GetSelectedWeapon(),
                IsEffectiveDrawRequested));
        }

        public void PutWeaponOnBeltFromAnimationEvent()
        {
            HandleTransitionOutcome(weaponTransitions.PutWeaponOnBeltFromAnimationEvent(GetSelectedWeapon()));
        }

        public void HoldAttackReadyFromAnimationEvent() { }

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
            ReconcileWeaponPresentation();
        }

        public void ResetAttackRequestFromAnimationEvent()
        {
            combatActions.ResetAnimationRequests();
        }

        public void InterruptByHitReaction()
        {
            combatActions.InterruptByHitReaction();
            ResetAnimatorRequests();

            if (!weaponDrawingBlockState.IsWeaponDrawingBlocked
                && weaponTransitions.TryReverseForFullBodyAction(out var isDrawRequested))
            {
                weaponIntent.SetDrawRequest(isDrawRequested);
            }
            else if (weaponDrawingBlockState.IsWeaponDrawingBlocked)
            {
                RequestPresentationReconciliation();
            }

            UpdateRunningAvailability();
        }

        /// <summary>
        /// Requests the normal clip-owned sheathing transition. This is intentionally public for
        /// gameplay sessions that own their own sequence, such as the sparring conclusion.
        /// </summary>
        public void RequestSheatheWeapon()
        {
            if (!weaponIntent.IsDrawRequested && weaponTransitions.DisplayMode != WeaponDisplayMode.RightHand)
            {
                return;
            }

            combatActions.Cancel(restoreMovement: false);
            weaponIntent.RequestSheathe();
            HandleTransitionOutcome(weaponTransitions.RequestSheathe());
        }

        public bool TryGetCurrentWeaponSlot(out Inventory.Slot.SlotModel slot)
        {
            slot = GetWeaponSlot(weaponTransitions.CurrentSlotIndex);
            if (IsWeaponSlot(slot))
            {
                return true;
            }

            slot = GetWeaponSlot(weaponIntent.SelectedSlotIndex);
            if (IsWeaponSlot(slot))
            {
                return true;
            }

            if (IsWeaponSlot(playerInventory.LeftWeaponSlot))
            {
                slot = playerInventory.LeftWeaponSlot;
                return true;
            }

            if (IsWeaponSlot(playerInventory.RightWeaponSlot))
            {
                slot = playerInventory.RightWeaponSlot;
                return true;
            }

            slot = null;
            return false;
        }

        public bool TryGetCurrentWeaponPose(out Vector3 position, out Quaternion rotation)
        {
            return weaponTransitions.TryGetCurrentWeaponPose(out position, out rotation);
        }

        private void OnWeaponSlotInput(WeaponSlotInputMessage message)
        {
            if (weaponDrawingBlockState.IsWeaponDrawingBlocked)
            {
                return;
            }

            if (gameModesController.GameMode == GameMode.Dialogue)
            {
                // Dialogue never begins a draw. It still permits the player to stow a weapon
                // that was already in hand when the conversation started.
                if (weaponIntent.IsDrawRequested || weaponTransitions.DisplayMode == WeaponDisplayMode.RightHand)
                {
                    RequestSheatheWeapon();
                }

                return;
            }

            if (!CanProcessWeaponSlotInput || message.SlotIndex is < 1 or > 2)
            {
                return;
            }

            if (weaponIntent.IsSelectedSlot(message.SlotIndex))
            {
                if (!GetSelectedWeapon().HasWeapon)
                {
                    ReconcileWeaponPresentation();
                    return;
                }

                weaponIntent.ToggleDrawRequest();
            }
            else
            {
                weaponIntent.SelectSlotAndRequestDraw(message.SlotIndex);
            }

            if (TryApplyWeaponSlotIntentDuringTransition())
            {
                return;
            }

            ReconcileWeaponPresentation();
        }

        private void OnMouseDown(MouseDown message)
        {
            if (weaponDrawingBlockState.IsWeaponDrawingBlocked)
            {
                return;
            }

            if (message.Button is not (MouseButtonType.Left or MouseButtonType.Right)
             || !isInitialized
             || gameModesController.GameMode != GameMode.Game)
            {
                return;
            }

            // A hit reaction can block movement but may accept a follow-up attack. Other
            // blocked actions do not receive an attack or draw request from the mouse.
            if (actionState.IsActionBlocked && !combatActions.IsHitAttackInProgress)
            {
                return;
            }

            var selectedWeapon = GetSelectedWeapon();
            if (!selectedWeapon.HasWeapon)
            {
                return;
            }

            if (!weaponIntent.IsDrawRequested || !weaponTransitions.IsSelectionInHand(selectedWeapon))
            {
                weaponIntent.RequestDraw();
                if (weaponTransitions.IsAnimationInProgress)
                {
                    RequestPresentationReconciliation();
                    return;
                }

                ReconcileWeaponPresentation();
                return;
            }

            if (!weaponTransitions.IsAnimationInProgress)
            {
                combatActions.TryTriggerAttack(message.Button);
            }
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

        private void HandleFullBodyActionRequested()
        {
            if (!weaponDrawingBlockState.IsWeaponDrawingBlocked
                && weaponTransitions.TryReverseForFullBodyAction(out var isDrawRequested))
            {
                weaponIntent.SetDrawRequest(isDrawRequested);
            }
            else if (weaponDrawingBlockState.IsWeaponDrawingBlocked)
            {
                RequestPresentationReconciliation();
            }

            UpdateRunningAvailability();
        }

        private bool TryApplyWeaponSlotIntentDuringTransition()
        {
            if (!weaponTransitions.TryApplyWeaponSlotIntentDuringTransition(
                    GetSelectedWeapon(),
                    IsEffectiveDrawRequested,
                    out var outcome))
            {
                return false;
            }

            HandleTransitionOutcome(outcome);
            return true;
        }

        private void ReconcileWeaponPresentation()
        {
            if (!CanReconcileWeaponPresentation())
            {
                RequestPresentationReconciliation();
                return;
            }

            if (weaponTransitions.IsAnimationInProgress)
            {
                RequestPresentationReconciliation();
                return;
            }

            var selectedWeapon = GetSelectedWeapon();
            if (!selectedWeapon.HasWeapon || !IsEffectiveDrawRequested)
            {
                combatActions.Cancel();
            }

            hasPendingPresentationReconciliation = false;
            HandleTransitionOutcome(weaponTransitions.Reconcile(selectedWeapon, IsEffectiveDrawRequested));
        }

        private bool CanReconcileWeaponPresentation()
        {
            return !actionState.IsActionBlocked && !combatActions.IsAttackBlockingWeaponChanges;
        }

        private void ReconcilePresentationAfterInitialization()
        {
            if (isInitialized)
            {
                ReconcileWeaponPresentation();
            }
        }

        private void OnWeaponDrawingBlockChanged()
        {
            if (!isInitialized || !weaponDrawingBlockState.IsWeaponDrawingBlocked)
            {
                return;
            }

            weaponIntent.RequestSheathe();
            ReconcileWeaponPresentation();
        }

        private void RequestPresentationReconciliation()
        {
            hasPendingPresentationReconciliation = true;
        }

        private void HandleTransitionOutcome(WeaponTransitionOutcome outcome)
        {
            if (outcome != WeaponTransitionOutcome.None)
            {
                ResetAnimatorRequests();
            }

            if (outcome == WeaponTransitionOutcome.Sheathed)
            {
                if (!GetSelectedWeapon().HasWeapon)
                {
                    weaponIntent.RequestSheathe();
                }

                combatActions.Cancel(restoreMovement: false);
                PublishWeaponSheathed();

                // An interrupted transition can finish after the player has chosen another
                // weapon. Reconcile in Tick rather than starting a new transition inside an
                // animation-event callback.
                if (IsEffectiveDrawRequested)
                {
                    RequestPresentationReconciliation();
                }
            }

            UpdateRunningAvailability();
        }

        private void PublishWeaponSheathed()
        {
            weaponSheathedPublisher?.Publish(new WeaponSheathedMessage(ownerDamageReceiver?.OwnerTransform));
        }

        private PlayerWeaponSelection GetSelectedWeapon()
        {
            var slot = GetWeaponSlot(weaponIntent.SelectedSlotIndex);
            var itemConfig = IsWeaponSlot(slot) ? slot.ItemConfig : null;
            return new PlayerWeaponSelection(weaponIntent.SelectedSlotIndex, itemConfig);
        }

        private bool IsEffectiveDrawRequested => weaponIntent.IsDrawRequested
                                                 && !weaponDrawingBlockState.IsWeaponDrawingBlocked;

        private Inventory.Slot.SlotModel GetWeaponSlot(int slotIndex)
        {
            return slotIndex switch
            {
                1 => playerInventory.LeftWeaponSlot,
                2 => playerInventory.RightWeaponSlot,
                _ => null
            };
        }

        private static bool IsWeaponSlot(Inventory.Slot.SlotModel slot)
        {
            return slot?.ItemConfig?.ItemType == ItemType.Weapon;
        }

        private void ResetAnimatorRequests()
        {
            weaponTransitions.ResetAnimatorRequests();
            combatActions.ResetAnimationRequests();
        }

        private void UpdateRunningAvailability()
        {
            var shouldAllowRunning =
                !combatActions.IsAttackRootMotionStateActive
                && weaponTransitions.CurrentAnimationKind != WeaponAnimationKind.Draw
                && (weaponTransitions.DisplayMode != WeaponDisplayMode.RightHand
                    || weaponTransitions.CurrentItemConfig == null);

            playerMovement?.SetRunAllowed(shouldAllowRunning);
        }
    }
}
