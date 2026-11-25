using Inventory.Item;
using Inventory.Slot;
using UnityEngine;

namespace UI.Inventory
{
    /// <summary>
    /// Recognizes two clicks on the same player inventory slot without deciding what the page
    /// should do with its item.
    /// </summary>
    public sealed class InventorySlotDoubleClickTracker
    {
        private SlotModel previousSlot;
        private ItemConfig previousItemConfig;
        private string previousRuntimeTag;
        private float previousClickTime = float.NegativeInfinity;

        public bool RegisterClick(SlotModel slot, float clickTime, float maximumIntervalSeconds)
        {
            if (slot?.ItemStack?.ItemConfig == null || maximumIntervalSeconds <= 0f)
            {
                Reset();
                return false;
            }

            var isDoubleClick = ReferenceEquals(previousSlot, slot)
                                && previousItemConfig == slot.ItemStack.ItemConfig
                                && previousRuntimeTag == slot.ItemStack.RuntimeTag
                                && clickTime >= previousClickTime
                                && clickTime - previousClickTime <= maximumIntervalSeconds;
            if (isDoubleClick)
            {
                Reset();
                return true;
            }

            previousSlot = slot;
            previousItemConfig = slot.ItemStack.ItemConfig;
            previousRuntimeTag = slot.ItemStack.RuntimeTag;
            previousClickTime = clickTime;
            return false;
        }

        public void Reset()
        {
            previousSlot = null;
            previousItemConfig = null;
            previousRuntimeTag = null;
            previousClickTime = float.NegativeInfinity;
        }
    }
}
