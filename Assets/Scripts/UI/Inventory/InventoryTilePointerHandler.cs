using Inventory;
using Inventory.Grid;
using Inventory.Inventories;
using UI.Pages;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace UI.Inventory
{
    public sealed class InventoryInteractionContext
    {
        public IInventoryInteractionPage ActivePage { get; private set; }
        public TradePage ActiveTradePage => ActivePage as TradePage;

        private IInventory hoveredInventory;
        private Tile hoveredTile;

        public void SetActivePage(IInventoryInteractionPage page)
        {
            ActivePage = page;
        }

        public void ClearActivePage(IInventoryInteractionPage page)
        {
            if (ActivePage == page)
            {
                ActivePage = null;
                ClearHoveredInventory();
            }
        }

        public void SetHoveredInventory(IInventory inventory, Tile tile)
        {
            hoveredInventory = inventory;
            hoveredTile = tile;
        }

        public void ClearHoveredInventory(IInventory inventory = null, Tile tile = null)
        {
            if (inventory == null || (hoveredInventory == inventory && hoveredTile == tile))
            {
                hoveredInventory = null;
                hoveredTile = null;
            }
        }

        public bool TryGetHoveredInventory(out IInventory inventory, out Tile tile)
        {
            inventory = hoveredInventory;
            tile = hoveredTile;
            return inventory != null && tile != null;
        }
    }

    public class InventoryTilePointerHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Tile Tile { get; private set; }
        public IInventory Inventory { get; private set; }
        private InventoryInteractionContext interactionContext;

        [Inject]
        public void Construct(InventoryInteractionContext context)
        {
            interactionContext = context;
        }

        public void Initialize(IInventory inventory, Tile tile)
        {
            Inventory = inventory;
            Tile = tile;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            interactionContext?.SetHoveredInventory(Inventory, Tile);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            interactionContext?.ClearHoveredInventory(Inventory, Tile);
        }

        private void OnDisable()
        {
            interactionContext?.ClearHoveredInventory(Inventory, Tile);
        }
    }
}
