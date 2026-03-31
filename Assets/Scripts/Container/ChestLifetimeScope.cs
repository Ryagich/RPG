using Container.Chest;
using Interactable;
using Inventory.Inventories;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container
{
    public class ChestLifetimeScope : LifetimeScope
    {
        [SerializeField] private Character.CharacterInfo characterInfo;

        protected override void Configure(IContainerBuilder builder)
        {
            var interactable = gameObject.AddComponent<Interactable.Interactable>();
            interactable.InteractionMode = InteractionMode.Manual;
            
            builder.RegisterInstance(interactable);
            builder.RegisterInstance(characterInfo).AsSelf();
            builder.RegisterEntryPoint<ChestInventory>().As<IInventory>().AsSelf();
            builder.RegisterEntryPoint<ChestInteractableLogic>().AsSelf();
        }
    }
}