using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using UnityEngine;

namespace NPC
{
    public sealed class NpcEquippedWeaponDropService
    {
        private readonly PlayerInventory inventory;
        private readonly NpcWorldItemDropper dropper;
        private readonly NpcWeaponInHandController weaponVisualController;
        private readonly Transform ownerTransform;

        public NpcEquippedWeaponDropService(
            PlayerInventory inventory,
            NpcWorldItemDropper dropper,
            NpcWeaponInHandController weaponVisualController,
            Transform ownerTransform)
        {
            this.inventory = inventory;
            this.dropper = dropper;
            this.weaponVisualController = weaponVisualController;
            this.ownerTransform = ownerTransform;
        }

        public void DropCurrentWeapon()
        {
            if (!TryGetCurrentWeaponSlot(out var slot))
            {
                return;
            }

            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            var hasVisualPose = weaponVisualController != null
                             && weaponVisualController.TryGetCurrentWeaponPose(out position, out rotation);
            if (!hasVisualPose)
            {
                ResolveFallbackPose(out position, out rotation);
            }

            if (!inventory.TryTakeFromSlot(slot, out var weaponStack))
            {
                return;
            }

            dropper.DropAt(weaponStack, position, rotation);
        }

        private bool TryGetCurrentWeaponSlot(out SlotModel slot)
        {
            if (weaponVisualController != null && weaponVisualController.TryGetCurrentWeaponSlot(out slot))
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

        private void ResolveFallbackPose(out Vector3 position, out Quaternion rotation)
        {
            if (ownerTransform == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return;
            }

            position = ownerTransform.position + Vector3.up;
            rotation = ownerTransform.rotation;
        }
    }
}
