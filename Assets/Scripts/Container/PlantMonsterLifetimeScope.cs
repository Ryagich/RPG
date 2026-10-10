using StateMachine.Graph;
using StateMachine;
using Combat;
using Factions;
using InfinityPBR;
using Inventory;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Looting;
using Monsters;
using NPC;
using Stats;
using TargetLock;
using UI;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using VContainer.Unity;
using RuntimeStateMachine = StateMachine.StateMachine;

namespace Container
{
    /// <summary>
    /// Owns the runtime dependencies authored specifically for PlantMonster_1.
    /// Behaviour systems are intentionally not started here: the graph is a future extension
    /// point and remains inert until monster states are authored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlantMonsterLifetimeScope : LifetimeScope, IFactionMember
    {
        [Header("Stats")]
        [SerializeField] private StatsConfig statsConfig;
        [Tooltip("When enabled, water and food decrease periodically. HP periodic behaviour remains active.")]
        [SerializeField] private bool consumeAdditionalStatsOverTime;

        [Header("State Machine")]
        [SerializeField] private StateMachineGraph stateMachineGraph;
        [SerializeField] private PlantMonsterSearchConfig searchConfig;

        [Header("Perception")]
        [SerializeField] private NpcVisionConfig visionConfig;
        [SerializeField] private NpcVisionConfig combatVisionConfig;
        [SerializeField] private FactionConfig faction;
        [SerializeField] private bool treatFactionlessTargetsAsHostile = true;

        [Header("Combat")]
        [SerializeField, Min(0f)] private float contactDamage = 10f;
        [Tooltip("Assign the trigger collider used by the monster's contact attack. The scope adds the damage-zone component to this collider's GameObject.")]
        [SerializeField] private Collider attackDamageCollider;

        [Header("Initial Inventory")]
        [Tooltip("Optional guaranteed loadout. If empty, a set is selected from the monster faction.")]
        [SerializeField] private ItemSetConfig itemSetConfigOverride;

        [Header("Corpse Loot")]
        [Tooltip("Assign the manually activated interaction zone used to loot this monster after death.")]
        [SerializeField] private Interactable.Interactable corpseLootInteractable;

        public FactionConfig Faction => faction;
        public bool TreatFactionlessTargetsAsHostile => treatFactionlessTargetsAsHostile;

        protected override void Configure(IContainerBuilder builder)
        {
            if (statsConfig == null)
            {
                Debug.LogError($"{nameof(PlantMonsterLifetimeScope)} on {name} requires its own {nameof(StatsConfig)}.", this);
                return;
            }

            if (stateMachineGraph == null)
            {
                Debug.LogError($"{nameof(PlantMonsterLifetimeScope)} on {name} requires a {nameof(StateMachineGraph)}.", this);
                return;
            }

            if (searchConfig == null)
            {
                Debug.LogError($"{nameof(PlantMonsterLifetimeScope)} on {name} requires a {nameof(PlantMonsterSearchConfig)}.", this);
                return;
            }

            if (visionConfig == null)
            {
                Debug.LogError($"{nameof(PlantMonsterLifetimeScope)} on {name} requires a {nameof(NpcVisionConfig)}.", this);
                return;
            }

            if (combatVisionConfig == null)
            {
                Debug.LogError($"{nameof(PlantMonsterLifetimeScope)} on {name} requires a combat {nameof(NpcVisionConfig)}.", this);
                return;
            }

            // StaminaConfig is inherited from the project scope, just like the NPC scope. Only
            // mutable balance data that belongs to this monster is overridden locally.
            builder.RegisterInstance(statsConfig).AsSelf();
            builder.RegisterInstance(stateMachineGraph).AsSelf();
            builder.RegisterInstance(searchConfig).AsSelf();
            builder.RegisterInstance(visionConfig).AsSelf();
            builder.RegisterInstance(transform).AsSelf();
            if (faction != null)
            {
                builder.RegisterInstance(faction).AsSelf();
            }

            RegisterInitialInventoryLoadout(builder);
            RegisterCharacterInventory(builder);
            RegisterCorpseLoot(builder);

            var targetLockTarget = GetComponent<TargetLockTarget>() ?? gameObject.AddComponent<TargetLockTarget>();
            builder.RegisterComponent(targetLockTarget).AsSelf();

            var damageReceiverHost = GetComponent<DamageReceiverHost>() ?? gameObject.AddComponent<DamageReceiverHost>();
            builder.RegisterComponent(damageReceiverHost).AsSelf();
            builder.Register<StatsController>(Lifetime.Singleton).AsSelf();
            builder.RegisterInstance(new AdditionalStatsPeriodicDrainPolicy(consumeAdditionalStatsOverTime))
                   .As<IAdditionalStatsPeriodicDrainPolicy>();
            builder.RegisterEntryPoint<StatsPeriodicChanger>().AsSelf();
            builder.Register<CharacterDamageReceiver>(Lifetime.Scoped).AsSelf();

            builder.RegisterComponentInHierarchy<Animator>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<NavMeshAgent>().UnderTransform(transform).AsSelf();
            builder.Register<PlantMonsterAnimationController>(Lifetime.Scoped).AsSelf().As<IStartable>();
            builder.RegisterEntryPoint<PlantMonsterNavMeshController>().AsSelf();
            builder.Register<CharacterActionState>(Lifetime.Scoped).AsSelf();

            var vision = GetComponent<NpcVision>() ?? gameObject.AddComponent<NpcVision>();
            builder.RegisterComponent(vision).AsSelf();
            var visionSensor = GetVisionSensor();
            if (visionSensor == null)
            {
                Debug.LogError($"{nameof(PlantMonsterLifetimeScope)} on {name} requires a root {nameof(CharacterVisionSensor)}.", this);
                return;
            }

            builder.RegisterComponent(visionSensor).AsSelf();
            builder.RegisterInstance(CreateCombatVision()).AsSelf();
            builder.RegisterEntryPoint<PlantMonsterTargetingController>().AsSelf();

            var damageZone = GetOrCreateDamageZone();
            builder.RegisterComponent(damageZone).AsSelf();
            builder.RegisterInstance(new PlantMonsterAttackSettings(contactDamage)).AsSelf();
            builder.Register<PlantMonsterCombatController>(Lifetime.Scoped).AsSelf();
            builder.Register<PlantMonsterHitReactionController>(Lifetime.Scoped)
                   .AsSelf()
                   .As<ICharacterHitReactionController>()
                   .As<IStartable>()
                   .As<ITickable>()
                   .As<System.IDisposable>();
            builder.RegisterEntryPoint<PlantMonsterStateMachineRunner>().AsSelf();

            var animationEvents = GetComponent<PlantMonsterAnimationEventReceiver>()
                                  ?? gameObject.AddComponent<PlantMonsterAnimationEventReceiver>();
            builder.RegisterComponent(animationEvents).AsSelf();

            // The scope composes the visual adapter; the adapter, rather than this scope,
            // owns all calls into the asset's presentation components.
            builder.RegisterComponentInHierarchy<BlendShapesManager>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<BlendShapesPresetManager>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<PrefabAndObjectManager>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<ColorShiftRuntime>().UnderTransform(transform).AsSelf();
            builder.RegisterEntryPoint<PlantMonsterVisualController>().AsSelf();

            builder.Register<StateMachineContext>(Lifetime.Scoped).AsSelf();
            builder.Register<RuntimeStateMachine>(Lifetime.Singleton).AsSelf();
        }

        private PlantMonsterDamageZone GetOrCreateDamageZone()
        {
            var host = attackDamageCollider != null ? attackDamageCollider.gameObject : gameObject;
            var damageZone = host.GetComponent<PlantMonsterDamageZone>()
                             ?? host.AddComponent<PlantMonsterDamageZone>();
            damageZone.Configure(attackDamageCollider);
            return damageZone;
        }

        private CharacterVisionSensor GetVisionSensor()
        {
            return GetComponent<CharacterVisionSensor>();
        }

        private PlantMonsterCombatVisionController CreateCombatVision()
        {
            const string combatVisionObjectName = "Combat Vision";
            var combatVisionTransform = transform.Find(combatVisionObjectName);
            if (combatVisionTransform == null)
            {
                var createdCombatVisionObject = new GameObject(combatVisionObjectName);
                combatVisionTransform = createdCombatVisionObject.transform;
                combatVisionTransform.SetParent(transform, false);
            }

            var combatVisionObject = combatVisionTransform.gameObject;
            var combatVision = combatVisionObject.GetComponent<NpcVision>()
                               ?? combatVisionObject.AddComponent<NpcVision>();
            var combatVisionSensor = combatVisionObject.GetComponent<PlantMonsterCombatVisionSensor>()
                                     ?? combatVisionObject.AddComponent<PlantMonsterCombatVisionSensor>();
            combatVision.Construct(combatVisionConfig);
            combatVisionSensor.Construct(combatVisionConfig);

            var controller = new PlantMonsterCombatVisionController(
                combatVisionObject,
                combatVision,
                combatVisionSensor);
            controller.SetEnabled(false);
            return controller;
        }

        private void RegisterInitialInventoryLoadout(IContainerBuilder builder)
        {
            var itemSetConfig = itemSetConfigOverride ?? faction?.GetRandomItemSetConfig();
            var itemLootSetConfig = faction?.GetRandomItemLootSetConfig();
            if (itemSetConfig == null && itemLootSetConfig == null)
            {
                return;
            }

            if (itemSetConfig != null)
            {
                builder.RegisterInstance(itemSetConfig).AsSelf();
            }

            if (itemLootSetConfig != null)
            {
                builder.RegisterInstance(itemLootSetConfig).AsSelf();
            }

            builder.RegisterEntryPoint<NpcInitialInventoryLoadoutApplier>(Lifetime.Scoped).AsSelf();
        }

        private static void RegisterCharacterInventory(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<PlayerInventory>()
                   .As<IInventory>()
                   .As<IEquipmentInventory>()
                   .As<ICharacterInventoryCapacity>()
                   .As<IInventoryOverflow>()
                   .AsSelf();
        }

        private void RegisterCorpseLoot(IContainerBuilder builder)
        {
            if (corpseLootInteractable == null)
            {
                return;
            }

            corpseLootInteractable.InteractionMode = Interactable.InteractionMode.Manual;
            builder.RegisterComponent(corpseLootInteractable).AsSelf();

            var corpseLootController = GetComponent<CorpseLootController>()
                                        ?? gameObject.AddComponent<CorpseLootController>();
            corpseLootController.ConfigureLootZone(corpseLootInteractable.gameObject);
            builder.RegisterComponent(corpseLootController).AsSelf();

            var availability = corpseLootInteractable.GetComponent<CorpseLootAvailability>()
                               ?? corpseLootInteractable.gameObject.AddComponent<CorpseLootAvailability>();
            builder.RegisterComponent(availability).AsSelf();
            builder.RegisterEntryPoint<CorpseLootInteractableLogic>().AsSelf();
        }
    }
}
