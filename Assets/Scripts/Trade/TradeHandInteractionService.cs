using Inventory;
using Inventory.Inventories;

namespace Trade
{
    /// <summary>
    /// Coordinates the player hand with the lifetime of a trade session.
    /// It has no UI knowledge and never transfers an item across trade participants.
    /// </summary>
    public sealed class TradeHandInteractionService
    {
        private readonly PlayerInventory playerInventory;
        private readonly InventoryHandController inventoryHandController;

        public TradeHandInteractionService(PlayerInventory playerInventory, InventoryHandController inventoryHandController)
        {
            this.playerInventory = playerInventory;
            this.inventoryHandController = inventoryHandController;
        }

        public bool TryReturnHeldItemBeforeCancel(TradeSession session)
        {
            if (session == null || playerInventory.HandSlot.Value?.ItemStack == null)
            {
                return true;
            }

            var sourceInventory = playerInventory.HandSourceInventory.Value;
            if (sourceInventory != session.PlayerInventory
                && sourceInventory != session.TargetInventory
                && !session.IsOfferInventory(sourceInventory))
            {
                return true;
            }

            return inventoryHandController.TryReturnHeldItemToSource();
        }
    }
}
