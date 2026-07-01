using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using UnityEngine;

namespace Inventory
{
    public sealed class EquippedWeaponDropService
    {
        private readonly PlayerInventory inventory;
        private readonly CharacterWorldItemDropper dropper;
        private readonly IEquippedWeaponVisual weaponVisual;
        private readonly Transform ownerTransform;

        public EquippedWeaponDropService(
            PlayerInventory inventory,
            CharacterWorldItemDropper dropper,
            IEquippedWeaponVisual weaponVisual,
            Transform ownerTransform)
        {
            this.inventory = inventory;
            this.dropper = dropper;
            this.weaponVisual = weaponVisual;
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
            var hasVisualPose = weaponVisual != null
                             && weaponVisual.TryGetCurrentWeaponPose(out position, out rotation);
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
            if (weaponVisual != null && weaponVisual.TryGetCurrentWeaponSlot(out slot))
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
