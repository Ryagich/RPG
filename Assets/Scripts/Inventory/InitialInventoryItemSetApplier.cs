using Inventory.Inventories;
using Inventory.Item;
using VContainer.Unity;

namespace Inventory
{
    public sealed class InitialInventoryItemSetApplier : IStartable
    {
        private readonly IInventory inventory;
        private readonly ItemSetConfig itemSetConfig;

        public InitialInventoryItemSetApplier(IInventory inventory, ItemSetConfig itemSetConfig)
        {
            this.inventory = inventory;
            this.itemSetConfig = itemSetConfig;
        }

        public void Start()
        {
            if (inventory == null || itemSetConfig?.ItemConfigs == null)
            {
                return;
            }

            foreach (var itemConfig in itemSetConfig.ItemConfigs)
            {
                if (itemConfig != null)
                {
                    inventory.TryAdd(itemConfig);
                }
            }
        }
    }
}
