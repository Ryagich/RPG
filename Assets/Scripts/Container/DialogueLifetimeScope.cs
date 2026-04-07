using Container.Dialogue;
using Interactable;
using Inventory;
using Inventory.Inventories;
using Money;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container
{
    public class DialogueLifetimeScope : LifetimeScope
    {
        [SerializeField] private Character.CharacterInfo characterInfo;
        [SerializeField] private InventoryConfig inventoryConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            var interactable = gameObject.AddComponent<Interactable.Interactable>();
            interactable.InteractionMode = InteractionMode.Manual;

            builder.RegisterInstance(interactable);
            builder.RegisterInstance(characterInfo).AsSelf();
            if (inventoryConfig != null)
            {
                builder.RegisterInstance(inventoryConfig).AsSelf();
            }
            
            builder.Register(_ => new MoneyStorage(100), Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<ChestInventory>().As<IInventory>().AsSelf();
            builder.RegisterEntryPoint<DialogueInteractableLogic>().AsSelf();
        }
    }
}