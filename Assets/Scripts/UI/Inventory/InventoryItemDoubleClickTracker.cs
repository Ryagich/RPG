using System;
using Inventory.Inventories;
using Inventory.Item;
using UnityEngine;

namespace UI.Inventory
{
    /// <summary>
    /// Recognizes two clicks on the same rendered inventory item without owning the action that
    /// follows. Item identity is stored by inventory, stack provenance and grid position because
    /// regular click-and-release may recreate the item entry between clicks.
    /// </summary>
    public sealed class InventoryItemDoubleClickTracker
    {
        private IInventory previousInventory;
        private ItemConfig previousItemConfig;
        private string previousRuntimeTag;
        private Matrix4x4 previousPosition;
        private float previousClickTime = float.NegativeInfinity;

        public bool RegisterClick(IInventory inventory, ItemInInventory item, float clickTime, float maximumIntervalSeconds)
        {
            if (inventory == null || item?.ItemStack?.ItemConfig == null || maximumIntervalSeconds <= 0f)
            {
                Reset();
                return false;
            }

            var isDoubleClick = ReferenceEquals(previousInventory, inventory)
                                && previousItemConfig == item.ItemStack.ItemConfig
                                && string.Equals(previousRuntimeTag, item.ItemStack.RuntimeTag, StringComparison.Ordinal)
                                && previousPosition == item.Position
                                && clickTime >= previousClickTime
                                && clickTime - previousClickTime <= maximumIntervalSeconds;
            if (isDoubleClick)
            {
                Reset();
                return true;
            }

            previousInventory = inventory;
            previousItemConfig = item.ItemStack.ItemConfig;
            previousRuntimeTag = item.ItemStack.RuntimeTag;
            previousPosition = item.Position;
            previousClickTime = clickTime;
            return false;
        }

        public void Reset()
        {
            previousInventory = null;
            previousItemConfig = null;
            previousRuntimeTag = null;
            previousPosition = default;
            previousClickTime = float.NegativeInfinity;
        }
    }
}
