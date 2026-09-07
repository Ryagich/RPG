using Inventory;
using Inventory.Inventories;
using Inventory.Item;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container.Chest
{
    /// <summary>Applies optional authored chest stock after its selected inventory is created.</summary>
    public sealed class ChestInitialInventoryLoadoutApplier : IStartable
    {
        private readonly IInventory inventory;
        private readonly IObjectResolver resolver;
        public ChestInitialInventoryLoadoutApplier(IInventory inventory, IObjectResolver resolver) { this.inventory = inventory; this.resolver = resolver; }

        public void Start()
        {
            if (inventory == null) return;
            if (resolver.TryResolve<ItemSetConfig>(out var set) && set?.ItemConfigs != null)
                foreach (var item in set.ItemConfigs) if (item != null) inventory.TryAdd(item);

            if (!resolver.TryResolve<ItemLootSetConfig>(out var loot) || loot?.Entries == null) return;
            foreach (var entry in loot.Entries)
                if (entry != null && entry.IsValid && Random.value < entry.Chance)
                    inventory.TryAdd(new ItemStack(entry.ItemConfig, Random.Range(entry.MinAmount, entry.MaxAmount + 1)));
        }
    }
}
