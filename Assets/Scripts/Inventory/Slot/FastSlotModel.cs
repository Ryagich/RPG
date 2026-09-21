using System;
using Inventory.Item;

namespace Inventory.Slot
{
    [Serializable]
    public sealed class FastSlotModel
    {
        public int Index { get; }
        public string ActionName { get; }
        public ItemConfig ItemConfig { get; private set; }

        public FastSlotModel(int index, string actionName)
        {
            Index = index;
            ActionName = actionName;
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
