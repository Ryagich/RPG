using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Item;
using Inventory.Slot;
using UnityEngine;

namespace Inventory.Inventories
{
    /// <summary>
    /// A merchant's permanent stock and character equipment. The grid behaviour comes directly
    /// from TradeSellInventory, while character-only operations are kept here.
    /// </summary>
    public sealed class MerchantInventory : TradeSellInventory, IEquipmentInventory, ICharacterInventoryCapacity, IInventoryOverflow
    {
        public SlotModel HelmSlot { get; } = new(ItemType.Helm, SlotStackLimitType.SingleItem);
        public SlotModel FaceSlot { get; } = new(ItemType.Face, SlotStackLimitType.SingleItem);
        public SlotModel BodySlot { get; } = new(ItemType.Body, SlotStackLimitType.SingleItem);
        public SlotModel HandsSlot { get; } = new(ItemType.Hands, SlotStackLimitType.SingleItem);
        public SlotModel ArmsSlot { get; } = new(ItemType.Arms, SlotStackLimitType.SingleItem);
        public SlotModel LegsSlot { get; } = new(ItemType.Legs, SlotStackLimitType.SingleItem);
        public SlotModel HipsSlot { get; } = new(ItemType.Hips, SlotStackLimitType.SingleItem);
        public SlotModel BackpackSlot { get; } = new(ItemType.Backpack, SlotStackLimitType.SingleItem);
        public SlotModel LeftWeaponSlot { get; } = new(ItemType.Weapon, SlotStackLimitType.SingleItem);
        public SlotModel RightWeaponSlot { get; } = new(ItemType.Weapon, SlotStackLimitType.SingleItem);

        // Merchant stock has no weight limit. A zero current weight keeps weight-based systems neutral.
        public float CurrentWeight => 0f;
        public bool IsFaceSlotBlocked => HelmSlot.ItemConfig != null && HelmSlot.ItemConfig.BlocksFaceSlot;

        bool IInventory.TryAdd(ItemConfig config)
        {
            return TryAddToEquipmentOrGrid(new ItemStack(config)) == null;
        }

        ItemStack IInventory.TryAdd(ItemStack itemStack)
        {
            return TryAddToEquipmentOrGrid(itemStack);
        }

        public bool IsSlotBlocked(SlotModel slot)
        {
            return slot == FaceSlot && IsFaceSlotBlocked;
        }

        public ItemStack TryAddToGrid(ItemStack itemStack)
        {
            return base.TryAdd(itemStack);
        }

        public ItemStack TryAddToGridRebuilding(ItemStack itemStack)
        {
            return base.TryAdd(itemStack);
        }

        public bool TryMoveFirstGridItemToEmptySlot(ItemType slotType)
        {
            var slot = GetSlots().FirstOrDefault(current => current.ItemType == slotType && current.ItemStack == null);
            var item = Items.FirstOrDefault(current => current?.ItemStack?.ItemConfig?.ItemType == slotType);
            if (slot == null || item?.ItemStack == null || !CanAcceptItemInSlot(slot, item.ItemStack))
            {
                return false;
            }

            var itemStack = item.ItemStack;
            base.Remove(item);
            slot.ItemStack = itemStack;
            ResolveSlotSideEffects(slot);
            NotifyChanged();
            return true;
        }

        public bool TryTakeFromSlot(SlotModel slot, out ItemStack itemStack)
        {
            itemStack = null;
            if (slot?.ItemStack == null || !GetSlots().Contains(slot))
            {
                return false;
            }

            itemStack = slot.ItemStack;
            slot.ItemStack = null;
            NotifyChanged();
            return true;
        }

        public bool TryPlaceInSlot(
            SlotModel slot,
            ItemStack newItemStack,
            out ItemStack remainderStack,
            out ItemStack replacedStack)
        {
            remainderStack = null;
            replacedStack = null;
            if (!GetSlots().Contains(slot) || !CanAcceptItemInSlot(slot, newItemStack))
            {
                return false;
            }

            if (slot.ItemStack == null)
            {
                var countToPlace = Mathf.Min(newItemStack.Count, slot.GetMaxStack(newItemStack.ItemConfig));
                slot.ItemStack = new ItemStack(newItemStack.ItemConfig, countToPlace, newItemStack.IsRotated, newItemStack.RuntimeTag);
                remainderStack = newItemStack.Count > countToPlace
                    ? new ItemStack(newItemStack.ItemConfig, newItemStack.Count - countToPlace, newItemStack.IsRotated, newItemStack.RuntimeTag)
                    : null;
                ResolveSlotSideEffects(slot);
                NotifyChanged();
                return true;
            }

            if (slot.ItemStack.CanStackWith(newItemStack))
            {
                var freeSpace = slot.GetMaxStack(newItemStack.ItemConfig) - slot.ItemStack.Count;
                if (freeSpace <= 0)
                {
                    remainderStack = newItemStack.Clone();
                    return false;
                }

                var movedCount = Mathf.Min(freeSpace, newItemStack.Count);
                slot.ItemStack.Count += movedCount;
                remainderStack = movedCount < newItemStack.Count
                    ? new ItemStack(newItemStack.ItemConfig, newItemStack.Count - movedCount, newItemStack.IsRotated, newItemStack.RuntimeTag)
                    : null;
                NotifyChanged();
                return true;
            }

            replacedStack = slot.ItemStack;
            var replacementCount = Mathf.Min(newItemStack.Count, slot.GetMaxStack(newItemStack.ItemConfig));
            slot.ItemStack = new ItemStack(newItemStack.ItemConfig, replacementCount, newItemStack.IsRotated, newItemStack.RuntimeTag);
            remainderStack = newItemStack.Count > replacementCount
                ? new ItemStack(newItemStack.ItemConfig, newItemStack.Count - replacementCount, newItemStack.IsRotated, newItemStack.RuntimeTag)
                : null;
            ResolveSlotSideEffects(slot);
            NotifyChanged();
            return true;
        }

        public IReadOnlyList<ItemStack> ConsumePendingOverflowItems()
        {
            return Array.Empty<ItemStack>();
        }

        private ItemStack TryAddToEquipmentOrGrid(ItemStack itemStack)
        {
            var remainingStack = CloneIfValid(itemStack);
            if (remainingStack == null)
            {
                return itemStack;
            }

            var changed = FillExistingSlotStacks(remainingStack);
            if (remainingStack.Count > 0)
            {
                var freeSlotRemainder = TryAddToFreeSlot(remainingStack);
                if (freeSlotRemainder == null)
                {
                    NotifyChanged();
                    return null;
                }

                changed |= freeSlotRemainder.Count != remainingStack.Count;
                remainingStack = freeSlotRemainder;
            }

            if (remainingStack.Count <= 0)
            {
                if (changed)
                {
                    NotifyChanged();
                }

                return null;
            }

            var gridRemainder = base.TryAdd(remainingStack);
            if (changed && gridRemainder != null)
            {
                NotifyChanged();
            }

            return gridRemainder;
        }

        private bool FillExistingSlotStacks(ItemStack remainingStack)
        {
            var changed = false;
            foreach (var slot in GetSlots())
            {
                if (remainingStack.Count <= 0 || slot.ItemStack == null || !slot.ItemStack.CanStackWith(remainingStack))
                {
                    continue;
                }

                var freeSpace = slot.GetMaxStack(remainingStack.ItemConfig) - slot.ItemStack.Count;
                if (freeSpace <= 0)
                {
                    continue;
                }

                var movedCount = Mathf.Min(freeSpace, remainingStack.Count);
                slot.ItemStack.Count += movedCount;
                remainingStack.Count -= movedCount;
                changed = true;
            }

            return changed;
        }

        private ItemStack TryAddToFreeSlot(ItemStack itemStack)
        {
            foreach (var slot in GetSlots())
            {
                if (slot.ItemStack != null || !CanAcceptItemInSlot(slot, itemStack))
                {
                    continue;
                }

                var countToPlace = Mathf.Min(itemStack.Count, slot.GetMaxStack(itemStack.ItemConfig));
                slot.ItemStack = new ItemStack(itemStack.ItemConfig, countToPlace, itemStack.IsRotated, itemStack.RuntimeTag);
                ResolveSlotSideEffects(slot);
                return itemStack.Count > countToPlace
                    ? new ItemStack(itemStack.ItemConfig, itemStack.Count - countToPlace, itemStack.IsRotated, itemStack.RuntimeTag)
                    : null;
            }

            return itemStack;
        }

        private void ResolveSlotSideEffects(SlotModel slot)
        {
            if (slot != HelmSlot || !IsFaceSlotBlocked || FaceSlot.ItemStack == null)
            {
                return;
            }

            var blockedFaceItem = FaceSlot.ItemStack;
            FaceSlot.ItemStack = null;
            base.TryAdd(blockedFaceItem);
        }

        private bool CanAcceptItemInSlot(SlotModel slot, ItemStack itemStack)
        {
            return slot != null
                   && itemStack?.ItemConfig != null
                   && slot.ItemType == itemStack.ItemConfig.ItemType
                   && !IsSlotBlocked(slot);
        }

        private static ItemStack CloneIfValid(ItemStack itemStack)
        {
            return itemStack?.ItemConfig == null || itemStack.Count <= 0 ? null : itemStack.Clone();
        }

        private IEnumerable<SlotModel> GetSlots()
        {
            yield return HelmSlot;
            yield return FaceSlot;
            yield return BodySlot;
            yield return HandsSlot;
            yield return ArmsSlot;
            yield return LegsSlot;
            yield return HipsSlot;
            yield return BackpackSlot;
            yield return LeftWeaponSlot;
            yield return RightWeaponSlot;
        }
    }
}
