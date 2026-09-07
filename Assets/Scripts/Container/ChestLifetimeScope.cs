using Container.Chest;
using Interactable;
using Inventory;
using Inventory.Inventories;
using Inventory.Item;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container
{
    public class ChestLifetimeScope : LifetimeScope
    {
        [SerializeField] private Character.CharacterInfo characterInfo;
        [SerializeField] private InventoryConfig inventoryConfig;
        [Header("Inventory")]
        [SerializeField] private bool hasInfiniteInventorySize;
        [SerializeField] private ItemLootSetConfig itemLootSetConfig;
        [SerializeField] private ItemSetConfig itemSetConfig;
        [Header("Lid Animation")]
        [SerializeField] private bool animateLid = true;
        [SerializeField] private ChestLidAnimationSettings lidAnimationSettings = new();

        protected override void Configure(IContainerBuilder builder)
        {
            var interactable = gameObject.AddComponent<Interactable.Interactable>();
            interactable.InteractionMode = InteractionMode.Manual;
            builder.RegisterInstance(interactable);
            if (characterInfo != null) builder.RegisterInstance(characterInfo).AsSelf();
            if (inventoryConfig != null) builder.RegisterInstance(inventoryConfig).AsSelf();
            if (itemSetConfig != null) builder.RegisterInstance(itemSetConfig).AsSelf();
            if (itemLootSetConfig != null) builder.RegisterInstance(itemLootSetConfig).AsSelf();

            if (hasInfiniteInventorySize) builder.RegisterEntryPoint<UnlimitedChestInventory>().As<IInventory>().AsSelf();
            else builder.RegisterEntryPoint<ChestInventory>().As<IInventory>().AsSelf();

            if (itemSetConfig != null || itemLootSetConfig != null)
                builder.Register<ChestInitialInventoryLoadoutApplier>(Lifetime.Scoped).AsSelf().As<IStartable>();

            if (animateLid)
            {
                builder.RegisterInstance(lidAnimationSettings).AsSelf();
                builder.Register<ChestLidAnimator>(Lifetime.Scoped).AsSelf();
            }

            builder.RegisterEntryPoint<ChestInteractableLogic>().AsSelf();
        }
    }
}
