using Inventory.Inventories;
using Inventory.Item;
using UnityEngine;
using VContainer;
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

    /// <summary>
    /// Applies a faction NPC's guaranteed item set and then its independent random loot entries.
    /// Kept separate from the player item-set applier so the NPC-only faction rule does not leak
    /// into the generic initial inventory path.
    /// </summary>
    public sealed class NpcInitialInventoryLoadoutApplier : IStartable
    {
        private readonly IInventory inventory;
        private readonly IObjectResolver resolver;

        public NpcInitialInventoryLoadoutApplier(IInventory inventory, IObjectResolver resolver)
        {
            this.inventory = inventory;
            this.resolver = resolver;
        }

        public void Start()
        {
            if (inventory == null)
            {
                return;
            }

            var itemSetConfig = resolver.TryResolve<ItemSetConfig>(out var resolvedItemSetConfig)
                ? resolvedItemSetConfig
                : null;
            var itemLootSetConfig = resolver.TryResolve<ItemLootSetConfig>(out var resolvedItemLootSetConfig)
                ? resolvedItemLootSetConfig
                : null;

            ApplyItemSet(itemSetConfig);
            ApplyRandomLoot(itemLootSetConfig);
        }

        private void ApplyItemSet(ItemSetConfig itemSetConfig)
        {
            if (itemSetConfig?.ItemConfigs == null)
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

        private void ApplyRandomLoot(ItemLootSetConfig itemLootSetConfig)
        {
            if (itemLootSetConfig?.Entries == null)
            {
                return;
            }

            foreach (var entry in itemLootSetConfig.Entries)
            {
                if (entry == null || !entry.IsValid || Random.value >= entry.Chance)
                {
                    continue;
                }

                var count = Random.Range(entry.MinAmount, entry.MaxAmount + 1);
                // TryAdd returns the unplaced remainder. It is intentionally ignored: failed units
                // are not created as world loot and cannot exceed this NPC's inventory capacity.
                inventory.TryAdd(new ItemStack(entry.ItemConfig, count));
            }
        }
    }
}
