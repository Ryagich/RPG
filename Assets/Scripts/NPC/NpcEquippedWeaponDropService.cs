using System;
using Inventory;
using Inventory.Inventories;
using UnityEngine;

namespace NPC
{
    [Obsolete("Use Inventory.EquippedWeaponDropService. Player and NPC share equipped weapon drop systems.")]
    public sealed class NpcEquippedWeaponDropService
    {
        private readonly EquippedWeaponDropService inner;

        public NpcEquippedWeaponDropService(
            IEquipmentInventory inventory,
            CharacterWorldItemDropper dropper,
            IEquippedWeaponVisual weaponVisual,
            Transform ownerTransform)
        {
            inner = new EquippedWeaponDropService(inventory, dropper, weaponVisual, ownerTransform);
        }

        public void DropCurrentWeapon()
        {
            inner.DropCurrentWeapon();
        }
    }
}
