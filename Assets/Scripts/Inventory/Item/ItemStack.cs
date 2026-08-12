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
        public string RuntimeTag { get; }

        public ItemStack(ItemConfig itemConfig, int count = 1, bool isRotated = false, string runtimeTag = null)
        {
            ItemConfig = itemConfig;
            Count = Math.Max(1, count);
            IsRotated = isRotated;
            RuntimeTag = runtimeTag;
        }

        public int MaxStack => ItemConfig?.MaxStack ?? 1;
        public bool IsFull => Count >= MaxStack;
        public float TotalWeight => (ItemConfig?.Weight ?? 0f) * Count;
        public int TotalPrice => (ItemConfig?.Price ?? 0) * Count;
        public Vector2Int Size => GetRotatedSize(ItemConfig?.Size ?? Vector2Int.one);
        public Vector2Int SizeInInventory => GetRotatedSize(ItemConfig?.SizeInInventory ?? Vector2Int.one);

        public bool CanStackWith(ItemStack other)
        {
            // Rotation affects only the grid footprint of the stack already placed in an inventory.
            // It must not prevent merging counts for the same item type into an existing partial stack.
            // Session-owned stacks must never merge with an identical player-owned item:
            // otherwise their provenance would be lost and cleanup could remove player loot.
            return other != null
                   && ItemConfig != null
                   && ItemConfig == other.ItemConfig
                   && string.Equals(RuntimeTag, other.RuntimeTag, StringComparison.Ordinal);
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
            return ItemConfig == null ? null : new ItemStack(ItemConfig, Count, IsRotated, RuntimeTag);
        }

        private Vector2Int GetRotatedSize(Vector2Int size)
        {
            return IsRotated ? new Vector2Int(size.y, size.x) : size;
        }
    }
}
