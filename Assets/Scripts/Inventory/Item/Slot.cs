using System;

namespace Inventory.Item
{
    [Serializable]
    public class Slot
    {
        public ItemConfig ItemConfig;
        public ItemType ItemType;
    }
}