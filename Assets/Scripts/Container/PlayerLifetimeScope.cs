using CameraScripts;
using Interactable;
using Inventory;
using Inventory.Inventories;
using Money;
using Movement;
using Quests;
using Stats;
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
            builder.RegisterComponentInHierarchy<Animator>().AsSelf();
            builder.RegisterComponentInHierarchy<CharacterVisualRoot>().AsSelf();
            builder.RegisterComponentInHierarchy<PlayerWeaponHandAnchor>().AsSelf();
            builder.RegisterComponentInHierarchy<PlayerWeaponAnimationEventReceiver>().AsSelf();

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
            builder.RegisterEntryPoint<PlayerAnimationController>().AsSelf();
            builder.RegisterEntryPoint<PlayerInteractableLogic>().AsSelf();
            builder.RegisterEntryPoint<ItemHolderInteractableLogic>().AsSelf();
            builder.Register<StatsController>(Lifetime.Singleton).AsSelf();
            builder.Register<StatFillers>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<StatsPeriodicChanger>().AsSelf();
            builder.RegisterEntryPoint<StaminaPeriodicChanger>().AsSelf();
            builder.RegisterEntryPoint<StaminaMovementChanger>().AsSelf();
            builder.RegisterEntryPoint<EquippedDefenseStatsChanger>().AsSelf();
            builder.RegisterEntryPoint<EquippedItemVisualController>().AsSelf();

            builder.RegisterEntryPoint<PlayerInventory>().As<IInventory>().AsSelf();
            builder.Register(_ => new MoneyStorage(112), Lifetime.Scoped).AsSelf();
            builder.Register<QuestController>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<InventoryHandController>().AsSelf();
            builder.RegisterEntryPoint<PlayerFastSlotsController>().AsSelf();
            builder.RegisterEntryPoint<PlayerWeaponInHandController>().AsSelf();
        }
    }
}
