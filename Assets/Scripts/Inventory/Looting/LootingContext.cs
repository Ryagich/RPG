using Inventory.Inventories;

namespace Inventory.Looting
{
    public class LootingContext
    {
        public IInventory CurrentTargetInventory { get; private set; }

        public void SetTarget(IInventory inventory)
        {
            CurrentTargetInventory = inventory;
        }

        public void Clear()
        {
            CurrentTargetInventory = null;
        }
    }
}