using Container.Chest;
using Interactable;
using Inventory;
using Inventory.Inventories;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container
{
    public class ChestLifetimeScope : LifetimeScope
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
            builder.RegisterEntryPoint<ChestInventory>().As<IInventory>().AsSelf();
            builder.RegisterEntryPoint<ChestInteractableLogic>().AsSelf();
        }
    }
}