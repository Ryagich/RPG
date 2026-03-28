using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Slot;
using UnityEngine;

namespace UI.Pages
{
    public interface IInventoryInteractionPage
    {
        bool TryCaptureGrabOffset(Vector2 screenPoint);
        void ResetGrabOffset();
        bool TryGetHoveredSlot(Vector2 screenPoint, out SlotModel slotModel);
        bool TryGetPlacementTile(Vector2 screenPoint, IInventory inventory, out Tile tile);
        bool IsInPlayerSections(Vector2 screenPoint);
    }
}