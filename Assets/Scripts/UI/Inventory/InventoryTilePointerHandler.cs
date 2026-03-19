using Inventory;
using Inventory.Grid;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Inventory
{
    public class InventoryTilePointerHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public static Tile HoveredTile { get; private set; }
        public static IInventory HoveredInventory { get; private set; }

        public Tile Tile { get; private set; }
        public IInventory Inventory { get; private set; }

        public void Initialize(IInventory inventory, Tile tile)
        {
            Inventory = inventory;
            Tile = tile;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoveredInventory = Inventory;
            HoveredTile = Tile;
            Debug.Log(HoveredTile.Index);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (HoveredInventory == Inventory && HoveredTile == Tile)
            {
                HoveredInventory = null;
                HoveredTile = null;
            }
        }

        private void OnDisable()
        {
            if (HoveredInventory == Inventory && HoveredTile == Tile)
            {
                HoveredInventory = null;
                HoveredTile = null;
            }
        }

        public static bool TryGetHovered(out IInventory inventory, out Tile tile)
        {
            inventory = HoveredInventory;
            tile = HoveredTile;
            return inventory != null && tile != null;
        }
    }
}