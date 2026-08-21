using System;
using Combat;
using Inventory;
using Inventory.Inventories;
using Inventory.Item;
using Movement;
using Stats;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace NPC
{
    public sealed class NpcWeaponInHandController : IWeaponAnimationEventHandler, IEquippedWeaponVisual, IStartable, ITickable, IDisposable
    {
        private const string WeaponAnimationLayerName = "Weapon Handling";
        private const string SheatheWeaponStatePath = "Weapon Handling.SheatheWeapon";

        private static readonly int DrawWeaponRequestedParameterHash = Animator.StringToHash("MoveWeaponInHand");
        private static readonly int SheatheWeaponRequestedParameterHash = Animator.StringToHash("MoveWeaponInBelt");
        private static readonly int AttackRequestedParameterHash = Animator.StringToHash("Attack");
        private static readonly int HeavyAttackRequestedParameterHash = Animator.StringToHash("HeavyAttack");
        private static readonly int DodgeRequestedParameterHash = Animator.StringToHash("Dodge");
        private static readonly int RollRequestedParameterHash = Animator.StringToHash("Roll");
        private static readonly int DirectionXParameterHash = Animator.StringToHash("DirectionX");
        private static readonly int DirectionYParameterHash = Animator.StringToHash("DirectionY");
        private static readonly int EmptyStateHash = Animator.StringToHash("Empty Idle");
        private static readonly int HeavyAttackHitRootMotionStateHash = Animator.StringToHash("A_Attack_HeavyCombo01A_Hit_RootMotion_Sword");
        private static readonly int HeavyAttackHitStateHash = Animator.StringToHash("A_Attack_HeavyCombo01B_Hit_Sword");
        private static readonly int DodgeStateHash = Animator.StringToHash("Dodge Tree");
        private static readonly int RollStateHash = Animator.StringToHash("Dodge RollTree");
        private static readonly int SheatheWeaponStateHash = Animator.StringToHash(SheatheWeaponStatePath);

        private enum WeaponDisplayMode
        {
            None,
            RightHand,
            Belt
        }

        private readonly PlayerInventory inventory;
        private readonly PlayerWeaponHandAnchor handAnchor;
        private readonly PlayerWeaponAnimationEventReceiver animationEventReceiver;
        private readonly Animator animator;
        private readonly CharacterDamageReceiver ownerDamageReceiver;
        private readonly CharacterActionState actionState;
        private readonly CharacterRootMotionController rootMotionController;
        private readonly NpcNavMeshController navMeshController;
        private readonly PlayerMovementConfig movementConfig;
        private readonly StatsController statsController;
        private readonly MessagePipe.IPublisher<Messages.NpcAttackStartedMessage> attackStartedPublisher;
        private readonly MessagePipe.IPublisher<Messages.WeaponSheathedMessage> weaponSheathedPublisher;
        private readonly CompositeDisposable disposables = new();

        private GameObject currentWeaponInstance;
        private ItemConfig currentWeaponItemConfig;
        private WeaponDamageZone activeDamageZone;
        private bool isWeaponDrawn;
        private bool isSheatheAnimationInProgress;
        private bool hasEnteredSheatheAnimationState;
        private bool hasAttackComboWindow;
        private bool isAttackInProgress;
        private bool isAttackStartNotificationPending;
        private bool isEvasionDirectionLocked;
        private int currentRenderedSlotIndex;
        private WeaponDisplayMode currentDisplayMode;
        private readonly int fullBodyLayerIndex;
        private readonly int weaponAnimationLayerIndex;

        public NpcWeaponInHandController(
            PlayerInventory inventory,
            PlayerWeaponHandAnchor handAnchor,
            PlayerWeaponAnimationEventReceiver animationEventReceiver,
            Animator animator,
            CharacterDamageReceiver ownerDamageReceiver,
            CharacterActionState actionState,
            CharacterRootMotionController rootMotionController,
            NpcNavMeshController navMeshController,
            PlayerMovementConfig movementConfig,
            StatsController statsController,
            MessagePipe.IPublisher<Messages.NpcAttackStartedMessage> attackStartedPublisher,
            MessagePipe.IPublisher<Messages.WeaponSheathedMessage> weaponSheathedPublisher)
        {
            this.inventory = inventory;
            this.handAnchor = handAnchor;
            this.animationEventReceiver = animationEventReceiver;
            this.animator = animator;
            this.ownerDamageReceiver = ownerDamageReceiver;
            this.actionState = actionState;
            this.rootMotionController = rootMotionController;
            this.navMeshController = navMeshController;
            this.movementConfig = movementConfig;
            this.statsController = statsController;
            this.attackStartedPublisher = attackStartedPublisher;
            this.weaponSheathedPublisher = weaponSheathedPublisher;
            fullBodyLayerIndex = animator != null ? animator.GetLayerIndex("Full Body") : -1;
            weaponAnimationLayerIndex = animator != null ? animator.GetLayerIndex(WeaponAnimationLayerName) : -1;

            animationEventReceiver?.Bind(this);
        }

        public void Start()
        {
            inventory.Changed.Subscribe(_ => RefreshWeaponInHand()).AddTo(disposables);
            RefreshWeaponInHand();
            UpdateRootMotionAvailability();
        }

        public void Tick()
        {
            UpdateRootMotionAvailability();
            UpdateAttackProgress();
            ReleaseEvasionDirectionWhenFinished();
            SynchronizeSheatheCompletionWithAnimatorState();
        }

        public void Dispose()
        {
            navMeshController?.SetActionMovementLocked(false);
            ReleaseEvasionDirection();
            rootMotionController?.SetRootMotionActive(this, false);
            EndCurrentWeaponDamageWindow();
            DestroyCurrentWeaponInstance();
            disposables.Dispose();
        }

        public void TakeWeaponInHandFromAnimationEvent() => MoveCurrentWeapon(WeaponDisplayMode.RightHand);
        public void BeginMoveWeaponToRightHandFromAnimationEvent() => MoveCurrentWeapon(WeaponDisplayMode.RightHand);
        public void PutWeaponOnBeltFromAnimationEvent()
        {
            MoveCurrentWeapon(WeaponDisplayMode.Belt);
            CompleteSheatheAnimation();
            weaponSheathedPublisher?.Publish(new Messages.WeaponSheathedMessage(ownerDamageReceiver?.OwnerTransform));
        }
        public void BeginMoveWeaponToBeltFromAnimationEvent() => MoveCurrentWeapon(WeaponDisplayMode.Belt);
        public void HoldAttackReadyFromAnimationEvent() => hasAttackComboWindow = true;
        public void AttackStartedFromAnimationEvent() => BeginAttack();
        public void BeginDamageWindowFromAnimationEvent() => BeginCurrentWeaponDamageWindow();
        public void EndDamageWindowFromAnimationEvent() => EndCurrentWeaponDamageWindow();
        public void EnableDamageImmunityFromAnimationEvent() => ownerDamageReceiver?.SetWeaponDamageBlocked(true);
        public void DisableDamageImmunityFromAnimationEvent() => ownerDamageReceiver?.SetWeaponDamageBlocked(false);
        public void LockMovementFromAnimationEvent()
        {
            actionState?.SetActionBlocked(true);
            navMeshController?.SetActionMovementLocked(true);
        }

        public void UnlockMovementFromAnimationEvent()
        {
            actionState?.SetActionBlocked(false);
            navMeshController?.SetActionMovementLocked(false);
            ReleaseEvasionDirection();
        }

        public void AttackFinishedFromAnimationEvent()
        {
            isAttackStartNotificationPending = false;
            isAttackInProgress = false;
            EndCurrentWeaponDamageWindow();
            actionState?.SetActionBlocked(false);
            navMeshController?.SetActionMovementLocked(false);
            ReleaseEvasionDirection();
        }

        public void ResetAttackRequestFromAnimationEvent()
        {
            // All equipped attack clips reset their Animator request at time zero. They do not
            // all expose the optional AttackStarted event, so this is the reliable clip-owned
            // boundary for announcing that an accepted NPC attack has actually begun.
            if (isAttackStartNotificationPending)
            {
                BeginAttack();
            }

            ClearAttackRequest();
        }

        public bool HasWeaponInWeaponSlots =>
            inventory?.LeftWeaponSlot?.ItemConfig?.ItemType == ItemType.Weapon
         || inventory?.RightWeaponSlot?.ItemConfig?.ItemType == ItemType.Weapon;

        public bool IsWeaponDrawn => isWeaponDrawn && currentDisplayMode == WeaponDisplayMode.RightHand && currentWeaponItemConfig != null;
        public bool IsWeaponSheathed => !isSheatheAnimationInProgress
                                       && !isWeaponDrawn
                                       && currentDisplayMode != WeaponDisplayMode.RightHand;
        public bool IsAttackInProgress => isAttackInProgress;
        public bool CanStartWeaponSheathing => IsWeaponSheathed
                                              || (!isSheatheAnimationInProgress
                                                  && !IsFullBodyActionAnimationActive());

        public bool RequestDrawWeapon()
        {
            if (!HasWeaponInWeaponSlots)
            {
                return false;
            }

            if (isWeaponDrawn)
            {
                return true;
            }

            isWeaponDrawn = true;
            CompleteSheatheAnimation();
            if (animator == null || weaponAnimationLayerIndex < 0)
            {
                // An NPC without the Weapon Handling layer cannot receive the clip-owned handoff.
                // Keep the immediate visual fallback for incomplete Animator setups.
                RefreshWeaponInHand();
                return true;
            }

            // The draw clip owns the transfer from belt to hand through its animation events.
            animator.ResetTrigger(SheatheWeaponRequestedParameterHash);
            animator.SetTrigger(DrawWeaponRequestedParameterHash);
            return true;
        }

        public void RequestSheatheWeapon()
        {
            if (!isWeaponDrawn && currentDisplayMode != WeaponDisplayMode.RightHand)
            {
                return;
            }

            bool requiresSheatheAnimation = isWeaponDrawn || currentDisplayMode == WeaponDisplayMode.RightHand;
            isWeaponDrawn = false;
            isSheatheAnimationInProgress = requiresSheatheAnimation
                                         && animator != null
                                         && weaponAnimationLayerIndex >= 0;
            hasEnteredSheatheAnimationState = false;
            if (isSheatheAnimationInProgress)
            {
                animator.ResetTrigger(DrawWeaponRequestedParameterHash);
                animator.SetTrigger(SheatheWeaponRequestedParameterHash);
                // Do not refresh here: RefreshWeaponInHand derives Belt from isWeaponDrawn and
                // would move the visual before BeginMoveWeaponToBelt/PutWeaponOnBelt.
                return;
            }

            // Preserve the non-animated fallback when the Animator or its layer is unavailable.
            RefreshWeaponInHand();
        }

        private void SynchronizeSheatheCompletionWithAnimatorState()
        {
            if (!isSheatheAnimationInProgress)
            {
                return;
            }

            if (IsSheatheWeaponAnimationStateActive())
            {
                hasEnteredSheatheAnimationState = true;
                return;
            }

            if (hasEnteredSheatheAnimationState)
            {
                // If an animation event was skipped by an interrupted transition, commit the
                // same final visual state before exposing this NPC as sheathed to session logic.
                MoveCurrentWeapon(WeaponDisplayMode.Belt);
                CompleteSheatheAnimation();
                weaponSheathedPublisher?.Publish(new Messages.WeaponSheathedMessage(ownerDamageReceiver?.OwnerTransform));
            }
        }

        private bool IsSheatheWeaponAnimationStateActive()
        {
            if (animator == null || weaponAnimationLayerIndex < 0)
            {
                return false;
            }

            if (animator.GetCurrentAnimatorStateInfo(weaponAnimationLayerIndex).fullPathHash == SheatheWeaponStateHash)
            {
                return true;
            }

            return animator.IsInTransition(weaponAnimationLayerIndex)
                   && animator.GetNextAnimatorStateInfo(weaponAnimationLayerIndex).fullPathHash == SheatheWeaponStateHash;
        }

        private void CompleteSheatheAnimation()
        {
            isSheatheAnimationInProgress = false;
            hasEnteredSheatheAnimationState = false;
        }

        public bool RequestAttack()
        {
            if (!IsWeaponDrawn || actionState?.IsActionBlocked == true)
            {
                return false;
            }

            SpendStamina(GetStaminaCost(stamina => stamina.LightAttackCost));
            QueueAttackStartNotification();
            SetActionRequests(lightAttackRequested: true, heavyAttackRequested: false, dodgeRequested: false, rollRequested: false);
            return true;
        }

        public bool RequestHeavyAttack()
        {
            // The player's Animator accepts a heavy attack during the same buffered combo window
            // as a light attack. NPCs use that very window too, rather than inventing a separate
            // combo system on top of the Animator.
            if (!IsWeaponDrawn)
            {
                return false;
            }

            SpendStamina(GetStaminaCost(stamina => stamina.HeavyAttackCost));
            QueueAttackStartNotification();
            SetActionRequests(lightAttackRequested: false, heavyAttackRequested: true, dodgeRequested: false, rollRequested: false);
            return true;
        }

        public bool RequestComboAttack()
        {
            if (!IsWeaponDrawn)
            {
                return false;
            }

            SpendStamina(GetStaminaCost(stamina => stamina.LightAttackCost));
            QueueAttackStartNotification();
            SetActionRequests(lightAttackRequested: true, heavyAttackRequested: false, dodgeRequested: false, rollRequested: false);
            return true;
        }

        public bool RequestDodge(Vector3 worldDirection)
        {
            if (actionState?.IsActionBlocked == true || animator == null)
            {
                return false;
            }

            SpendStamina(GetStaminaCost(stamina => stamina.DodgeCost));
            SetEvasionDirection(worldDirection);
            SetActionRequests(lightAttackRequested: false, heavyAttackRequested: false, dodgeRequested: true, rollRequested: false);
            return true;
        }

        public bool RequestRoll(Vector3 worldDirection)
        {
            if (actionState?.IsActionBlocked == true || animator == null)
            {
                return false;
            }

            SpendStamina(GetStaminaCost(stamina => stamina.RollCost));
            SetEvasionDirection(worldDirection);
            SetActionRequests(lightAttackRequested: false, heavyAttackRequested: false, dodgeRequested: false, rollRequested: true);
            return true;
        }

        public float StaminaNormalized
        {
            get
            {
                var stamina = GetStamina();
                return stamina == null || stamina.Max <= Mathf.Epsilon
                    ? 0f
                    : Mathf.Clamp01(stamina.Value.Value / stamina.Max);
            }
        }

        public bool CanRequestEvasion => animator != null && actionState?.IsActionBlocked != true;

        public bool ConsumeAttackComboWindow()
        {
            if (!hasAttackComboWindow)
            {
                return false;
            }

            hasAttackComboWindow = false;
            return true;
        }

        public void ClearAttackRequest()
        {
            hasAttackComboWindow = false;
            isAttackStartNotificationPending = false;
            SetActionRequests(lightAttackRequested: false, heavyAttackRequested: false, dodgeRequested: false, rollRequested: false);
        }

        public void InterruptByHitReaction()
        {
            EndCurrentWeaponDamageWindow();
            hasAttackComboWindow = false;
            actionState?.SetActionBlocked(false);
            navMeshController?.SetActionMovementLocked(false);
            ReleaseEvasionDirection();
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(DrawWeaponRequestedParameterHash);
            animator.ResetTrigger(SheatheWeaponRequestedParameterHash);
            ClearAttackRequest();
        }

        public bool TryGetCurrentWeaponSlot(out Inventory.Slot.SlotModel slot)
        {
            slot = currentRenderedSlotIndex switch
            {
                1 => inventory.LeftWeaponSlot,
                2 => inventory.RightWeaponSlot,
                _ => null
            };

            if (slot?.ItemConfig?.ItemType == ItemType.Weapon)
            {
                return true;
            }

            if (inventory.LeftWeaponSlot?.ItemConfig?.ItemType == ItemType.Weapon)
            {
                slot = inventory.LeftWeaponSlot;
                return true;
            }

            if (inventory.RightWeaponSlot?.ItemConfig?.ItemType == ItemType.Weapon)
            {
                slot = inventory.RightWeaponSlot;
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

        private void RefreshWeaponInHand()
        {
            var selectedWeapon = GetSelectedWeaponItemConfig(out var selectedSlotIndex);
            var displayMode = selectedWeapon == null
                ? WeaponDisplayMode.None
                : isWeaponDrawn ? WeaponDisplayMode.RightHand : WeaponDisplayMode.Belt;

            if (selectedWeapon == currentWeaponItemConfig
             && currentRenderedSlotIndex == selectedSlotIndex
             && currentDisplayMode == displayMode
             && currentWeaponInstance != null)
            {
                return;
            }

            RenderWeapon(selectedWeapon, selectedSlotIndex, displayMode);
        }

        private ItemConfig GetSelectedWeaponItemConfig(out int slotIndex)
        {
            var leftWeapon = inventory.LeftWeaponSlot?.ItemConfig;
            if (leftWeapon != null && leftWeapon.ItemType == ItemType.Weapon)
            {
                slotIndex = 1;
                return leftWeapon;
            }

            var rightWeapon = inventory.RightWeaponSlot?.ItemConfig;
            if (rightWeapon != null && rightWeapon.ItemType == ItemType.Weapon)
            {
                slotIndex = 2;
                return rightWeapon;
            }

            slotIndex = 0;
            return null;
        }

        private void RenderWeapon(ItemConfig itemConfig, int slotIndex, WeaponDisplayMode displayMode)
        {
            DestroyCurrentWeaponInstance();
            var targetParent = GetTargetParent(displayMode);
            if (itemConfig == null || itemConfig.WeaponInHandPrefab == null || targetParent == null || displayMode == WeaponDisplayMode.None)
            {
                currentWeaponItemConfig = null;
                currentRenderedSlotIndex = 0;
                currentDisplayMode = WeaponDisplayMode.None;
                return;
            }

            currentWeaponItemConfig = itemConfig;
            currentRenderedSlotIndex = slotIndex;
            currentDisplayMode = displayMode;
            currentWeaponInstance = Object.Instantiate(itemConfig.WeaponInHandPrefab, targetParent, false);
            currentWeaponInstance.name = $"{itemConfig.WeaponInHandPrefab.name} | NPC {displayMode}";
            ApplyAttachmentTransform(currentWeaponInstance.transform, itemConfig, displayMode);
        }

        private void DestroyCurrentWeaponInstance()
        {
            EndCurrentWeaponDamageWindow();
            if (currentWeaponInstance == null)
            {
                return;
            }

            Object.Destroy(currentWeaponInstance);
            currentWeaponInstance = null;
            currentRenderedSlotIndex = 0;
            currentDisplayMode = WeaponDisplayMode.None;
        }

        private void BeginCurrentWeaponDamageWindow()
        {
            if (activeDamageZone != null || currentWeaponInstance == null || currentWeaponItemConfig == null)
            {
                return;
            }

            var weapon = currentWeaponInstance.GetComponentInChildren<Weapon>(true);
            activeDamageZone = weapon != null ? weapon.DamageZone : null;
            activeDamageZone?.BeginDamageWindow(ownerDamageReceiver, currentWeaponItemConfig, IsHeavyAttackDamageAnimationActive());
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

        private void MoveCurrentWeapon(WeaponDisplayMode displayMode)
        {
            if (currentWeaponItemConfig == null || currentWeaponInstance == null)
            {
                RefreshWeaponInHand();
                return;
            }

            var targetParent = GetTargetParent(displayMode);
            if (targetParent == null)
            {
                return;
            }

            currentDisplayMode = displayMode;
            currentWeaponInstance.transform.SetParent(targetParent, false);
            currentWeaponInstance.name = $"{currentWeaponItemConfig.WeaponInHandPrefab.name} | NPC {displayMode}";
            ApplyAttachmentTransform(currentWeaponInstance.transform, currentWeaponItemConfig, displayMode);
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

        private static void ApplyAttachmentTransform(Transform targetTransform, ItemConfig itemConfig, WeaponDisplayMode displayMode)
        {
            if (targetTransform == null || itemConfig == null)
            {
                return;
            }

            var attachment = displayMode == WeaponDisplayMode.RightHand
                ? itemConfig.RightHandWeaponAttachment
                : itemConfig.BeltWeaponAttachment;
            if (attachment == null)
            {
                return;
            }

            targetTransform.localPosition = attachment.LocalPosition;
            targetTransform.localRotation = Quaternion.Euler(attachment.LocalEulerAngles);
        }

        private void SetActionRequests(bool lightAttackRequested, bool heavyAttackRequested, bool dodgeRequested, bool rollRequested)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(AttackRequestedParameterHash, lightAttackRequested);
            animator.SetBool(HeavyAttackRequestedParameterHash, heavyAttackRequested);
            animator.SetBool(DodgeRequestedParameterHash, dodgeRequested);
            animator.SetBool(RollRequestedParameterHash, rollRequested);
            // The player enables root motion as soon as its action is requested. Do the same
            // for NPCs so the first displacement frame of Dodge/Roll is never discarded.
            UpdateRootMotionAvailability();
        }

        private void QueueAttackStartNotification()
        {
            if (!isAttackInProgress)
            {
                isAttackStartNotificationPending = true;
            }
        }

        private void UpdateAttackProgress()
        {
            var isActionAnimationActive = IsFullBodyActionAnimationActive();
            if (isAttackInProgress && !isActionAnimationActive)
            {
                isAttackInProgress = false;
            }
        }

        private void BeginAttack()
        {
            isAttackStartNotificationPending = false;
            if (isAttackInProgress)
            {
                return;
            }

            isAttackInProgress = true;
            actionState?.SetActionBlocked(true);
            navMeshController?.SetActionMovementLocked(true);
            attackStartedPublisher?.Publish(new Messages.NpcAttackStartedMessage(ownerDamageReceiver?.OwnerTransform));
        }

        private bool IsFullBodyActionAnimationActive()
        {
            if (animator == null || fullBodyLayerIndex < 0)
            {
                return false;
            }

            if (IsFullBodyActionState(animator.GetCurrentAnimatorStateInfo(fullBodyLayerIndex)))
            {
                return true;
            }

            return animator.IsInTransition(fullBodyLayerIndex)
                   && IsFullBodyActionState(animator.GetNextAnimatorStateInfo(fullBodyLayerIndex));
        }

        private void SetEvasionDirection(Vector3 worldDirection)
        {
            if (animator == null)
            {
                return;
            }

            isEvasionDirectionLocked = true;
            if (navMeshController != null)
            {
                navMeshController.LockEvasionDirection(worldDirection);
                return;
            }

            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                worldDirection = -animator.transform.forward;
            }

            var localDirection = animator.transform.InverseTransformDirection(worldDirection.normalized);
            animator.SetFloat(DirectionXParameterHash, Mathf.Clamp(localDirection.x, -1f, 1f));
            animator.SetFloat(DirectionYParameterHash, Mathf.Clamp(localDirection.z, -1f, 1f));
        }

        private bool IsHeavyAttackDamageAnimationActive()
        {
            if (animator == null || fullBodyLayerIndex < 0)
            {
                return false;
            }

            if (IsHeavyAttackState(animator.GetCurrentAnimatorStateInfo(fullBodyLayerIndex)))
            {
                return true;
            }

            return animator.IsInTransition(fullBodyLayerIndex)
                   && IsHeavyAttackState(animator.GetNextAnimatorStateInfo(fullBodyLayerIndex));
        }

        private static bool IsHeavyAttackState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.shortNameHash == HeavyAttackHitRootMotionStateHash
                   || stateInfo.shortNameHash == HeavyAttackHitStateHash;
        }

        private void UpdateRootMotionAvailability()
        {
            if (animator == null || fullBodyLayerIndex < 0)
            {
                return;
            }

            var isRootMotionActive = IsCombatActionRequested()
                                     || IsFullBodyActionState(animator.GetCurrentAnimatorStateInfo(fullBodyLayerIndex))
                                     || (animator.IsInTransition(fullBodyLayerIndex)
                                         && IsFullBodyActionState(animator.GetNextAnimatorStateInfo(fullBodyLayerIndex)));
            rootMotionController?.SetRootMotionActive(this, isRootMotionActive, GetRootMotionMultiplier());
        }

        private float GetRootMotionMultiplier()
        {
            if (animator == null || fullBodyLayerIndex < 0)
            {
                return 1f;
            }

            var currentState = animator.GetCurrentAnimatorStateInfo(fullBodyLayerIndex);
            var nextState = animator.IsInTransition(fullBodyLayerIndex)
                ? animator.GetNextAnimatorStateInfo(fullBodyLayerIndex)
                : default;
            if (currentState.shortNameHash == RollStateHash || nextState.shortNameHash == RollStateHash)
            {
                return movementConfig != null ? movementConfig.RollRootMotionMultiplier : 3f;
            }

            return currentState.shortNameHash == DodgeStateHash || nextState.shortNameHash == DodgeStateHash
                ? movementConfig != null ? movementConfig.DodgeRootMotionMultiplier : 2f
                : 1f;
        }

        private static bool IsFullBodyActionState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.shortNameHash != 0 && stateInfo.shortNameHash != EmptyStateHash;
        }

        private bool IsCombatActionRequested()
        {
            return animator != null
                   && (animator.GetBool(AttackRequestedParameterHash)
                       || animator.GetBool(HeavyAttackRequestedParameterHash)
                       || animator.GetBool(DodgeRequestedParameterHash)
                       || animator.GetBool(RollRequestedParameterHash));
        }

        private void ReleaseEvasionDirectionWhenFinished()
        {
            if (isEvasionDirectionLocked && !IsEvasionAnimationActive())
            {
                ReleaseEvasionDirection();
            }
        }

        private bool IsEvasionAnimationActive()
        {
            if (animator == null || fullBodyLayerIndex < 0)
            {
                return false;
            }

            if (animator.GetBool(DodgeRequestedParameterHash)
                || animator.GetBool(RollRequestedParameterHash))
            {
                return true;
            }

            var currentState = animator.GetCurrentAnimatorStateInfo(fullBodyLayerIndex);
            if (currentState.shortNameHash == DodgeStateHash || currentState.shortNameHash == RollStateHash)
            {
                return true;
            }

            return animator.IsInTransition(fullBodyLayerIndex)
                   && (animator.GetNextAnimatorStateInfo(fullBodyLayerIndex).shortNameHash == DodgeStateHash
                       || animator.GetNextAnimatorStateInfo(fullBodyLayerIndex).shortNameHash == RollStateHash);
        }

        private void ReleaseEvasionDirection()
        {
            if (!isEvasionDirectionLocked)
            {
                return;
            }

            isEvasionDirectionLocked = false;
            navMeshController?.ReleaseEvasionDirection();
        }

        private Stamina GetStamina()
        {
            return statsController?.GetStat(StatType.Stamina) as Stamina;
        }

        private float GetStaminaCost(Func<Stamina, float> selector)
        {
            var stamina = GetStamina();
            return stamina != null && selector != null ? Mathf.Max(0f, selector(stamina)) : 0f;
        }

        private void SpendStamina(float amount)
        {
            if (amount > 0f)
            {
                statsController?.AddValue(StatType.Stamina, -amount, StatChangeSource.Combat);
            }
        }
    }
}
