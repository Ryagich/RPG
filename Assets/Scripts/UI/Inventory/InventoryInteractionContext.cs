using Inventory.Grid;
using Inventory.Inventories;
using UI.Pages;

namespace UI.Inventory
{
    /// <summary>Tracks the active inventory page and the grid tile currently under the pointer.</summary>
    public sealed class InventoryInteractionContext
    {
        public IInventoryInteractionPage ActivePage { get; private set; }
        public TradePage ActiveTradePage => ActivePage as TradePage;

        private IInventory hoveredInventory;
        private Tile hoveredTile;

        public void SetActivePage(IInventoryInteractionPage page) => ActivePage = page;

        public void ClearActivePage(IInventoryInteractionPage page)
        {
            if (ActivePage != page) return;
            ActivePage = null;
            ClearHoveredInventory();
        }

        public void SetHoveredInventory(IInventory inventory, Tile tile)
        {
            hoveredInventory = inventory;
            hoveredTile = tile;
        }

        public void ClearHoveredInventory(IInventory inventory = null, Tile tile = null)
        {
            if (inventory != null && (hoveredInventory != inventory || hoveredTile != tile)) return;
            hoveredInventory = null;
            hoveredTile = null;
        }

        public bool TryGetHoveredInventory(out IInventory inventory, out Tile tile)
        {
            inventory = hoveredInventory;
            tile = hoveredTile;
            return inventory != null && tile != null;
        }
    }
}
