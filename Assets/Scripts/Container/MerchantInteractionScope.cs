using System.Collections.Generic;
using Character;
using Container.Dialogue;
using Dialogs.Graph;
using Factions;
using Interactable;
using Inventory.Inventories;
using Money;
using NPC;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container
{
    public sealed class MerchantInteractionScope : LifetimeScope
    {
        [SerializeField] private Character.CharacterInfo characterInfo;
        [SerializeField] private DialogGraph dialog;
        [SerializeField] private FactionConfig faction;
        [Header("Merchant Stock")]
        [SerializeField] private List<MerchantStockRule> stockRules = new();
        [SerializeField] private List<MerchantUniqueItemCountRange> uniqueItemCountRanges = new();
        [SerializeField] private List<MerchantItemAmountRange> itemAmountRanges = new();

        protected override void Configure(IContainerBuilder builder)
        {
            var interactable = gameObject.AddComponent<Interactable.Interactable>();
            interactable.InteractionMode = InteractionMode.Manual;

            builder.RegisterInstance(interactable);
            builder.RegisterInstance(characterInfo).AsSelf();
            if (dialog != null)
            {
                builder.RegisterInstance(dialog).AsSelf();
            }

            if (faction != null)
            {
                builder.RegisterInstance(faction).AsSelf();
            }

            builder.RegisterEntryPoint<MerchantInventory>()
                   .As<IInventory>()
                   .AsSelf();
            builder.Register(_ => MoneyStorage.CreateUnlimited(), Lifetime.Scoped).AsSelf();
            builder.RegisterInstance(new MerchantStockSettings(stockRules, uniqueItemCountRanges, itemAmountRanges));
            builder.RegisterEntryPoint<MerchantStockGenerator>().AsSelf();
            builder.RegisterEntryPoint<DialogueInteractableLogic>().AsSelf();
        }
    }
}
