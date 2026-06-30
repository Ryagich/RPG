using Inventory.Item;
using Inventory.Slot;

namespace NPC
{
    public sealed class NpcInventoryDropSource
    {
        public readonly ItemInInventory GridItem;
        public readonly SlotModel Slot;
        public readonly ItemStack Snapshot;
        public readonly float Score;

        public NpcInventoryDropSource(ItemInInventory gridItem, ItemStack snapshot, float score)
        {
            GridItem = gridItem;
            Snapshot = snapshot;
            Score = score;
        }

        public NpcInventoryDropSource(SlotModel slot, ItemStack snapshot, float score)
        {
            Slot = slot;
            Snapshot = snapshot;
            Score = score;
        }
    }
}
