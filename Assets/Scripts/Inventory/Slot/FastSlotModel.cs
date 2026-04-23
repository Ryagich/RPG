using System;
using Inventory.Item;

namespace Inventory.Slot
{
    [Serializable]
    public sealed class FastSlotModel
    {
        public int Index { get; }
        public string ActionName { get; }
        public string DisplayName { get; }
        public ItemConfig ItemConfig { get; private set; }

        public FastSlotModel(int index, string actionName, string displayName)
        {
            Index = index;
            ActionName = actionName;
            DisplayName = displayName;
        }

        public void Assign(ItemConfig itemConfig)
        {
            ItemConfig = itemConfig;
        }

        public void Clear()
        {
            ItemConfig = null;
        }
    }
}
