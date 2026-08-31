using Inventory.Item;
using Inventory.Slot;

namespace Inventory.Inventories
{
    /// <summary>
    /// Equipment and grid operations used by character systems. This deliberately excludes
    /// player input, fast slots and movement-specific inventory behaviour.
    /// </summary>
    public interface IEquipmentInventory : ITiledInventory
    {
        SlotModel HelmSlot { get; }
        SlotModel FaceSlot { get; }
        SlotModel BodySlot { get; }
        SlotModel HandsSlot { get; }
        SlotModel ArmsSlot { get; }
        SlotModel LegsSlot { get; }
        SlotModel HipsSlot { get; }
        SlotModel BackpackSlot { get; }
        SlotModel LeftWeaponSlot { get; }
        SlotModel RightWeaponSlot { get; }

        bool IsSlotBlocked(SlotModel slot);
        ItemStack TryAddToGrid(ItemStack itemStack);
        ItemStack TryAddToGridRebuilding(ItemStack itemStack);
        bool TryMoveFirstGridItemToEmptySlot(ItemType slotType);
        bool TryTakeFromSlot(SlotModel slot, out ItemStack itemStack);
        bool TryPlaceInSlot(SlotModel slot, ItemStack itemStack, out ItemStack remainderStack, out ItemStack replacedStack);
    }
}
