using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Inventories;
using Inventory.Item;
using Money;

namespace Trade
{
    /// <summary>
    /// Application owner for a single open trade. Its lifetime is independent from the page that renders it.
    /// </summary>
    public sealed class TradeSession
    {
        public TradeOffer PlayerOffer { get; }
        public TradeOffer TargetOffer { get; }
        public IInventory PlayerInventory { get; }
        public IInventory TargetInventory { get; }

        public TradeSession(IInventory playerInventory, IInventory targetInventory)
        {
            PlayerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            TargetInventory = targetInventory ?? throw new ArgumentNullException(nameof(targetInventory));
            PlayerOffer = new TradeOffer(PlayerInventory);
            TargetOffer = new TradeOffer(TargetInventory);
        }

        public bool IsOfferInventory(IInventory inventory)
        {
            return inventory == PlayerOffer.Inventory || inventory == TargetOffer.Inventory;
        }

        public TradeOffer GetOfferForOwner(IInventory inventory)
        {
            if (inventory == PlayerInventory)
            {
                return PlayerOffer;
            }

            return inventory == TargetInventory ? TargetOffer : null;
        }

        public TradeOffer GetOffer(IInventory inventory)
        {
            if (inventory == PlayerOffer.Inventory)
            {
                return PlayerOffer;
            }

            return inventory == TargetOffer.Inventory ? TargetOffer : null;
        }
    }

    /// <summary>Applies a confirmed offer and evaluates capacity without any page or UI dependency.</summary>
    public sealed class TradeSettlementService
    {
        public bool CanAccept(IInventory destination, IEnumerable<ItemStack> incoming, IEnumerable<ItemStack> reserved, IEnumerable<ItemStack> held)
        {
            return destination != null
                   && TradeCapacityPlanner.TryCreate(destination, out var capacity)
                   && capacity.CanAddStacks(incoming)
                   && capacity.CanAddStacks(reserved)
                   && capacity.CanAddStacks(held);
        }

        public void Settle(TradeOffer offer, IInventory destination, MoneyStorage sellerMoney, MoneyStorage buyerMoney)
        {
            if (offer == null || destination == null || sellerMoney == null || buyerMoney == null)
            {
                return;
            }

            foreach (var item in offer.Inventory.Items.ToList())
            {
                var stack = item.ItemStack.Clone();
                var affordableCount = buyerMoney.GetAffordableItemCount(stack.ItemConfig.Price, stack.Count);
                if (affordableCount <= 0)
                {
                    continue;
                }

                stack.Count = Math.Min(stack.Count, affordableCount);
                var remainder = destination.TryAdd(stack);
                var soldCount = stack.Count - (remainder?.Count ?? 0);
                if (soldCount <= 0)
                {
                    continue;
                }

                if (soldCount >= item.Count)
                {
                    offer.Inventory.Remove(item);
                }
                else
                {
                    item.ItemStack.Count -= soldCount;
                }

                var price = item.ItemConfig.Price * soldCount;
                buyerMoney.TrySpend(price);
                sellerMoney.Add(price);
            }
        }

        public int CalculatePrice(IInventory inventory)
        {
            return inventory?.Items.Where(item => item?.ItemConfig != null).Sum(item => item.ItemStack.TotalPrice) ?? 0;
        }
    }
}
