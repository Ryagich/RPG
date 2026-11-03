using Inventory.Grid;
using Inventory.Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace UI.Inventory
{
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
