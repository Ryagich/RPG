using CameraScripts;
using Interactable;
using Inventory;
using Inventory.Inventories;
using Movement;
using UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container
{
    public class PlayerLifetimeScope : LifetimeScope
    {
        [SerializeField] private CanvasLifetimeScope canvasLifetimeScope;
        [SerializeField] private Character.CharacterInfo characterInfo;
        [SerializeField] private InventoryConfig inventoryConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<CharacterController>().AsSelf();
            
            builder.RegisterInstance(transform);
            builder.RegisterInstance("Player").Keyed("Scope ID"); 
            
            builder.RegisterInstance(characterInfo).AsSelf();
            if (inventoryConfig != null)
            {
                builder.RegisterInstance(inventoryConfig).AsSelf();
            }
            
            var founder = gameObject.AddComponent<InteractableFounder>();
            builder.RegisterComponent(founder).AsSelf();
            
            builder.RegisterBuildCallback(_ =>
                                          {
                                              CreateChildFromPrefab(canvasLifetimeScope);
                                          });
            
            builder.RegisterEntryPoint<CameraMotor>().AsSelf();
            builder.RegisterEntryPoint<PlayerGravity>().AsSelf();
            builder.RegisterEntryPoint<PlayerMovementController>().AsSelf();
            builder.RegisterEntryPoint<PlayerMovement>().AsSelf();
            builder.RegisterEntryPoint<PlayerInteractableLogic>().AsSelf();
            builder.RegisterEntryPoint<ItemHolderInteractableLogic>().AsSelf();
            
            builder.RegisterEntryPoint<PlayerInventory>().As<IInventory>().AsSelf();
            builder.RegisterEntryPoint<InventoryHandController>().AsSelf();
        }
    }
}