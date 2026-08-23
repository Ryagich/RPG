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
using Object = UnityEngine.Object;
using Stats;

namespace Inventory
{
    public sealed class PlayerWeaponInHandController : IWeaponAnimationEventHandler, IEquippedWeaponVisual, IStartable, ITickable, IDisposable
    {
        private const string WeaponAnimationLayerName = "Weapon Handling";
        private const string AttackLayerName = "Full Body";
        private const string DrawWeaponStatePath = "Weapon Handling.DrawWeapon";
        private const string SheatheWeaponStatePath = "Weapon Handling.SheatheWeapon";
        private const string EmptyIdleStateName = "Empty Idle";
        private const string AttackStateName = "A_Attack_LightCombo01C_Sword";
        private const string AttackComboAStateName = "A_Attack_LightCombo01A_Sword";
        private const string AttackComboBHitStateName = "A_Attack_LightCombo01B_Hit_Sword";
        private const string AttackWindUpStateName = "A_Attack_LightCombo01C_WindUp_Sword";
        private const string AttackHitStateName = "A_Attack_LightCombo01C_Hit_Sword";
        private const string AttackFollowThroughStateName = "A_Attack_LightCombo01C_FollowThrough_Sword";
        private const string AttackComboAReturnToIdleStateName = "A_Attack_LightCombo01A_ReturnToIdle_Sword";
        private const string AttackComboBReturnToIdleStateName = "A_Attack_LightCombo01B_ReturnToIdle_Sword";
        private const string AttackRootMotionStateName = "A_Attack_LightCombo01C_RootMotion_Sword";
        private const string AttackWindUpRootMotionStateName = "A_Attack_LightCombo01C_WindUp_RootMotion_Sword";
        private const string AttackHitRootMotionStateName = "A_Attack_LightCombo01C_Hit_RootMotion_Sword";
        private const string AttackFollowThroughRootMotionStateName = "A_Attack_LightCombo01C_FollowThrough_RootMotion_Sword";
        private const string AttackComboCReturnToIdleRootMotionStateName = "A_Attack_LightCombo01C_ReturnToIdle_RootMotion_Sword";
        private const string HitBackStateName = "A_Hit_B_Stagger_RootMotion_Sword";
        private const string HitFrontStateName = "A_Hit_F_Stagger_RootMotion_Sword";
        private const string HitRightStateName = "A_Hit_R_Stagger_RootMotion_Sword";
        private const string HitLeftStateName = "A_Hit_L_Stagger_RootMotion_Sword";
        private const string HeavyAttackHitRootMotionStateName = "A_Attack_HeavyCombo01A_Hit_RootMotion_Sword";
        private const string HeavyAttackHitStateName = "A_Attack_HeavyCombo01B_Hit_Sword";
        private const string DodgeStateName = "Dodge Tree";
        private const string RollStateName = "Dodge RollTree";
        private const string DrawWeaponClipName = "A_Draw_Sword";
        private const string SheatheWeaponClipName = "A_Sheathe_Sword";
        private const float FallbackAttachmentBlendDuration = 0.08f;
        private const string BeginMoveWeaponToRightHandEventName = "BeginMoveWeaponToRightHand";
        private const string TakeWeaponInHandEventName = "TakeWeaponInHand";
        private const string BeginMoveWeaponToBeltEventName = "BeginMoveWeaponToBelt";
        private const string PutWeaponOnBeltEventName = "PutWeaponOnBelt";
        private const string DrawWeaponRequestedParameter = "MoveWeaponInHand";
        private const string SheatheWeaponRequestedParameter = "MoveWeaponInBelt";
        private const string AttackRequestedParameter = "Attack";
        private const string HeavyAttackRequestedParameter = "HeavyAttack";
        private const string DodgeRequestedParameter = "Dodge";
        private const string RollRequestedParameter = "Roll";

        private static readonly int DrawWeaponStateHash = Animator.StringToHash(DrawWeaponStatePath);
        private static readonly int SheatheWeaponStateHash = Animator.StringToHash(SheatheWeaponStatePath);
        private static readonly int EmptyIdleStateShortNameHash = Animator.StringToHash(EmptyIdleStateName);
        private static readonly int AttackStateShortNameHash = Animator.StringToHash(AttackStateName);
        private static readonly int AttackComboAStateShortNameHash = Animator.StringToHash(AttackComboAStateName);
        private static readonly int AttackComboBHitStateShortNameHash = Animator.StringToHash(AttackComboBHitStateName);
        private static readonly int AttackWindUpStateShortNameHash = Animator.StringToHash(AttackWindUpStateName);
        private static readonly int AttackHitStateShortNameHash = Animator.StringToHash(AttackHitStateName);
        private static readonly int AttackFollowThroughStateShortNameHash = Animator.StringToHash(AttackFollowThroughStateName);
        private static readonly int AttackComboAReturnToIdleStateShortNameHash = Animator.StringToHash(AttackComboAReturnToIdleStateName);
        private static readonly int AttackComboBReturnToIdleStateShortNameHash = Animator.StringToHash(AttackComboBReturnToIdleStateName);
        private static readonly int AttackRootMotionStateShortNameHash = Animator.StringToHash(AttackRootMotionStateName);
        private static readonly int AttackWindUpRootMotionStateShortNameHash = Animator.StringToHash(AttackWindUpRootMotionStateName);
        private static readonly int AttackHitRootMotionStateShortNameHash = Animator.StringToHash(AttackHitRootMotionStateName);
        private static readonly int AttackFollowThroughRootMotionStateShortNameHash = Animator.StringToHash(AttackFollowThroughRootMotionStateName);
        private static readonly int AttackComboCReturnToIdleRootMotionStateShortNameHash = Animator.StringToHash(AttackComboCReturnToIdleRootMotionStateName);
        private static readonly int HitBackStateShortNameHash = Animator.StringToHash(HitBackStateName);
        private static readonly int HitFrontStateShortNameHash = Animator.StringToHash(HitFrontStateName);
        private static readonly int HitRightStateShortNameHash = Animator.StringToHash(HitRightStateName);
        private static readonly int HitLeftStateShortNameHash = Animator.StringToHash(HitLeftStateName);
        private static readonly int HeavyAttackHitRootMotionStateShortNameHash = Animator.StringToHash(HeavyAttackHitRootMotionStateName);
        private static readonly int HeavyAttackHitStateShortNameHash = Animator.StringToHash(HeavyAttackHitStateName);
        private static readonly int DodgeStateShortNameHash = Animator.StringToHash(DodgeStateName);
        private static readonly int RollStateShortNameHash = Animator.StringToHash(RollStateName);
        private static readonly int DrawWeaponRequestedParameterHash = Animator.StringToHash(DrawWeaponRequestedParameter);
        private static readonly int SheatheWeaponRequestedParameterHash = Animator.StringToHash(SheatheWeaponRequestedParameter);
        private static readonly int AttackRequestedParameterHash = Animator.StringToHash(AttackRequestedParameter);
        private static readonly int HeavyAttackRequestedParameterHash = Animator.StringToHash(HeavyAttackRequestedParameter);
        private static readonly int DodgeRequestedParameterHash = Animator.StringToHash(DodgeRequestedParameter);
        private static readonly int RollRequestedParameterHash = Animator.StringToHash(RollRequestedParameter);

        private enum WeaponDisplayMode
        {
            None,
            RightHand,
            Belt
        }

        private enum WeaponAnimationKind
        {
            None,
            Draw,
            Sheathe
        }

        private readonly PlayerInventory playerInventory;
        private readonly PlayerWeaponHandAnchor handAnchor;
        private readonly PlayerWeaponAnimationEventReceiver animationEventReceiver;
        private readonly Animator animator;
        private readonly CharacterRootMotionController rootMotionController;
        private readonly GameModesController gameModesController;
        private readonly PlayerMovement playerMovement;
        private readonly PlayerMovementConfig playerMovementConfig;
        private readonly PlayerAnimationController playerAnimationController;
        private readonly TargetLockController targetLockController;
        private readonly CharacterDamageReceiver ownerDamageReceiver;
        private readonly CharacterActionState actionState;
        private readonly StatsController statsController;
        private readonly IPublisher<PlayerEvasionCompletedMessage> evasionCompletedPublisher;
        private readonly IPublisher<WeaponSheathedMessage> weaponSheathedPublisher;
        private readonly CompositeDisposable disposables = new();
        private readonly SerialDisposable weaponAttachmentBlendDisposable = new();
        private readonly int weaponAnimationLayerIndex;
        private readonly int attackLayerIndex;

        private int selectedWeaponSlotIndex = 1;
        private bool isInitialized;
        private bool isWeaponDrawn;
        private bool isWeaponAnimationInProgress;
        private bool hasEnteredSheatheAnimationState;
        private bool hasPendingRefresh;
        private bool isHitAttackInProgress;
        private bool isCombatActionLocked;
        private bool hasAttachmentBlendStartedForCurrentAnimation;
        private bool shouldPreservePoseForCurrentDraw;
        private GameObject currentWeaponInstance;
        private ItemConfig currentWeaponItemConfig;
        private WeaponDamageZone activeDamageZone;
        private int currentRenderedSlotIndex;
        private WeaponDisplayMode currentDisplayMode;
        private WeaponAnimationKind currentAnimationKind;
        private string currentAnimationClipName;

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
            this.playerInventory = playerInventory;
            this.handAnchor = handAnchor;
            this.animationEventReceiver = animationEventReceiver;
            this.animator = animator;
            this.rootMotionController = rootMotionController;
            this.gameModesController = gameModesController;
            this.playerMovement = playerMovement;
            this.playerMovementConfig = playerMovementConfig;
            this.playerAnimationController = playerAnimationController;
            this.targetLockController = targetLockController;
            this.ownerDamageReceiver = ownerDamageReceiver;
            this.actionState = actionState;
            this.statsController = statsController;
            this.evasionCompletedPublisher = evasionCompletedPublisher;
            this.weaponSheathedPublisher = weaponSheathedPublisher;
            weaponAnimationLayerIndex = animator != null
                ? animator.GetLayerIndex(WeaponAnimationLayerName)
                : -1;
            attackLayerIndex = animator != null
                ? animator.GetLayerIndex(AttackLayerName)
                : -1;
            animationEventReceiver?.Bind(this);

            weaponSlotInputSubscriber.Subscribe(OnWeaponSlotInput).AddTo(disposables);
            mouseDownSubscriber.Subscribe(OnMouseDown).AddTo(disposables);
            dodgeInputSubscriber.Subscribe(OnDodgeInput).AddTo(disposables);
            rollInputSubscriber.Subscribe(OnRollInput).AddTo(disposables);
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged).AddTo(disposables);
            playerInventory.Changed.Subscribe(_ => RefreshWeaponAfterInitialization()).AddTo(disposables);
            playerInventory.HandSlot.Subscribe(_ => RefreshWeaponAfterInitialization()).AddTo(disposables);
        }

        public void Start()
        {
            ResetAnimatorRequests();
            playerAnimationController?.ReleaseEvasionDirection();
            ownerDamageReceiver?.SetWeaponDamageBlocked(false);

            // The initial item set is applied before this entry point starts. Its inventory
            // notifications must establish the initial belt presentation, not begin a draw
            // transition before the player has requested one.
            isInitialized = true;
            RefreshWeaponInHand();
            UpdateRunningAvailability();
            UpdateAttackRootMotionAvailability();
        }

        public void Dispose()
        {
            playerAnimationController?.ReleaseEvasionDirection();
            ownerDamageReceiver?.SetWeaponDamageBlocked(false);
            weaponAttachmentBlendDisposable.Dispose();
            DestroyCurrentWeaponInstance();
            UpdateAttackRootMotionAvailability(forceDisable: true);
            disposables.Dispose();
        }

        public void Tick()
        {
            UpdateAttackRootMotionAvailability();
            SynchronizeSheatheCompletionWithAnimatorState();

            if (hasPendingRefresh
             && !isWeaponAnimationInProgress
             && !actionState.IsActionBlocked
             && !IsAttackBlockingWeaponChanges())
            {
                hasPendingRefresh = false;
                RefreshWeaponInHand();
            }
        }

        public void BeginMoveWeaponToRightHandFromAnimationEvent()
        {
            if (currentAnimationKind != WeaponAnimationKind.Draw || hasAttachmentBlendStartedForCurrentAnimation)
            {
                return;
            }

            hasAttachmentBlendStartedForCurrentAnimation = true;

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
            if (currentAnimationKind != WeaponAnimationKind.Draw || currentWeaponItemConfig == null)
            {
                return;
            }

            FinalizeWeaponRender(
                currentWeaponItemConfig,
                currentRenderedSlotIndex,
                WeaponDisplayMode.RightHand,
                snapToAttachmentTransform: true);
            CleanupSpawnedWeaponInstancesExceptCurrent();
            CompleteWeaponAnimationFromEvent(WeaponAnimationKind.Draw);
        }

        public void BeginMoveWeaponToBeltFromAnimationEvent()
        {
            if (currentAnimationKind != WeaponAnimationKind.Sheathe || hasAttachmentBlendStartedForCurrentAnimation)
            {
                return;
            }

            hasAttachmentBlendStartedForCurrentAnimation = true;

            if (TryStartNextWeaponDuringSheathe())
            {
                return;
            }

            MoveCurrentWeaponToBeltPreservingPose();
        }

        public void PutWeaponOnBeltFromAnimationEvent()
        {
            if (currentAnimationKind != WeaponAnimationKind.Sheathe || currentWeaponItemConfig == null)
            {
                return;
            }

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
            // Animation Event: AttackStarted
            // Use on attack clips when the combat action itself begins.
            // This event is stronger than a pure movement lock: it marks attack-in-progress,
            // enables attack root motion handling, and also removes player control.
            isHitAttackInProgress = true;
            isCombatActionLocked = true;
            UpdateAttackRootMotionAvailability();

            playerMovement?.ChangeState(false);
            playerAnimationController?.SetLocomotionLocked(true);
        }

        public void BeginDamageWindowFromAnimationEvent()
        {
            BeginCurrentWeaponDamageWindow();
        }

        public void EndDamageWindowFromAnimationEvent()
        {
            EndCurrentWeaponDamageWindow();
        }

        public bool IsWeaponSheathed => !isWeaponAnimationInProgress
                                       && currentDisplayMode != WeaponDisplayMode.RightHand;

        public bool IsWeaponDrawn => isWeaponDrawn;

        public bool CanProcessWeaponSlotInput => isInitialized
                                                  && !actionState.IsActionBlocked
                                                  && !IsAttackBlockingWeaponChanges();

        private bool IsWeaponInHand => isWeaponDrawn
                                      && currentDisplayMode == WeaponDisplayMode.RightHand
                                      && currentWeaponItemConfig != null;

        public int ActiveWeaponSlotIndex => selectedWeaponSlotIndex;

        /// <summary>
        /// Indicates that the ordinary sheathing animation can begin without replacing an
        /// active full-body action or another weapon transition.
        /// </summary>
        public bool CanStartWeaponSheathing => IsWeaponSheathed
                                              || (!isWeaponAnimationInProgress && !IsAttackRootMotionStateActive());

        /// <summary>
        /// True from the animation's LockMovement event until its matching UnlockMovement event.
        /// </summary>
        public bool IsCombatActionLocked => isCombatActionLocked;

        /// <summary>
        /// True while the Roll request, transition, or its full-body animation is active.
        /// Consumers that synchronize with a roll must wait for this to become false rather
        /// than relying on the earlier UnlockMovement animation event.
        /// </summary>
        public bool IsRollAnimationActive => IsRollInProgress();

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

            CancelAttackFlow(restoreMovement: false);
            isWeaponDrawn = false;
            StartSheatheAnimation(currentRenderedSlotIndex, currentWeaponItemConfig, ignoreActiveCombatAction: true);
        }

        public void EnableDamageImmunityFromAnimationEvent()
        {
            ownerDamageReceiver?.SetWeaponDamageBlocked(true);
        }

        public void DisableDamageImmunityFromAnimationEvent()
        {
            ownerDamageReceiver?.SetWeaponDamageBlocked(false);
        }

        public void LockMovementFromAnimationEvent()
        {
            // Animation Event: LockMovement
            // Use on attack / return-to-idle clips when control should be taken away
            // without changing the rest of the attack flow state.
            isCombatActionLocked = true;
            playerMovement?.ChangeState(false);
            playerAnimationController?.SetLocomotionLocked(true);
        }

        public void UnlockMovementFromAnimationEvent()
        {
            // Animation Event: UnlockMovement
            // Use on attack / return-to-idle clips when control can be returned
            // before the full attack flow has completely ended.
            var completedEvasion = IsEvasionInProgress();
            var completedRoll = IsRollInProgress();
            isCombatActionLocked = false;
            playerAnimationController?.ReleaseEvasionDirection();

            if (gameModesController.GameMode == GameMode.Game)
            {
                playerMovement?.ChangeState(true);
            }

            playerAnimationController?.SetLocomotionLocked(false);

            if (completedEvasion)
            {
                evasionCompletedPublisher.Publish(new PlayerEvasionCompletedMessage(completedRoll));
            }
        }

        public void AttackFinishedFromAnimationEvent()
        {
            isHitAttackInProgress = false;
            isCombatActionLocked = false;
            UpdateAttackRootMotionAvailability();

            if (gameModesController.GameMode == GameMode.Game)
            {
                playerMovement?.ChangeState(true);
            }

            playerAnimationController?.SetLocomotionLocked(false);
            UpdateRunningAvailability();
            RefreshWeaponInHand();
        }

        public void ResetAttackRequestFromAnimationEvent()
        {
            // Legacy event endpoint: ResetAttackRequest.
            // Its meaning is now "reset animation requests": both mutually exclusive
            // attack request bools are cleared together.
            ResetAnimationRequests();
        }

        public void InterruptByHitReaction()
        {
            playerAnimationController?.ReleaseEvasionDirection();
            CancelAttackFlow(restoreMovement: false);
            // A damage window can be active before the AttackStarted event has set its flag.
            // Hit reaction must close it regardless of that timing.
            EndCurrentWeaponDamageWindow();
            ResetAnimatorRequests();
            UpdateAttackRootMotionAvailability(forceDisable: true);
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
            if (currentWeaponInstance != null)
            {
                position = currentWeaponInstance.transform.position;
                rotation = currentWeaponInstance.transform.rotation;
                return true;
            }

            position = default;
            rotation = default;
            return false;
        }

        private void OnWeaponSlotInput(WeaponSlotInputMessage message)
        {
            LogWeaponSlotInput(message.SlotIndex, "Received weapon-slot input");

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

            // A hit reaction may also block the player, but only an active weapon attack
            // is allowed to receive a replacement request while movement is locked.
            if (actionState.IsActionBlocked && !isHitAttackInProgress)
            {
                return;
            }

            if (gameModesController.GameMode != GameMode.Game)
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

            targetLockController?.TryFaceAttackTarget();
            TriggerAttack(message.Button);
        }

        private void OnGameModeChanged(GameModeChangedMessage message)
        {
            if (message.GameMode == GameMode.Game)
            {
                if (!isHitAttackInProgress)
                {
                    playerAnimationController?.SetLocomotionLocked(false);
                }

                return;
            }

            CancelAttackFlow();
        }

        private void OnDodgeInput(DodgeInputMessage _)
        {
            // Dodge and Roll are full-body actions and must remain available while the weapon
            // visual is being drawn or sheathed. In particular, a lesson can pause that visual
            // transition before forwarding the player's first evasion input.
            if (animator == null
             || gameModesController.GameMode != GameMode.Game
             || IsHitAnimationInProgress()
             || (actionState.IsActionBlocked && !isHitAttackInProgress))
            {
                return;
            }

            SpendStamina(GetStamina().DodgeCost);

            CaptureEvasionDirection();

            SetAnimationRequests(
                lightAttackRequested: false,
                heavyAttackRequested: false,
                dodgeRequested: true,
                rollRequested: false);
        }

        private void OnRollInput(RollInputMessage _)
        {
            if (animator == null
             || gameModesController.GameMode != GameMode.Game
             || IsHitAnimationInProgress()
             || (actionState.IsActionBlocked && !isHitAttackInProgress))
            {
                return;
            }

            SpendStamina(GetStamina().RollCost);
            CaptureEvasionDirection();

            SetAnimationRequests(
                lightAttackRequested: false,
                heavyAttackRequested: false,
                dodgeRequested: false,
                rollRequested: true);
        }

        private void RefreshWeaponInHand()
        {
            if (actionState.IsActionBlocked || IsAttackBlockingWeaponChanges())
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
                CancelAttackFlow();
                HandleEmptySelectedSlot();
                return;
            }

            if (!isWeaponDrawn)
            {
                CancelAttackFlow();
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
            if (itemConfig == null || IsAttackBlockingWeaponChanges())
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

            isWeaponAnimationInProgress = true;
            hasPendingRefresh = false;
            hasAttachmentBlendStartedForCurrentAnimation = false;
            currentAnimationKind = WeaponAnimationKind.Draw;
            currentAnimationClipName = DrawWeaponClipName;
            UpdateRunningAvailability();

            if (animator == null)
            {
                FinalizeWeaponRender(itemConfig, slotIndex, WeaponDisplayMode.RightHand, snapToAttachmentTransform: true);
                CompleteWeaponAnimationFromEvent(WeaponAnimationKind.Draw);
                return;
            }

            animator.ResetTrigger(DrawWeaponRequestedParameterHash);
            animator.ResetTrigger(SheatheWeaponRequestedParameterHash);
            animator.SetTrigger(DrawWeaponRequestedParameterHash);
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

            if (!ignoreActiveCombatAction && IsAttackBlockingWeaponChanges())
            {
                return;
            }

            isWeaponAnimationInProgress = true;
            hasEnteredSheatheAnimationState = false;
            hasPendingRefresh = false;
            hasAttachmentBlendStartedForCurrentAnimation = false;
            currentAnimationKind = WeaponAnimationKind.Sheathe;
            currentAnimationClipName = SheatheWeaponClipName;
            UpdateRunningAvailability();

            if (animator == null)
            {
                FinalizeWeaponRender(itemConfig, slotIndex, WeaponDisplayMode.Belt, snapToAttachmentTransform: false);
                PutWeaponOnBeltFromAnimationEvent();
                return;
            }

            animator.ResetTrigger(SheatheWeaponRequestedParameterHash);
            animator.ResetTrigger(DrawWeaponRequestedParameterHash);
            animator.SetTrigger(SheatheWeaponRequestedParameterHash);
        }

        private void TriggerAttack(MouseButtonType button)
        {
            if (animator == null)
            {
                return;
            }

            var isHeavyAttack = button == MouseButtonType.Right;
            SpendStamina(isHeavyAttack ? GetStamina().HeavyAttackCost : GetStamina().LightAttackCost);
            SetAnimationRequests(
                lightAttackRequested: !isHeavyAttack,
                heavyAttackRequested: isHeavyAttack,
                dodgeRequested: false,
                rollRequested: false);
        }

        private void CancelAttackFlow(bool restoreMovement = true)
        {
            playerAnimationController?.ReleaseEvasionDirection();
            DisableDamageImmunityFromAnimationEvent();
            isCombatActionLocked = false;

            if (isHitAttackInProgress)
            {
                isHitAttackInProgress = false;
                EndCurrentWeaponDamageWindow();
                UpdateAttackRootMotionAvailability();
                if (restoreMovement && gameModesController.GameMode == GameMode.Game)
                {
                    playerMovement?.ChangeState(true);
                }

                if (restoreMovement)
                {
                    playerAnimationController?.SetLocomotionLocked(false);
                }
            }

            if (animator == null)
            {
                return;
            }

            ResetAnimationRequests();

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

            isWeaponAnimationInProgress = false;
            hasEnteredSheatheAnimationState = false;
            hasAttachmentBlendStartedForCurrentAnimation = false;
            shouldPreservePoseForCurrentDraw = false;
            currentAnimationKind = WeaponAnimationKind.None;
            currentAnimationClipName = null;
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
            isCombatActionLocked = false;
            ResetAnimationRequests();
            playerAnimationController?.ReleaseEvasionDirection();
            UpdateAttackRootMotionAvailability(forceDisable: true);
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
                hasEnteredSheatheAnimationState = true;
                return;
            }

            if (!hasEnteredSheatheAnimationState || currentWeaponItemConfig == null)
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
            if (animator.GetCurrentAnimatorStateInfo(weaponAnimationLayerIndex).fullPathHash == SheatheWeaponStateHash)
            {
                return true;
            }

            return animator.IsInTransition(weaponAnimationLayerIndex)
                   && animator.GetNextAnimatorStateInfo(weaponAnimationLayerIndex).fullPathHash == SheatheWeaponStateHash;
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

            weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
            DestroyCurrentWeaponInstance();
            currentWeaponItemConfig = null;
            currentRenderedSlotIndex = 0;
            currentDisplayMode = WeaponDisplayMode.None;

            RenderWeapon(selectedItemConfig, selectedWeaponSlotIndex, WeaponDisplayMode.Belt);
            shouldPreservePoseForCurrentDraw = true;
            StartDrawAnimation(selectedWeaponSlotIndex, selectedItemConfig, preserveCurrentVisual: true);
            return true;
        }

        private void MoveCurrentWeaponToBeltPreservingPose()
        {
            if (currentWeaponInstance == null || currentWeaponItemConfig == null)
            {
                return;
            }

            var targetParent = GetTargetParent(WeaponDisplayMode.Belt);
            if (targetParent == null)
            {
                return;
            }

            weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
            currentWeaponInstance.transform.SetParent(targetParent, true);
            currentDisplayMode = WeaponDisplayMode.Belt;
            UpdateCurrentWeaponInstanceName();
            UpdateRunningAvailability();
        }

        private void MoveCurrentWeaponToRightHandPreservingPose()
        {
            if (currentWeaponInstance == null || currentWeaponItemConfig == null)
            {
                return;
            }

            var targetParent = GetTargetParent(WeaponDisplayMode.RightHand);
            if (targetParent == null)
            {
                return;
            }

            weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
            currentWeaponInstance.transform.SetParent(targetParent, true);
            currentDisplayMode = WeaponDisplayMode.RightHand;
            UpdateCurrentWeaponInstanceName();
            UpdateRunningAvailability();
        }

        private bool TryStartNextWeaponDuringSheatheIfBeginEventPassed()
        {
            if (!TryGetAnimationEventNormalizedTime(currentAnimationClipName, BeginMoveWeaponToBeltEventName, out var beginMoveNormalizedTime))
            {
                return false;
            }

            if (animator == null || weaponAnimationLayerIndex < 0)
            {
                return false;
            }

            var stateInfo = animator.GetCurrentAnimatorStateInfo(weaponAnimationLayerIndex);
            if (stateInfo.fullPathHash != SheatheWeaponStateHash || stateInfo.normalizedTime < beginMoveNormalizedTime)
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

        private ItemConfig GetWeaponItemConfigForSlot(int slotIndex)
        {
            var slot = slotIndex == 1
                ? playerInventory.LeftWeaponSlot
                : playerInventory.RightWeaponSlot;
            var itemConfig = slot?.ItemConfig;

            return itemConfig?.ItemType == ItemType.Weapon
                ? itemConfig
                : null;
        }

        private void RenderWeapon(ItemConfig itemConfig, int slotIndex, WeaponDisplayMode displayMode)
        {
            DestroyCurrentWeaponInstance();
            CleanupSpawnedWeaponInstancesExceptCurrent(itemConfig);

            if (itemConfig == null || displayMode == WeaponDisplayMode.None)
            {
                currentWeaponItemConfig = null;
                currentRenderedSlotIndex = 0;
                currentDisplayMode = WeaponDisplayMode.None;
                UpdateRunningAvailability();
                return;
            }

            var weaponPrefab = itemConfig.ItemType == ItemType.Weapon
                ? itemConfig.WeaponInHandPrefab
                : null;
            var targetParent = GetTargetParent(displayMode);
            if (weaponPrefab == null || targetParent == null)
            {
                currentWeaponItemConfig = null;
                currentRenderedSlotIndex = 0;
                currentDisplayMode = WeaponDisplayMode.None;
                UpdateRunningAvailability();
                return;
            }

            currentWeaponItemConfig = itemConfig;
            currentRenderedSlotIndex = slotIndex;
            currentDisplayMode = displayMode;

            currentWeaponInstance = Object.Instantiate(weaponPrefab, targetParent, false);
            UpdateCurrentWeaponInstanceName();

            ApplyAttachmentTransform(currentWeaponInstance.transform, itemConfig, displayMode);

            UpdateRunningAvailability();
        }

        private void FinalizeWeaponRender(
            ItemConfig itemConfig,
            int slotIndex,
            WeaponDisplayMode displayMode,
            bool snapToAttachmentTransform)
        {
            var targetParent = GetTargetParent(displayMode);
            if (currentWeaponInstance != null
             && currentWeaponItemConfig == itemConfig
             && targetParent != null)
            {
                currentWeaponInstance.transform.SetParent(targetParent, !snapToAttachmentTransform);

                if (snapToAttachmentTransform)
                {
                    ApplyAttachmentTransform(currentWeaponInstance.transform, itemConfig, displayMode);
                }

                currentRenderedSlotIndex = slotIndex;
                currentDisplayMode = displayMode;
                UpdateCurrentWeaponInstanceName();
                return;
            }

            RenderWeapon(itemConfig, slotIndex, displayMode);
        }

        private void DestroyCurrentWeaponInstance()
        {
            if (currentWeaponInstance == null)
            {
                return;
            }

            EndCurrentWeaponDamageWindow();
            Object.Destroy(currentWeaponInstance);
            currentWeaponInstance = null;
        }

        private void CleanupSpawnedWeaponInstancesExceptCurrent(ItemConfig expectedItemConfig = null)
        {
            expectedItemConfig ??= currentWeaponItemConfig;
            CleanupSpawnedWeaponInstancesInAnchor(handAnchor?.RightHand, expectedItemConfig);
            CleanupSpawnedWeaponInstancesInAnchor(handAnchor?.Belt, expectedItemConfig);
        }

        private void CleanupSpawnedWeaponInstancesInAnchor(Transform anchor, ItemConfig expectedItemConfig)
        {
            if (anchor == null)
            {
                return;
            }

            for (var index = anchor.childCount - 1; index >= 0; index--)
            {
                var child = anchor.GetChild(index);
                if (child == null || child.gameObject == currentWeaponInstance)
                {
                    continue;
                }

                if (!IsGeneratedWeaponInstance(child.gameObject, expectedItemConfig))
                {
                    continue;
                }

                Object.Destroy(child.gameObject);
            }
        }

        private static bool IsGeneratedWeaponInstance(GameObject candidate, ItemConfig expectedItemConfig)
        {
            if (candidate == null)
            {
                return false;
            }

            var prefabName = expectedItemConfig?.WeaponInHandPrefab != null
                ? expectedItemConfig.WeaponInHandPrefab.name
                : null;
            return !string.IsNullOrWhiteSpace(prefabName) && candidate.name.StartsWith(prefabName, StringComparison.Ordinal);
        }

        private void UpdateCurrentWeaponInstanceName()
        {
            if (currentWeaponInstance != null && currentWeaponItemConfig?.WeaponInHandPrefab != null)
            {
                currentWeaponInstance.name = $"{currentWeaponItemConfig.WeaponInHandPrefab.name} | {currentDisplayMode}";
            }
        }

        private void BeginCurrentWeaponDamageWindow()
        {
            if (activeDamageZone != null)
            {
                return;
            }

            if (currentWeaponInstance == null || currentWeaponItemConfig == null)
            {
                return;
            }

            var weapon = currentWeaponInstance.GetComponentInChildren<Weapon>(true);
            var damageZone = weapon != null ? weapon.DamageZone : null;
            if (damageZone == null)
            {
                return;
            }

            activeDamageZone = damageZone;
            activeDamageZone.BeginDamageWindow(
                ownerDamageReceiver,
                currentWeaponItemConfig,
                IsHeavyAttackDamageAnimationActive());
        }

        private void EndCurrentWeaponDamageWindow()
        {
            if (activeDamageZone == null)
            {
                return;
            }

            activeDamageZone.EndDamageWindow();
            activeDamageZone = null;
        }

        private Transform GetTargetParent(WeaponDisplayMode displayMode)
        {
            return displayMode switch
            {
                WeaponDisplayMode.RightHand => handAnchor?.RightHand,
                WeaponDisplayMode.Belt => handAnchor?.Belt,
                _ => null
            };
        }

        private void StartWeaponAttachmentBlend(
            WeaponDisplayMode targetMode,
            string startEventName,
            string finishEventName)
        {
            if (currentWeaponInstance == null || currentWeaponItemConfig == null)
            {
                return;
            }

            var targetParent = GetTargetParent(targetMode);
            var attachment = GetAttachmentTransformData(currentWeaponItemConfig, targetMode);
            if (targetParent == null || attachment == null)
            {
                return;
            }

            var weaponTransform = currentWeaponInstance.transform;
            weaponTransform.SetParent(targetParent, true);
            currentDisplayMode = targetMode;
            UpdateCurrentWeaponInstanceName();

            if (targetMode == WeaponDisplayMode.Belt)
            {
                return;
            }

            if (TryGetAnimationEventWindowNormalized(
                    currentAnimationClipName,
                    startEventName,
                    finishEventName,
                    out var startNormalizedTime,
                    out var finishNormalizedTime))
            {
                StartTransformBlendByNormalizedTime(
                    weaponTransform,
                    attachment.LocalPosition,
                    Quaternion.Euler(attachment.LocalEulerAngles),
                    startNormalizedTime,
                    finishNormalizedTime);
                return;
            }

            StartTransformBlendByDuration(
                weaponTransform,
                attachment.LocalPosition,
                Quaternion.Euler(attachment.LocalEulerAngles),
                FallbackAttachmentBlendDuration);
        }

        private void StartTransformBlendByDuration(
            Transform targetTransform,
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            float duration)
        {
            weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;

            if (targetTransform == null)
            {
                return;
            }

            if (duration <= 0f)
            {
                targetTransform.localPosition = targetLocalPosition;
                targetTransform.localRotation = targetLocalRotation;
                return;
            }

            var startLocalPosition = targetTransform.localPosition;
            var startLocalRotation = targetTransform.localRotation;
            var elapsed = 0f;

            weaponAttachmentBlendDisposable.Disposable = Observable.EveryUpdate()
                .ObserveOnMainThread()
                .Subscribe(_ =>
                {
                    if (targetTransform == null)
                    {
                        weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
                        return;
                    }

                    elapsed += Time.deltaTime;
                    var progress = Mathf.Clamp01(elapsed / duration);

                    targetTransform.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, progress);
                    targetTransform.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, progress);

                    if (progress < 1f)
                    {
                        return;
                    }

                    weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
                });
        }

        private void StartTransformBlendByNormalizedTime(
            Transform targetTransform,
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            float startNormalizedTime,
            float finishNormalizedTime)
        {
            weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;

            if (targetTransform == null)
            {
                return;
            }

            var startLocalPosition = targetTransform.localPosition;
            var startLocalRotation = targetTransform.localRotation;
            var targetStateHash = GetCurrentAnimationStateHash();

            weaponAttachmentBlendDisposable.Disposable = Observable.EveryUpdate()
                .ObserveOnMainThread()
                .Subscribe(_ =>
                {
                    if (targetTransform == null || animator == null || weaponAnimationLayerIndex < 0)
                    {
                        weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
                        return;
                    }

                    var stateInfo = animator.GetCurrentAnimatorStateInfo(weaponAnimationLayerIndex);
                    if (stateInfo.fullPathHash != targetStateHash)
                    {
                        weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
                        return;
                    }

                    var progress = Mathf.InverseLerp(startNormalizedTime, finishNormalizedTime, stateInfo.normalizedTime);
                    targetTransform.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, progress);
                    targetTransform.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, progress);

                    if (progress < 1f)
                    {
                        return;
                    }

                    weaponAttachmentBlendDisposable.Disposable = Disposable.Empty;
                });
        }

        private bool TryGetAnimationEventWindowNormalized(
            string clipName,
            string startEventName,
            string finishEventName,
            out float startNormalizedTime,
            out float finishNormalizedTime)
        {
            var clip = GetAnimationClip(clipName);
            if (clip == null)
            {
                startNormalizedTime = 0f;
                finishNormalizedTime = 0f;
                return false;
            }

            float? startTime = null;
            float? finishTime = null;

            foreach (var animationEvent in clip.events)
            {
                if (!startTime.HasValue && animationEvent.functionName == startEventName)
                {
                    startTime = animationEvent.time;
                }

                if (!finishTime.HasValue && animationEvent.functionName == finishEventName)
                {
                    finishTime = animationEvent.time;
                }
            }

            if (!startTime.HasValue || !finishTime.HasValue || finishTime.Value <= startTime.Value || clip.length <= 0f)
            {
                startNormalizedTime = 0f;
                finishNormalizedTime = 0f;
                return false;
            }

            startNormalizedTime = startTime.Value / clip.length;
            finishNormalizedTime = finishTime.Value / clip.length;
            return true;
        }

        private bool TryGetAnimationEventNormalizedTime(
            string clipName,
            string eventName,
            out float normalizedTime)
        {
            var clip = GetAnimationClip(clipName);
            if (clip == null || clip.length <= 0f)
            {
                normalizedTime = 0f;
                return false;
            }

            foreach (var animationEvent in clip.events)
            {
                if (animationEvent.functionName != eventName)
                {
                    continue;
                }

                normalizedTime = animationEvent.time / clip.length;
                return true;
            }

            normalizedTime = 0f;
            return false;
        }

        private int GetCurrentAnimationStateHash()
        {
            return currentAnimationKind switch
            {
                WeaponAnimationKind.Draw => DrawWeaponStateHash,
                WeaponAnimationKind.Sheathe => SheatheWeaponStateHash,
                _ => 0
            };
        }

        private AnimationClip GetAnimationClip(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName) || animator?.runtimeAnimatorController == null)
            {
                return null;
            }

            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == clipName)
                {
                    return clip;
                }
            }

            return null;
        }

        private void ResetAnimatorRequests()
        {
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(DrawWeaponRequestedParameterHash);
            animator.ResetTrigger(SheatheWeaponRequestedParameterHash);
            ResetAnimationRequests();
        }

        private void ResetAnimationRequests()
        {
            SetAnimationRequests(
                lightAttackRequested: false,
                heavyAttackRequested: false,
                dodgeRequested: false,
                rollRequested: false);
        }

        private void SetAnimationRequests(
            bool lightAttackRequested,
            bool heavyAttackRequested,
            bool dodgeRequested,
            bool rollRequested)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(AttackRequestedParameterHash, lightAttackRequested);
            animator.SetBool(HeavyAttackRequestedParameterHash, heavyAttackRequested);
            animator.SetBool(DodgeRequestedParameterHash, dodgeRequested);
            animator.SetBool(RollRequestedParameterHash, rollRequested);
        }

        private void UpdateRunningAvailability()
        {
            var shouldAllowRunning =
                !IsAttackRootMotionStateActive()
             && currentAnimationKind != WeaponAnimationKind.Draw
             && (currentDisplayMode != WeaponDisplayMode.RightHand || currentWeaponItemConfig == null);

            playerMovement?.SetRunAllowed(shouldAllowRunning);
        }

        private void UpdateAttackRootMotionAvailability(bool forceDisable = false)
        {
            if (animator == null)
            {
                return;
            }

            var isRootMotionActive = !forceDisable && IsAttackRootMotionStateActive();
            var positionMultiplier = isRootMotionActive
                ? GetEvasionRootMotionMultiplier()
                : 1f;
            rootMotionController?.SetRootMotionActive(this, isRootMotionActive, positionMultiplier);
        }

        private bool IsAttackRootMotionStateActive()
        {
            if (animator == null || attackLayerIndex < 0)
            {
                return false;
            }

            if (IsAttackState(animator.GetCurrentAnimatorStateInfo(attackLayerIndex)))
            {
                return true;
            }

            return animator.IsInTransition(attackLayerIndex)
                && IsAttackState(animator.GetNextAnimatorStateInfo(attackLayerIndex));
        }

        private bool IsAttackBlockingWeaponChanges()
        {
            return isHitAttackInProgress || IsCombatActionAnimationActive();
        }

        private bool IsCombatActionAnimationActive()
        {
            return IsAttackRootMotionStateActive() || IsAttackRequested();
        }

        private bool IsAttackRequested()
        {
            return animator != null
                && (animator.GetBool(AttackRequestedParameterHash)
                 || animator.GetBool(HeavyAttackRequestedParameterHash)
                 || animator.GetBool(DodgeRequestedParameterHash)
                 || animator.GetBool(RollRequestedParameterHash));
        }

        private bool IsEvasionInProgress()
        {
            return IsDodgeInProgress() || IsRollInProgress();
        }

        private bool IsDodgeInProgress()
        {
            if (animator == null || attackLayerIndex < 0)
            {
                return false;
            }

            // The request bool is true only until the entry event resets it. Once the dodge
            // begins, the state check still identifies it for root-motion handling; it does
            // not block later combat requests from being buffered in the Animator.
            if (animator.GetBool(DodgeRequestedParameterHash)
                || animator.GetCurrentAnimatorStateInfo(attackLayerIndex).shortNameHash == DodgeStateShortNameHash)
            {
                return true;
            }

            return animator.IsInTransition(attackLayerIndex)
                && animator.GetNextAnimatorStateInfo(attackLayerIndex).shortNameHash == DodgeStateShortNameHash;
        }

        private bool IsRollInProgress()
        {
            if (animator == null || attackLayerIndex < 0)
            {
                return false;
            }

            if (animator.GetBool(RollRequestedParameterHash)
                || animator.GetCurrentAnimatorStateInfo(attackLayerIndex).shortNameHash == RollStateShortNameHash)
            {
                return true;
            }

            return animator.IsInTransition(attackLayerIndex)
                && animator.GetNextAnimatorStateInfo(attackLayerIndex).shortNameHash == RollStateShortNameHash;
        }

        private float GetEvasionRootMotionMultiplier()
        {
            if (IsRollInProgress())
            {
                return playerMovementConfig.RollRootMotionMultiplier;
            }

            return IsDodgeInProgress()
                ? playerMovementConfig.DodgeRootMotionMultiplier
                : 1f;
        }

        private void CaptureEvasionDirection()
        {
            // Both evasion blend trees select a directional clip from DirectionX/DirectionY.
            // This must happen before their time-zero LockMovement event clears locomotion.
            playerAnimationController?.CaptureEvasionDirection();
        }

        private bool IsHitAnimationInProgress()
        {
            if (animator == null || attackLayerIndex < 0)
            {
                return false;
            }

            if (IsHitState(animator.GetCurrentAnimatorStateInfo(attackLayerIndex)))
            {
                return true;
            }

            return animator.IsInTransition(attackLayerIndex)
                && IsHitState(animator.GetNextAnimatorStateInfo(attackLayerIndex));
        }

        private static bool IsHitState(AnimatorStateInfo stateInfo)
        {
            var stateHash = stateInfo.shortNameHash;
            return stateHash == HitBackStateShortNameHash
                || stateHash == HitFrontStateShortNameHash
                || stateHash == HitRightStateShortNameHash
                || stateHash == HitLeftStateShortNameHash
                || stateHash == AttackComboBHitStateShortNameHash
                || stateHash == AttackHitStateShortNameHash
                || stateHash == HeavyAttackHitRootMotionStateShortNameHash
                || stateHash == HeavyAttackHitStateShortNameHash;
        }

        private bool IsHeavyAttackDamageAnimationActive()
        {
            if (animator == null || attackLayerIndex < 0)
            {
                return false;
            }

            if (IsHeavyAttackHitState(animator.GetCurrentAnimatorStateInfo(attackLayerIndex)))
            {
                return true;
            }

            return animator.IsInTransition(attackLayerIndex)
                && IsHeavyAttackHitState(animator.GetNextAnimatorStateInfo(attackLayerIndex));
        }

        private static bool IsHeavyAttackHitState(AnimatorStateInfo stateInfo)
        {
            var stateHash = stateInfo.shortNameHash;
            return stateHash == HeavyAttackHitRootMotionStateShortNameHash
                || stateHash == HeavyAttackHitStateShortNameHash;
        }

        private Stamina GetStamina()
        {
            return (Stamina)statsController.GetStat(StatType.Stamina);
        }

        private void SpendStamina(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            statsController.AddValue(StatType.Stamina, -amount, StatChangeSource.Combat);
        }

        private static bool IsAttackState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.shortNameHash != 0
                && stateInfo.shortNameHash != EmptyIdleStateShortNameHash;
        }

        private static WeaponAttachmentTransformData GetAttachmentTransformData(ItemConfig itemConfig, WeaponDisplayMode displayMode)
        {
            if (itemConfig == null)
            {
                return null;
            }

            return displayMode == WeaponDisplayMode.RightHand
                ? itemConfig.RightHandWeaponAttachment
                : itemConfig.BeltWeaponAttachment;
        }

        private static void ApplyAttachmentTransform(Transform targetTransform, ItemConfig itemConfig, WeaponDisplayMode displayMode)
        {
            if (targetTransform == null || itemConfig == null)
            {
                return;
            }

            var attachment = GetAttachmentTransformData(itemConfig, displayMode);
            if (attachment == null)
            {
                return;
            }

            targetTransform.localPosition = attachment.LocalPosition;
            targetTransform.localRotation = Quaternion.Euler(attachment.LocalEulerAngles);
        }
    }
}
