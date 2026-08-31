using System.Collections.Generic;
using Inventory.Inventories;
using Money;
using NPC;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container
{
    /// <summary>
    /// An NPC scope whose single character inventory is a permanent merchant inventory.
    /// </summary>
    public sealed class MerchantLifetimeScope : NpcLifetimeScope
    {
        [Header("Merchant Stock")]
        [SerializeField] private List<MerchantStockRule> stockRules = new();
        [SerializeField] private List<MerchantUniqueItemCountRange> uniqueItemCountRanges = new();
        [SerializeField] private List<MerchantItemAmountRange> itemAmountRanges = new();

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterInstance(new MerchantStockSettings(stockRules, uniqueItemCountRanges, itemAmountRanges));
            builder.RegisterEntryPoint<MerchantStockGenerator>().AsSelf();
        }

        protected override void RegisterCharacterInventory(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<MerchantInventory>()
                   .As<IInventory>()
                   .As<IEquipmentInventory>()
                   .As<ICharacterInventoryCapacity>()
                   .As<IInventoryOverflow>()
                   .AsSelf();
        }

        protected override void RegisterMoneyStorage(IContainerBuilder builder)
        {
            builder.Register(_ => MoneyStorage.CreateUnlimited(), Lifetime.Scoped).AsSelf();
        }
    }
}
