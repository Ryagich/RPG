using System;
using Inventory.Item;

namespace Inventory.Slot
{
    [Serializable]
    public class SlotModel
    {
        public ItemConfig ItemConfig;
        public ItemType ItemType;

        public SlotModel(ItemType type, ItemConfig itemConfig)
        {
            ItemType = type;
            ItemConfig = itemConfig;
        }
        
        public SlotModel(ItemType type)
        {
            ItemType = type;
        }
    }
}