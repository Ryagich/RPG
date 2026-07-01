using System.Collections.Generic;
using Inventory;
using Inventory.Inventories;
using Inventory.Item;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NPC
{
    public sealed class NpcItemPickupService
    {
        private readonly PlayerInventory inventory;
        private readonly CharacterWorldItemDropper dropper;

        public NpcItemPickupService(PlayerInventory inventory, CharacterWorldItemDropper dropper)
        {
            this.inventory = inventory;
            this.dropper = dropper;
        }

        public bool TryPickup(NpcItemPickupPlan plan)
        {
            if (plan?.ItemHolder == null || plan.ItemStack?.ItemConfig == null || inventory == null)
            {
                return false;
            }

            var removedStacks = RemovePlannedDrops(plan.DropSources);
            var originalCount = plan.ItemStack.Count;
            var remainder = plan.UseSlot
                ? TryPlaceInSlot(plan)
                : TryAddToInventory(plan.ItemStack);

            if (remainder != null && remainder.Count >= originalCount)
            {
                RestoreOrDrop(removedStacks);
                return false;
            }

            foreach (var removedStack in removedStacks)
            {
                dropper.Drop(removedStack);
            }

            if (remainder == null)
            {
                Object.Destroy(plan.ItemHolder.gameObject);
            }
            else
            {
                plan.ItemHolder.SetCount(remainder.Count);
            }

            DropPendingOverflowItems();
            return true;
        }

        private ItemStack TryPlaceInSlot(NpcItemPickupPlan plan)
        {
            if (!inventory.TryPlaceInSlot(plan.TargetSlot, plan.ItemStack, out var remainder, out var replaced))
            {
                return plan.ItemStack;
            }

            if (replaced != null)
            {
                var replacedRemainder = TryAddToInventory(replaced);
                if (replacedRemainder != null)
                {
                    dropper.Drop(replacedRemainder);
                }
            }

            if (remainder != null)
            {
                return TryAddToInventory(remainder);
            }

            return null;
        }

        private ItemStack TryAddToInventory(ItemStack itemStack)
        {
            var remainder = inventory.TryAdd(itemStack);
            if (remainder == null)
            {
                return null;
            }

            return inventory.TryAddToGridRebuilding(remainder);
        }

        private List<ItemStack> RemovePlannedDrops(IEnumerable<NpcInventoryDropSource> dropSources)
        {
            var removed = new List<ItemStack>();
            foreach (var source in dropSources)
            {
                if (source?.Snapshot?.ItemConfig == null)
                {
                    continue;
                }

                if (source.GridItem != null && inventory.CanGet(source.GridItem))
                {
                    removed.Add(source.GridItem.ItemStack.Clone());
                    inventory.Remove(source.GridItem);
                    continue;
                }

                if (source.Slot != null && source.Slot.ItemStack?.ItemConfig == source.Snapshot.ItemConfig)
                {
                    if (inventory.TryTakeFromSlot(source.Slot, out var slotStack))
                    {
                        removed.Add(slotStack);
                    }
                }
            }

            return removed;
        }

        private void RestoreOrDrop(IEnumerable<ItemStack> stacks)
        {
            foreach (var stack in stacks)
            {
                var remainder = TryAddToInventory(stack);
                if (remainder != null)
                {
                    dropper.Drop(remainder);
                }
            }
        }

        private void DropPendingOverflowItems()
        {
            foreach (var overflowItem in inventory.ConsumePendingOverflowItems())
            {
                dropper.Drop(overflowItem);
            }
        }
    }
}
