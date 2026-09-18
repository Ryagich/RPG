using System;
using System.Collections.Generic;
using System.Linq;
using Inventory;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using Inventory.Storage;
using UnityEngine;
using YG;

namespace Saves
{
    /// <summary>
    /// Translates each NPC inventory implementation to the primitive save format. Merchant
    /// stock is part of the merchant's world state, rather than a new random roll on every
    /// location load, so both its grid and equipped items are persisted.
    /// </summary>
    internal static class NpcInventorySaveMapper
    {
        public static SavedInventoryItem[] Capture(IEquipmentInventory inventory)
        {
            if (inventory is PlayerInventory playerInventory)
            {
                return GameSaveService.SerializeInventory(playerInventory);
            }

            if (inventory is MerchantInventory merchantInventory)
            {
                return CaptureMerchant(merchantInventory);
            }

            return CaptureEquipment(inventory);
        }

        public static void Restore(
            IEquipmentInventory inventory,
            ItemStorage itemStorage,
            IEnumerable<SavedInventoryItem> savedEntries)
        {
            if (inventory is PlayerInventory playerInventory)
            {
                GameSaveService.RestoreInventory(playerInventory, itemStorage, savedEntries);
                return;
            }

            if (inventory is MerchantInventory merchantInventory)
            {
                RestoreMerchant(merchantInventory, itemStorage, savedEntries);
                return;
            }

            RestoreEquipment(inventory, itemStorage, savedEntries);
        }

        private static SavedInventoryItem[] CaptureMerchant(MerchantInventory inventory)
        {
            var entries = new List<SavedInventoryItem>();
            foreach (ItemInInventory item in inventory.Items)
            {
                if (item?.ItemStack?.ItemConfig == null || item.Tiles == null || item.Tiles.Count == 0 ||
                    string.IsNullOrWhiteSpace(item.ItemStack.ItemConfig.Id))
                {
                    continue;
                }

                entries.Add(new SavedInventoryItem
                {
                    itemId = item.ItemStack.ItemConfig.Id,
                    count = item.ItemStack.Count,
                    x = item.Tiles.Min(tile => tile.Index.x),
                    y = item.Tiles.Min(tile => tile.Index.y),
                    rotated = item.ItemStack.IsRotated ? 1 : 0,
                    slot = (int)PlayerInventoryContainer.Grid
                });
            }

            entries.AddRange(CaptureEquipment(inventory));
            return entries.ToArray();
        }

        private static void RestoreMerchant(
            MerchantInventory inventory,
            ItemStorage itemStorage,
            IEnumerable<SavedInventoryItem> savedEntries)
        {
            if (inventory == null || itemStorage == null)
            {
                return;
            }

            foreach (ItemInInventory item in inventory.Items.ToArray())
            {
                inventory.Remove(item);
            }

            RestoreEquipment(inventory, itemStorage, savedEntries);
            if (savedEntries == null)
            {
                return;
            }

            foreach (SavedInventoryItem entry in savedEntries)
            {
                if (entry.slot != (int)PlayerInventoryContainer.Grid || entry.count <= 0 ||
                    !itemStorage.TryGetById(entry.itemId, out ItemConfig itemConfig))
                {
                    continue;
                }

                var stack = new ItemStack(itemConfig, entry.count, entry.rotated != 0);
                ItemStack remainder = inventory.Tiles.TryGetTile(entry.x, entry.y, out var tile)
                    ? inventory.TryAdd(stack, tile)
                    : inventory.TryAddToGrid(stack);
                if (remainder != null)
                {
                    Debug.LogWarning($"Saved merchant item '{entry.itemId}' could not be restored.");
                }
            }
        }

        private static SavedInventoryItem[] CaptureEquipment(IEquipmentInventory inventory)
        {
            if (inventory == null)
            {
                return Array.Empty<SavedInventoryItem>();
            }

            return GetEquipmentSlots(inventory)
                .Where(entry => entry.Slot?.ItemStack?.ItemConfig != null &&
                                !string.IsNullOrWhiteSpace(entry.Slot.ItemStack.ItemConfig.Id))
                .Select(entry => new SavedInventoryItem
                {
                    itemId = entry.Slot.ItemStack.ItemConfig.Id,
                    count = entry.Slot.ItemStack.Count,
                    x = 0,
                    y = 0,
                    rotated = entry.Slot.ItemStack.IsRotated ? 1 : 0,
                    slot = (int)entry.Container
                })
                .ToArray();
        }

        private static void RestoreEquipment(
            IEquipmentInventory inventory,
            ItemStorage itemStorage,
            IEnumerable<SavedInventoryItem> savedEntries)
        {
            if (inventory == null || itemStorage == null)
            {
                return;
            }

            foreach ((SlotModel slot, _) in GetEquipmentSlots(inventory))
            {
                inventory.TryTakeFromSlot(slot, out _);
            }

            if (savedEntries == null)
            {
                return;
            }

            foreach (SavedInventoryItem entry in savedEntries)
            {
                if (!TryGetEquipmentSlot(inventory, entry.slot, out SlotModel slot) || entry.count <= 0 ||
                    !itemStorage.TryGetById(entry.itemId, out ItemConfig itemConfig))
                {
                    continue;
                }

                var stack = new ItemStack(itemConfig, entry.count, entry.rotated != 0);
                if (!inventory.TryPlaceInSlot(slot, stack, out ItemStack remainder, out _) || remainder != null)
                {
                    Debug.LogWarning($"Saved equipment item '{entry.itemId}' could not be restored.");
                }
            }
        }

        private static bool TryGetEquipmentSlot(IEquipmentInventory inventory, int container, out SlotModel slot)
        {
            slot = container switch
            {
                (int)PlayerInventoryContainer.Helm => inventory.HelmSlot,
                (int)PlayerInventoryContainer.Face => inventory.FaceSlot,
                (int)PlayerInventoryContainer.Body => inventory.BodySlot,
                (int)PlayerInventoryContainer.Hands => inventory.HandsSlot,
                (int)PlayerInventoryContainer.Arms => inventory.ArmsSlot,
                (int)PlayerInventoryContainer.Legs => inventory.LegsSlot,
                (int)PlayerInventoryContainer.Hips => inventory.HipsSlot,
                (int)PlayerInventoryContainer.Backpack => inventory.BackpackSlot,
                (int)PlayerInventoryContainer.LeftWeapon => inventory.LeftWeaponSlot,
                (int)PlayerInventoryContainer.RightWeapon => inventory.RightWeaponSlot,
                _ => null
            };
            return slot != null;
        }

        private static IEnumerable<(SlotModel Slot, PlayerInventoryContainer Container)> GetEquipmentSlots(
            IEquipmentInventory inventory)
        {
            yield return (inventory.HelmSlot, PlayerInventoryContainer.Helm);
            yield return (inventory.FaceSlot, PlayerInventoryContainer.Face);
            yield return (inventory.BodySlot, PlayerInventoryContainer.Body);
            yield return (inventory.HandsSlot, PlayerInventoryContainer.Hands);
            yield return (inventory.ArmsSlot, PlayerInventoryContainer.Arms);
            yield return (inventory.LegsSlot, PlayerInventoryContainer.Legs);
            yield return (inventory.HipsSlot, PlayerInventoryContainer.Hips);
            yield return (inventory.BackpackSlot, PlayerInventoryContainer.Backpack);
            yield return (inventory.LeftWeaponSlot, PlayerInventoryContainer.LeftWeapon);
            yield return (inventory.RightWeaponSlot, PlayerInventoryContainer.RightWeapon);
        }
    }
}
