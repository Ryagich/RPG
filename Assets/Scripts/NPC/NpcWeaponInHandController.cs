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
    public sealed class NpcWeaponInHandController : IWeaponAnimationEventHandler, IStartable, IDisposable
    {
        private static readonly int DrawWeaponRequestedParameterHash = Animator.StringToHash("MoveWeaponInHand");
        private static readonly int SheatheWeaponRequestedParameterHash = Animator.StringToHash("MoveWeaponInBelt");

        private readonly PlayerInventory inventory;
        private readonly PlayerWeaponHandAnchor handAnchor;
        private readonly PlayerWeaponAnimationEventReceiver animationEventReceiver;
        private readonly Animator animator;
        private readonly CharacterDamageReceiver ownerDamageReceiver;
        private readonly CompositeDisposable disposables = new();

        private GameObject currentWeaponInstance;
        private ItemConfig currentWeaponItemConfig;
        private WeaponDamageZone activeDamageZone;
        private bool hasWeaponPoseRequested;
        private int currentRenderedSlotIndex;

        public NpcWeaponInHandController(
            PlayerInventory inventory,
            PlayerWeaponHandAnchor handAnchor,
            PlayerWeaponAnimationEventReceiver animationEventReceiver,
            Animator animator,
            CharacterDamageReceiver ownerDamageReceiver)
        {
            this.inventory = inventory;
            this.handAnchor = handAnchor;
            this.animationEventReceiver = animationEventReceiver;
            this.animator = animator;
            this.ownerDamageReceiver = ownerDamageReceiver;

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

        public void TakeWeaponInHandFromAnimationEvent() => RefreshWeaponInHand();
        public void BeginMoveWeaponToRightHandFromAnimationEvent() { }
        public void PutWeaponOnBeltFromAnimationEvent() { }
        public void BeginMoveWeaponToBeltFromAnimationEvent() { }
        public void AttackStartedFromAnimationEvent() { }
        public void BeginDamageWindowFromAnimationEvent() => BeginCurrentWeaponDamageWindow();
        public void EndDamageWindowFromAnimationEvent() => EndCurrentWeaponDamageWindow();
        public void LockMovementFromAnimationEvent() { }
        public void UnlockMovementFromAnimationEvent() { }
        public void AttackFinishedFromAnimationEvent() => EndCurrentWeaponDamageWindow();
        public void ResetAttackRequestFromAnimationEvent() { }

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
            UpdateWeaponPose(false);
            if (selectedWeapon == currentWeaponItemConfig && currentRenderedSlotIndex == selectedSlotIndex && currentWeaponInstance != null)
            {
                return;
            }

            RenderWeapon(selectedWeapon, selectedSlotIndex);
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

        private void RenderWeapon(ItemConfig itemConfig, int slotIndex)
        {
            DestroyCurrentWeaponInstance();
            if (itemConfig == null || itemConfig.WeaponInHandPrefab == null || handAnchor?.Belt == null)
            {
                currentWeaponItemConfig = null;
                currentRenderedSlotIndex = 0;
                return;
            }

            currentWeaponItemConfig = itemConfig;
            currentRenderedSlotIndex = slotIndex;
            currentWeaponInstance = Object.Instantiate(itemConfig.WeaponInHandPrefab, handAnchor.Belt, false);
            currentWeaponInstance.name = $"{itemConfig.WeaponInHandPrefab.name} | NPC Belt";
            ApplyAttachmentTransform(currentWeaponInstance.transform, itemConfig);
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

        private static void ApplyAttachmentTransform(Transform targetTransform, ItemConfig itemConfig)
        {
            if (targetTransform == null || itemConfig?.BeltWeaponAttachment == null)
            {
                return;
            }

            var attachment = itemConfig.BeltWeaponAttachment;
            targetTransform.localPosition = attachment.LocalPosition;
            targetTransform.localRotation = Quaternion.Euler(attachment.LocalEulerAngles);
        }

        private void UpdateWeaponPose(bool hasWeapon)
        {
            if (animator == null || hasWeaponPoseRequested == hasWeapon)
            {
                return;
            }

            hasWeaponPoseRequested = hasWeapon;
            if (hasWeapon)
            {
                animator.ResetTrigger(SheatheWeaponRequestedParameterHash);
                animator.SetTrigger(DrawWeaponRequestedParameterHash);
                return;
            }

            animator.ResetTrigger(DrawWeaponRequestedParameterHash);
            animator.SetTrigger(SheatheWeaponRequestedParameterHash);
        }
    }
}
