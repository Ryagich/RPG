using CameraScripts;
using Combat;
using Interactable;
using Inventory;
using Inventory.Inventories;
using Money;
using Movement;
using Player;
using Quests;
using Stats;
using TargetLock;
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
            builder.RegisterComponentInHierarchy<CharacterController>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<Animator>().UnderTransform(transform).AsSelf();
            var ragdollController = GetComponent<PlayerRagdollController>() ?? gameObject.AddComponent<PlayerRagdollController>();
            builder.RegisterComponent(ragdollController).AsSelf();
            builder.RegisterComponentInHierarchy<CharacterVisualRoot>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<PlayerWeaponHandAnchor>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<PlayerWeaponAnimationEventReceiver>().UnderTransform(transform).AsSelf();

            builder.RegisterInstance(transform);
            builder.RegisterInstance("Player").Keyed("Scope ID");

            builder.RegisterInstance(characterInfo).AsSelf();
            if (inventoryConfig != null)
            {
                builder.RegisterInstance(inventoryConfig).AsSelf();
            }

            var founder = gameObject.AddComponent<InteractableFounder>();
            builder.RegisterComponent(founder).AsSelf();

            var damageReceiverHost = GetComponent<DamageReceiverHost>() ?? gameObject.AddComponent<DamageReceiverHost>();
            builder.RegisterComponent(damageReceiverHost).AsSelf();

            builder.RegisterBuildCallback(_ =>
                                          {
                                              CreateChildFromPrefab(canvasLifetimeScope);
                                          });

            builder.RegisterEntryPoint<CameraMotor>().AsSelf();
            builder.RegisterEntryPoint<TargetLockController>().AsSelf();
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

            // Player and NPC must keep the same character systems; only the control source differs:
            // player input drives these systems for Player, while the state machine drives them for NPC.
            builder.Register<CharacterActionState>(Lifetime.Scoped).AsSelf();
            builder.Register<CharacterRootMotionController>(Lifetime.Scoped)
                   .AsSelf()
                   .As<IStartable>()
                   .As<System.IDisposable>();
            builder.Register<PlayerHitReactionController>(Lifetime.Scoped)
                   .AsSelf()
                   .As<ICharacterHitReactionController>()
                   .As<IStartable>()
                   .As<ITickable>()
                   .As<System.IDisposable>();
            builder.Register<CharacterDamageReceiver>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<EquippedItemVisualController>().AsSelf();
            builder.RegisterEntryPoint<PlayerDeathController>().AsSelf();

            builder.RegisterEntryPoint<PlayerInventory>().As<IInventory>().AsSelf();
            builder.Register<CharacterWorldItemDropper>(Lifetime.Scoped).AsSelf();
            builder.Register<EquippedWeaponDropService>(Lifetime.Scoped).AsSelf();
            builder.Register(_ => new MoneyStorage(112), Lifetime.Scoped).AsSelf();
            builder.Register<QuestController>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<InventoryHandController>().AsSelf();
            builder.RegisterEntryPoint<PlayerFastSlotsController>().AsSelf();
            builder.Register<PlayerWeaponInHandController>(Lifetime.Scoped)
                   .AsSelf()
                   .As<IEquippedWeaponVisual>()
                   .As<IStartable>()
                   .As<ITickable>()
                   .As<System.IDisposable>();
        }
    }
}
