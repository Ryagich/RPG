using System;
using Combat;
using Inventory;
using Inventory.Inventories;
using Inventory.Item;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace NPC
{
    public sealed class NpcWeaponInHandController : IWeaponAnimationEventHandler, IEquippedWeaponVisual, IStartable, IDisposable
    {
        private static readonly int DrawWeaponRequestedParameterHash = Animator.StringToHash("MoveWeaponInHand");
        private static readonly int SheatheWeaponRequestedParameterHash = Animator.StringToHash("MoveWeaponInBelt");
        private static readonly int AttackRequestedParameterHash = Animator.StringToHash("Attack");

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
        private readonly CompositeDisposable disposables = new();

        private GameObject currentWeaponInstance;
        private ItemConfig currentWeaponItemConfig;
        private WeaponDamageZone activeDamageZone;
        private bool isWeaponDrawn;
        private int currentRenderedSlotIndex;
        private WeaponDisplayMode currentDisplayMode;

        public NpcWeaponInHandController(
            PlayerInventory inventory,
            PlayerWeaponHandAnchor handAnchor,
            PlayerWeaponAnimationEventReceiver animationEventReceiver,
            Animator animator,
            CharacterDamageReceiver ownerDamageReceiver,
            CharacterActionState actionState)
        {
            this.inventory = inventory;
            this.handAnchor = handAnchor;
            this.animationEventReceiver = animationEventReceiver;
            this.animator = animator;
            this.ownerDamageReceiver = ownerDamageReceiver;
            this.actionState = actionState;

            animationEventReceiver?.Bind(this);
        }

        public void Start()
        {
            inventory.Changed.Subscribe(_ => RefreshWeaponInHand()).AddTo(disposables);
            RefreshWeaponInHand();
        }

        public void Dispose()
        {
            EndCurrentWeaponDamageWindow();
            DestroyCurrentWeaponInstance();
            disposables.Dispose();
        }

        public void TakeWeaponInHandFromAnimationEvent() => MoveCurrentWeapon(WeaponDisplayMode.RightHand);
        public void BeginMoveWeaponToRightHandFromAnimationEvent() => MoveCurrentWeapon(WeaponDisplayMode.RightHand);
        public void PutWeaponOnBeltFromAnimationEvent() => MoveCurrentWeapon(WeaponDisplayMode.Belt);
        public void BeginMoveWeaponToBeltFromAnimationEvent() => MoveCurrentWeapon(WeaponDisplayMode.Belt);
        public void AttackStartedFromAnimationEvent() => actionState?.SetActionBlocked(true);
        public void BeginDamageWindowFromAnimationEvent() => BeginCurrentWeaponDamageWindow();
        public void EndDamageWindowFromAnimationEvent() => EndCurrentWeaponDamageWindow();
        public void LockMovementFromAnimationEvent() => actionState?.SetActionBlocked(true);
        public void UnlockMovementFromAnimationEvent() => actionState?.SetActionBlocked(false);
        public void AttackFinishedFromAnimationEvent()
        {
            EndCurrentWeaponDamageWindow();
            SetAttackRequested(false);
            actionState?.SetActionBlocked(false);
        }

        public void ResetAttackRequestFromAnimationEvent() => SetAttackRequested(false);

        public bool HasWeaponInWeaponSlots =>
            inventory?.LeftWeaponSlot?.ItemConfig?.ItemType == ItemType.Weapon
         || inventory?.RightWeaponSlot?.ItemConfig?.ItemType == ItemType.Weapon;

        public bool IsWeaponDrawn => isWeaponDrawn && currentDisplayMode == WeaponDisplayMode.RightHand && currentWeaponItemConfig != null;

        public bool RequestDrawWeapon()
        {
            if (!HasWeaponInWeaponSlots)
            {
                return false;
            }

            isWeaponDrawn = true;
            if (animator != null)
            {
                animator.ResetTrigger(SheatheWeaponRequestedParameterHash);
                animator.SetTrigger(DrawWeaponRequestedParameterHash);
            }

            RefreshWeaponInHand();
            return true;
        }

        public void RequestSheatheWeapon()
        {
            isWeaponDrawn = false;
            if (animator != null)
            {
                animator.ResetTrigger(DrawWeaponRequestedParameterHash);
                animator.SetTrigger(SheatheWeaponRequestedParameterHash);
            }

            RefreshWeaponInHand();
        }

        public bool RequestAttack()
        {
            if (!IsWeaponDrawn || actionState?.IsActionBlocked == true)
            {
                return false;
            }

            SetAttackRequested(true);
            return true;
        }

        public void InterruptByHitReaction()
        {
            EndCurrentWeaponDamageWindow();
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(DrawWeaponRequestedParameterHash);
            animator.ResetTrigger(SheatheWeaponRequestedParameterHash);
            SetAttackRequested(false);
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
            activeDamageZone?.BeginDamageWindow(ownerDamageReceiver, currentWeaponItemConfig);
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

        private void SetAttackRequested(bool isRequested)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(AttackRequestedParameterHash, isRequested);
        }
    }
}
