using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Slot;
using Messages;
using UnityEngine;

namespace UI.Pages
{
    public interface IInventoryInteractionPage
    {
        bool TryHandleMouseDown(MouseButtonType button, Vector2 screenPoint);
        bool TryCaptureGrabOffset(Vector2 screenPoint, out Vector2 handGrabOffset);
        void SetGrabOffset(Vector2 handGrabOffset);
        void ResetGrabOffset();
        bool TryGetHoveredSlot(Vector2 screenPoint, out SlotModel slotModel);
        bool TryGetSlotSize(SlotModel slotModel, out Vector2 slotSize);
        bool TryGetHoveredFastSlot(Vector2 screenPoint, out FastSlotModel fastSlotModel);
        bool TryGetFastSlotRect(FastSlotModel fastSlotModel, out RectTransform slotRect);
        bool TryGetPlacementTile(Vector2 screenPoint, IInventory inventory, out Tile tile);
        bool IsInPlayerSections(Vector2 screenPoint);
    }
}
