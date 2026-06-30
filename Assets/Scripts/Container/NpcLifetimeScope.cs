using Combat;
using Inventory;
using Inventory.Inventories;
using Money;
using Movement;
using NPC;
using Player;
using Quests;
using StateMachine.Graph;
using Stats;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using VContainer.Unity;

namespace Container
{
    public class NpcLifetimeScope : LifetimeScope
    {
        [SerializeField] private Character.CharacterInfo characterInfo;
        [SerializeField] private InventoryConfig inventoryConfig;
        [SerializeField] private StateMachineGraph stateMachineGraph;
        [SerializeField, ReadOnlyInInspector] private float currentHp;
        [SerializeField, ReadOnlyInInspector] private string currentState = "Not Started";

        public StateMachineGraph StateMachineGraph => stateMachineGraph;
        public float CurrentHp => currentHp;
        public string CurrentState => currentState;

        public void SetRuntimeDebugInfo(float hp, string stateName)
        {
            currentHp = hp;
            currentState = string.IsNullOrWhiteSpace(stateName) ? "None" : stateName;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<CharacterController>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<Animator>().UnderTransform(transform).AsSelf();

            var ragdollController = GetComponent<PlayerRagdollController>() ?? gameObject.AddComponent<PlayerRagdollController>();
            builder.RegisterComponent(ragdollController).AsSelf();
            builder.RegisterComponentInHierarchy<CharacterVisualRoot>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<PlayerWeaponHandAnchor>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<PlayerWeaponAnimationEventReceiver>().UnderTransform(transform).AsSelf();
            var npcVision = GetComponent<NpcVision>() ?? gameObject.AddComponent<NpcVision>();
            builder.RegisterComponent(npcVision).AsSelf();
            var npcVisionSensor = GetComponent<NpcVisionSensor>() ?? gameObject.AddComponent<NpcVisionSensor>();
            builder.RegisterComponent(npcVisionSensor).AsSelf();
            var npcItemInterest = GetComponent<NpcItemInterest>() ?? gameObject.AddComponent<NpcItemInterest>();
            builder.RegisterComponent(npcItemInterest).AsSelf();
            var navMeshAgent = GetComponent<NavMeshAgent>() ?? gameObject.AddComponent<NavMeshAgent>();
            builder.RegisterComponent(navMeshAgent).AsSelf();
            builder.RegisterEntryPoint<NpcNavMeshController>().AsSelf();

            builder.RegisterInstance(transform);
            builder.RegisterInstance("NPC").Keyed("Scope ID");
            if (stateMachineGraph != null)
            {
                builder.RegisterInstance(stateMachineGraph).AsSelf();
                builder.RegisterEntryPoint<NpcStateMachineRunner>().AsSelf();
            }
            else
            {
                Debug.LogWarning($"{nameof(NpcLifetimeScope)} on {name} has no state machine graph assigned.", this);
            }

            if (characterInfo != null)
            {
                builder.RegisterInstance(characterInfo).AsSelf();
            }

            if (inventoryConfig != null)
            {
                builder.RegisterInstance(inventoryConfig).AsSelf();
            }

            var damageReceiverHost = GetComponent<DamageReceiverHost>() ?? gameObject.AddComponent<DamageReceiverHost>();
            builder.RegisterComponent(damageReceiverHost).AsSelf();

            builder.RegisterEntryPoint<PlayerGravity>().AsSelf();
            builder.Register<StatsController>(Lifetime.Singleton).AsSelf();
            builder.Register<StatFillers>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<StatsPeriodicChanger>().AsSelf();
            builder.RegisterEntryPoint<EquippedDefenseStatsChanger>().AsSelf();
            builder.Register<CharacterDamageReceiver>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<EquippedItemVisualController>().AsSelf();
            builder.RegisterEntryPoint<NpcWeaponInHandController>().AsSelf();

            builder.RegisterEntryPoint<PlayerInventory>().As<IInventory>().AsSelf();
            builder.Register<NpcWorldItemDropper>(Lifetime.Scoped).AsSelf();
            builder.Register<NpcInventoryPlanner>(Lifetime.Scoped).AsSelf();
            builder.Register<NpcItemPickupService>(Lifetime.Scoped).AsSelf();
            builder.Register<NpcEquippedWeaponDropService>(Lifetime.Scoped).AsSelf();
            builder.Register(_ => new MoneyStorage(0), Lifetime.Scoped).AsSelf();
            builder.Register<QuestController>(Lifetime.Scoped).AsSelf();

        }
    }
}
