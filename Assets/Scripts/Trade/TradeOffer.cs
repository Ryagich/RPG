using System;
using System.Linq;
using Inventory;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using UnityEngine;

namespace Trade
{
    /// <summary>
    /// Owns one side's temporary trade basket. The offer deliberately contains no pricing or UI policy.
    /// Items withdrawn from it are returned to the owning inventory through that inventory's normal add path.
    /// </summary>
    public sealed class TradeOffer
    {
        public TradeSellInventory Inventory { get; } = new();
        public IInventory OwnerInventory { get; }

        public TradeOffer(IInventory ownerInventory)
        {
            OwnerInventory = ownerInventory ?? throw new ArgumentNullException(nameof(ownerInventory));
        }
    }

    /// <summary>
    /// Applies all reversible changes to a trade offer. The page chooses an item and renders the
    /// result; this service owns moving stacks and returning them to their offer owner on withdrawal
    /// or cancellation.
    /// </summary>
    public sealed class TradeOfferService
    {
        private readonly PlayerInventory playerInventory;
        private readonly Action<ItemStack> dropOverflow;

        public TradeOfferService(PlayerInventory playerInventory, Action<ItemStack> dropOverflow)
        {
            this.playerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            this.dropOverflow = dropOverflow;
        }

        public bool IsOfferInventory(TradeSession session, IInventory inventory)
        {
            return session != null && session.IsOfferInventory(inventory);
        }

        public ItemStack TryAddToPlayerInventory(ItemStack itemStack, Func<SlotModel, Vector2?> slotSizeProvider)
        {
            return playerInventory.TryAdd(itemStack, slotSizeProvider);
        }

        public bool CanMoveToInventory(TradeSession session, IInventory sourceInventory, SlotModel sourceSlot, IInventory destinationInventory)
        {
            if (session == null || destinationInventory == null)
            {
                return false;
            }

            var sourceSide = ResolveSourceSide(session, sourceInventory, sourceSlot);
            var destinationSide = ResolveInventorySide(session, destinationInventory);
            return sourceSide != TradeSide.None && sourceSide == destinationSide;
        }

        public bool CanMoveToPlayerSlot(TradeSession session, IInventory sourceInventory, SlotModel sourceSlot)
        {
            return ResolveSourceSide(session, sourceInventory, sourceSlot) == TradeSide.Player;
        }

        public bool TryAddToSourceOffer(
            TradeSession session,
            ItemStack itemStack,
            IInventory sourceInventory,
            SlotModel sourceSlot)
        {
            var destinationOffer = ResolveSourceSide(session, sourceInventory, sourceSlot) switch
            {
                TradeSide.Player => session?.PlayerOffer,
                TradeSide.Target => session?.TargetOffer,
                _ => null
            };

            if (itemStack?.ItemConfig == null || destinationOffer == null || destinationOffer.Inventory.TryAdd(itemStack) != null)
            {
                return false;
            }

            return true;
        }

        public bool TryMovePlayerSlotToOffer(TradeSession session, SlotModel slotModel, int count)
        {
            if (session == null || slotModel?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            var destinationInventory = session.PlayerOffer.Inventory;
            if (!playerInventory.TryTakeFromSlot(slotModel, count, out var itemStack) || itemStack?.ItemConfig == null)
            {
                return false;
            }

            if (destinationInventory.TryAdd(itemStack) != null)
            {
                playerInventory.TryPlaceInSlot(slotModel, itemStack, out _, out _);
                return false;
            }

            RebuildPlayerInventoryAfterBackpackChange(slotModel.ItemType, itemStack.ItemConfig);
            return true;
        }

        public bool TryMoveInventoryItemToOffer(TradeSession session, IInventory sourceInventory, ItemInInventory itemInInventory, int count)
        {
            if (session == null || sourceInventory == null || itemInInventory?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            var destinationOffer = session.GetOfferForOwner(sourceInventory);
            if (destinationOffer == null)
            {
                return false;
            }

            var itemConfig = itemInInventory.ItemConfig;
            var originalPosition = itemInInventory.Position;
            var originalCount = itemInInventory.Count;
            var moveCount = Mathf.Clamp(count, 1, originalCount);
            var movingStack = new ItemStack(itemConfig, moveCount, itemInInventory.ItemStack.IsRotated);
            var remainder = destinationOffer.Inventory.TryAdd(movingStack);
            var movedCount = moveCount - (remainder?.Count ?? 0);
            if (movedCount <= 0)
            {
                return false;
            }

            sourceInventory.Remove(itemInInventory);
            var remainingSourceCount = originalCount - movedCount;
            if (remainingSourceCount > 0)
            {
                sourceInventory.Add(new ItemStack(itemConfig, remainingSourceCount, itemInInventory.ItemStack.IsRotated), originalPosition);
            }

            return true;
        }

        public bool TryReturnOfferItem(
            TradeSession session,
            IInventory offerInventory,
            ItemInInventory itemInInventory,
            Func<SlotModel, Vector2?> slotSizeProvider)
        {
            if (session == null || offerInventory == null || itemInInventory?.ItemStack?.ItemConfig == null || !IsOfferInventory(session, offerInventory))
            {
                return false;
            }

            var offer = session.GetOffer(offerInventory);
            if (offer == null)
            {
                return false;
            }

            var returningStack = itemInInventory.ItemStack.Clone();
            offerInventory.Remove(itemInInventory);
            var remainder = TryAddToInventory(offer.OwnerInventory, returningStack, slotSizeProvider);
            var returnedCount = returningStack.Count - (remainder?.Count ?? 0);

            if (remainder != null)
            {
                offerInventory.TryAdd(remainder);
            }

            if (returnedCount > 0 && offer == session.PlayerOffer && returningStack.ItemConfig.ItemType == ItemType.Backpack)
            {
                RebuildPlayerInventoryAndDropOverflow();
            }

            return returnedCount > 0;
        }

        /// <summary>
        /// Returns an item from the player's offer using the inventory-page priority: an empty
        /// compatible equipment slot first, then only the player grid. The source offer is changed
        /// after a destination accepts at least part of the stack, so a failed transfer leaves it intact.
        /// </summary>
        public bool TryReturnPlayerOfferItemToEmptySlotOrGrid(
            TradeSession session,
            IInventory offerInventory,
            ItemInInventory itemInInventory,
            Func<SlotModel, Vector2?> slotSizeProvider)
        {
            if (session == null
             || offerInventory != session.PlayerOffer.Inventory
             || itemInInventory?.ItemStack?.ItemConfig == null
             || !offerInventory.CanGet(itemInInventory))
            {
                return false;
            }

            var returningStack = itemInInventory.ItemStack.Clone();
            ItemStack remainder;
            if (!playerInventory.TryPlaceInEmptySlot(returningStack, slotSizeProvider, out remainder))
            {
                remainder = playerInventory.TryAddToGrid(returningStack);
            }
            else if (remainder != null)
            {
                remainder = playerInventory.TryAddToGrid(remainder);
            }

            var returnedCount = returningStack.Count - (remainder?.Count ?? 0);
            if (returnedCount <= 0)
            {
                return false;
            }

            var originalPosition = itemInInventory.Position;
            offerInventory.Remove(itemInInventory);
            if (remainder != null)
            {
                offerInventory.Add(remainder, originalPosition);
            }

            if (returningStack.ItemConfig.ItemType == ItemType.Backpack)
            {
                RebuildPlayerInventoryAndDropOverflow();
            }

            return true;
        }

        public void ReturnAllOfferItems(TradeSession session, Func<SlotModel, Vector2?> slotSizeProvider)
        {
            if (session == null)
            {
                return;
            }

            ReturnOfferItems(session.PlayerOffer, slotSizeProvider);
            ReturnOfferItems(session.TargetOffer, slotSizeProvider);
            RebuildPlayerInventoryAndDropOverflow();
        }

        private void ReturnOfferItems(TradeOffer offer, Func<SlotModel, Vector2?> slotSizeProvider)
        {
            var offerInventory = offer?.Inventory;
            if (offerInventory == null)
            {
                return;
            }

            foreach (var item in offerInventory.Items.ToList())
            {
                var returnedStack = item.ItemStack.Clone();
                var remainder = TryAddToInventory(offer.OwnerInventory, returnedStack, slotSizeProvider);
                if (remainder == null)
                {
                    offerInventory.Remove(item);
                    continue;
                }

                item.ItemStack.Count = remainder.Count;
            }
        }

        private ItemStack TryAddToInventory(IInventory inventory, ItemStack itemStack, Func<SlotModel, Vector2?> slotSizeProvider)
        {
            if (inventory == playerInventory)
            {
                return playerInventory.TryAdd(itemStack, slotSizeProvider);
            }

            return inventory == null ? itemStack : inventory.TryAdd(itemStack);
        }

        private void RebuildPlayerInventoryAfterBackpackChange(ItemType slotType, ItemConfig itemConfig)
        {
            if (slotType == ItemType.Backpack && itemConfig?.ItemType == ItemType.Backpack)
            {
                RebuildPlayerInventoryAndDropOverflow();
            }
        }

        private void RebuildPlayerInventoryAndDropOverflow()
        {
            foreach (var droppedItem in playerInventory.RebuildInventoryFromCurrentBackpack())
            {
                dropOverflow?.Invoke(droppedItem);
            }
        }

        private static TradeSide ResolveSourceSide(TradeSession session, IInventory inventory, SlotModel slot)
        {
            return slot != null ? TradeSide.Player : ResolveInventorySide(session, inventory);
        }

        private static TradeSide ResolveInventorySide(TradeSession session, IInventory inventory)
        {
            if (session == null)
            {
                return TradeSide.None;
            }

            if (inventory == session.PlayerInventory || inventory == session.PlayerOffer.Inventory)
            {
                return TradeSide.Player;
            }

            return inventory == session.TargetInventory || inventory == session.TargetOffer.Inventory
                ? TradeSide.Target
                : TradeSide.None;
        }

        private enum TradeSide
        {
            None,
            Player,
            Target
        }
    }

    /// <summary>
    /// Applies the fixed double-click routes of an active trade. The UI resolves the clicked model;
    /// this coordinator decides the destination without depending on views or pointer state.
    /// </summary>
    public sealed class TradeDoubleClickActionService
    {
        private readonly PlayerInventory playerInventory;
        private readonly TradeOfferService tradeOfferService;

        public TradeDoubleClickActionService(PlayerInventory playerInventory, TradeOfferService tradeOfferService)
        {
            this.playerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            this.tradeOfferService = tradeOfferService ?? throw new ArgumentNullException(nameof(tradeOfferService));
        }

        public bool TryMoveInventoryItem(
            TradeSession session,
            IInventory sourceInventory,
            ItemInInventory itemInInventory,
            Func<SlotModel, Vector2?> slotSizeProvider)
        {
            if (session == null || sourceInventory == null || itemInInventory?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            if (sourceInventory == session.PlayerInventory || sourceInventory == session.TargetInventory)
            {
                return tradeOfferService.TryMoveInventoryItemToOffer(session, sourceInventory, itemInInventory, itemInInventory.Count);
            }

            if (sourceInventory == session.TargetOffer.Inventory)
            {
                return tradeOfferService.TryReturnOfferItem(session, sourceInventory, itemInInventory, slotSizeProvider);
            }

            return sourceInventory == session.PlayerOffer.Inventory
                   && tradeOfferService.TryReturnPlayerOfferItemToEmptySlotOrGrid(session, sourceInventory, itemInInventory, slotSizeProvider);
        }

        public bool TryMovePlayerSlot(TradeSession session, SlotModel slotModel)
        {
            if (session == null || slotModel?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            return playerInventory.TryMoveSlotItemToGrid(slotModel)
                   || tradeOfferService.TryMovePlayerSlotToOffer(session, slotModel, slotModel.ItemStack.Count);
        }
    }
}
