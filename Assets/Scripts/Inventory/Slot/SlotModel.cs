using System;
using Inventory.Item;

namespace Inventory.Slot
{
    public enum SlotStackLimitType
    {
        SingleItem = 0,
        ItemConfigMaxStack = 1
    }

    [Serializable]
    public class SlotModel
    {
        public ItemStack ItemStack;
        public ItemType ItemType;
        public SlotStackLimitType StackLimitType { get; }

        public ItemConfig ItemConfig => ItemStack?.ItemConfig;
        public int Count => ItemStack?.Count ?? 0;

        public SlotModel(ItemType type, SlotStackLimitType stackLimitType, ItemStack itemStack)
        {
            ItemType = type;
            StackLimitType = stackLimitType;
            ItemStack = itemStack;
        }
        
        public SlotModel(ItemType type, SlotStackLimitType stackLimitType)
        {
            ItemType = type;
            StackLimitType = stackLimitType;
        }

        public int GetMaxStack(ItemConfig itemConfig)
        {
            if (itemConfig == null)
            {
                return 1;
            }

            return StackLimitType == SlotStackLimitType.ItemConfigMaxStack
                ? itemConfig.MaxStack
                : 1;
        }
    }
}
