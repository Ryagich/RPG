using System;

namespace Inventory.Item
{
    [Serializable]
    public class ItemStack
    {
        public ItemConfig ItemConfig;
        public int Count;

        public ItemStack(ItemConfig itemConfig, int count = 1)
        {
            ItemConfig = itemConfig;
            Count = Math.Max(1, count);
        }

        public int MaxStack => ItemConfig?.MaxStack ?? 1;
        public bool IsFull => Count >= MaxStack;
        public float TotalWeight => (ItemConfig?.Weight ?? 0f) * Count;
        public int TotalPrice => (ItemConfig?.Price ?? 0) * Count;

        public bool CanStackWith(ItemStack other)
        {
            return other != null && ItemConfig != null && ItemConfig == other.ItemConfig;
        }

        public ItemStack Clone()
        {
            return ItemConfig == null ? null : new ItemStack(ItemConfig, Count);
        }
    }
}
