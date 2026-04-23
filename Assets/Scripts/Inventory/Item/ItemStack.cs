using System;
using UnityEngine;

namespace Inventory.Item
{
    [Serializable]
    public class ItemStack
    {
        public ItemConfig ItemConfig;
        public int Count;
        public bool IsRotated;

        public ItemStack(ItemConfig itemConfig, int count = 1, bool isRotated = false)
        {
            ItemConfig = itemConfig;
            Count = Math.Max(1, count);
            IsRotated = isRotated;
        }

        public int MaxStack => ItemConfig?.MaxStack ?? 1;
        public bool IsFull => Count >= MaxStack;
        public float TotalWeight => (ItemConfig?.Weight ?? 0f) * Count;
        public int TotalPrice => (ItemConfig?.Price ?? 0) * Count;
        public Vector2Int Size => GetRotatedSize(ItemConfig?.Size ?? Vector2Int.one);
        public Vector2Int SizeInInventory => GetRotatedSize(ItemConfig?.SizeInInventory ?? Vector2Int.one);

        public bool CanStackWith(ItemStack other)
        {
            return other != null && ItemConfig != null && ItemConfig == other.ItemConfig && IsRotated == other.IsRotated;
        }

        public bool CanRotate()
        {
            return ItemConfig != null && ItemConfig.Size.x != ItemConfig.Size.y;
        }

        public void Rotate90()
        {
            if (!CanRotate())
            {
                return;
            }

            IsRotated = !IsRotated;
        }

        public ItemStack Clone()
        {
            return ItemConfig == null ? null : new ItemStack(ItemConfig, Count, IsRotated);
        }

        private Vector2Int GetRotatedSize(Vector2Int size)
        {
            return IsRotated ? new Vector2Int(size.y, size.x) : size;
        }
    }
}
