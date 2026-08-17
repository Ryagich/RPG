using Combat;
using Dialogs.Graph;
using Factions;
using Inventory;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Looting;
using Money;
using Movement;
using NPC;
using Player;
using Quests;
using Quests.MapTargets;
using StateMachine.Graph;
using Stats;
using TargetLock;
using UnityEngine;
using UnityEngine.AI;
using GameAudio;
using VContainer;
using VContainer.Unity;

namespace Container
{
    public class NpcLifetimeScope : LifetimeScope
    {
        private const string DialogueZoneName = "Dialogue Interactable Zone";
        private const string ForcedDialogueInteractableKey = "Forced Dialogue Interactable";

        [SerializeField] private Character.CharacterInfo characterInfo;
        [SerializeField] private InventoryConfig inventoryConfig;
        [Header("Visuals")]
        [SerializeField] private CharacterDefaultVisualConfig defaultVisualConfig;
        [Header("Dialogue")]
        [SerializeField] private bool canTalk = true;
        [SerializeField] private DialogGraph dialog;
        [SerializeField] private StateMachineGraph stateMachineGraph;
        [Header("Faction")]
        [SerializeField] private FactionConfig faction;
        [Header("Stats")]
        [Tooltip("When enabled, water and food decrease periodically like they do for the player.")]
        [SerializeField] private bool consumeAdditionalStatsOverTime;
        [Header("Initial Inventory")]
        [Tooltip("If empty, this NPC randomly selects an item set from its faction.")]
        [SerializeField] private ItemSetConfig itemSetConfigOverride;
        [Header("Combat AI")]
        [Tooltip("If empty, this NPC uses the combat profile assigned to its faction.")]
        [SerializeField] private NpcCombatProfile combatProfileOverride;
        [SerializeField, Min(0.1f)] private float dialogueInteractionRadius = 1.8f;
        [SerializeField] private Interactable.Interactable forcedDialogueInteractable;
        [SerializeField, ReadOnlyInInspector] private float currentHp;
        [SerializeField, ReadOnlyInInspector] private string currentState = "Not Started";
        private NpcCombatProfile assignedCombatProfile;

        public StateMachineGraph StateMachineGraph => stateMachineGraph;
        public FactionConfig Faction => faction;
        public NpcCombatProfile CombatProfile => assignedCombatProfile ?? combatProfileOverride ?? faction?.CombatProfile;
        public bool CanTalk => canTalk;
        public float CurrentHp => currentHp;
        public string CurrentState => currentState;

        public void SetRuntimeDebugInfo(float hp, string stateName)
        {
            currentHp = hp;
            currentState = string.IsNullOrWhiteSpace(stateName) ? "None" : stateName;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            assignedCombatProfile = combatProfileOverride ?? faction?.GetRandomCombatProfile();

            builder.RegisterComponentInHierarchy<CharacterController>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<Animator>().UnderTransform(transform).AsSelf();

            var ragdollController = GetComponent<PlayerRagdollController>() ?? gameObject.AddComponent<PlayerRagdollController>();
            builder.RegisterComponent(ragdollController).AsSelf();
            builder.RegisterComponentInHierarchy<CharacterVisualRoot>().UnderTransform(transform).AsSelf();
            var corpseLootController = GetComponent<CorpseLootController>() ?? gameObject.AddComponent<CorpseLootController>();
            builder.RegisterComponent(corpseLootController).AsSelf();
            builder.RegisterComponentInHierarchy<PlayerWeaponHandAnchor>().UnderTransform(transform).AsSelf();
            builder.RegisterComponentInHierarchy<PlayerWeaponAnimationEventReceiver>().UnderTransform(transform).AsSelf();
            var npcVision = GetComponent<NpcVision>() ?? gameObject.AddComponent<NpcVision>();
            builder.RegisterComponent(npcVision).AsSelf();
            var npcVisionSensor = GetComponent<NpcVisionSensor>() ?? gameObject.AddComponent<NpcVisionSensor>();
            builder.RegisterComponent(npcVisionSensor).AsSelf();
            var npcItemInterest = GetComponent<NpcItemInterest>() ?? gameObject.AddComponent<NpcItemInterest>();
            builder.RegisterComponent(npcItemInterest).AsSelf();
            var targetLockTarget = GetComponent<TargetLockTarget>() ?? gameObject.AddComponent<TargetLockTarget>();
            builder.RegisterComponent(targetLockTarget).AsSelf();
            var interactable = GetComponent<Interactable.Interactable>() ?? gameObject.AddComponent<Interactable.Interactable>();
            interactable.InteractionMode = Interactable.InteractionMode.Manual;
            builder.RegisterComponent(interactable).AsSelf();
            var dialogueAvailability = GetComponent<NpcDialogueAvailability>() ?? gameObject.AddComponent<NpcDialogueAvailability>();
            builder.RegisterComponent(dialogueAvailability).AsSelf();
            var questMapTarget = GetComponent<QuestMapTarget>();
            if (questMapTarget != null)
            {
                builder.RegisterComponent(questMapTarget).AsSelf();
            }

            EnsureDialogueInteractionZone();
            bool hasForcedDialogueZone = RegisterForcedDialogueZone(builder);
            var navMeshAgent = GetComponent<NavMeshAgent>() ?? gameObject.AddComponent<NavMeshAgent>();
            builder.RegisterComponent(navMeshAgent).AsSelf();
            builder.RegisterEntryPoint<NpcNavMeshController>().AsSelf().As<IStaminaMovementState>();
            builder.RegisterEntryPoint<NpcFootstepPlayer>().AsSelf();

            builder.RegisterInstance(transform);
            builder.RegisterInstance("NPC").Keyed("Scope ID");
            builder.RegisterInstance(canTalk).Keyed("Can Talk");
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

            if (dialog != null)
            {
                builder.RegisterInstance(dialog).AsSelf();
            }

            if (inventoryConfig != null)
            {
                builder.RegisterInstance(inventoryConfig).AsSelf();
            }

            if (defaultVisualConfig != null)
            {
                builder.RegisterInstance(defaultVisualConfig).AsSelf();
            }

            if (faction != null)
            {
                builder.RegisterInstance(faction).AsSelf();
            }

            var initialItemSetConfig = itemSetConfigOverride ?? faction?.GetRandomItemSetConfig();
            if (initialItemSetConfig != null)
            {
                builder.RegisterInstance(initialItemSetConfig).AsSelf();
                builder.RegisterEntryPoint<InitialInventoryItemSetApplier>().AsSelf();
            }

            var damageReceiverHost = GetComponent<DamageReceiverHost>() ?? gameObject.AddComponent<DamageReceiverHost>();
            builder.RegisterComponent(damageReceiverHost).AsSelf();

            builder.RegisterEntryPoint<PlayerGravity>().AsSelf();
            builder.Register<StatsController>(Lifetime.Singleton).AsSelf();
            builder.Register<StatFillers>(Lifetime.Singleton).AsSelf();
            builder.RegisterInstance(new AdditionalStatsPeriodicDrainPolicy(consumeAdditionalStatsOverTime)).As<IAdditionalStatsPeriodicDrainPolicy>();
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
            builder.Register<NpcHitReactionController>(Lifetime.Scoped)
                   .AsSelf()
                   .As<ICharacterHitReactionController>()
                   .As<IStartable>()
                   .As<ITickable>()
                   .As<System.IDisposable>();
            builder.Register<CharacterDamageReceiver>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<EquippedItemVisualController>().AsSelf();
            builder.Register<NpcWeaponInHandController>(Lifetime.Scoped)
                   .AsSelf()
                   .As<IEquippedWeaponVisual>()
                   .As<IStartable>()
                   .As<ITickable>()
                   .As<System.IDisposable>();

            builder.RegisterEntryPoint<PlayerInventory>().As<IInventory>().AsSelf();
            builder.Register<CharacterWorldItemDropper>(Lifetime.Scoped).AsSelf();
            builder.Register<NpcInventoryPlanner>(Lifetime.Scoped).AsSelf();
            builder.Register<NpcItemPickupService>(Lifetime.Scoped).AsSelf();
            builder.Register<NpcTargetLockController>(Lifetime.Scoped).AsSelf();
            builder.Register<NpcCombatService>(Lifetime.Scoped)
                   .AsSelf()
                   .As<IStartable>()
                   .As<ITickable>()
                   .As<System.IDisposable>();
            builder.Register<NpcDialogueController>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<NpcDialogueInteractableLogic>().AsSelf();
            if (hasForcedDialogueZone)
            {
                builder.RegisterEntryPoint<NpcForcedDialogueInteractableLogic>().AsSelf();
            }
            builder.Register<EquippedWeaponDropService>(Lifetime.Scoped).AsSelf();
            builder.Register(_ => new MoneyStorage(0), Lifetime.Scoped).AsSelf();
            builder.Register<QuestController>(Lifetime.Scoped).AsSelf();

        }

        private void EnsureDialogueInteractionZone()
        {
            var zoneTransform = transform.Find(DialogueZoneName);
            if (zoneTransform == null)
            {
                var zoneObject = new GameObject(DialogueZoneName);
                zoneTransform = zoneObject.transform;
                zoneTransform.SetParent(transform, false);
                zoneTransform.localPosition = new Vector3(0f, 1f, 0f);
            }

            var interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
            {
                zoneTransform.gameObject.layer = interactableLayer;
            }

            var trigger = zoneTransform.GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = zoneTransform.gameObject.AddComponent<SphereCollider>();
            }

            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.radius = dialogueInteractionRadius;
        }

        private bool RegisterForcedDialogueZone(IContainerBuilder builder)
        {
            if (forcedDialogueInteractable == null)
            {
                Debug.LogError(
                    $"{nameof(NpcLifetimeScope)} on {name} requires a reference to Forced Dialogue Interactable Zone.",
                    this);
                return false;
            }

            var forcedDialogueAvailability = forcedDialogueInteractable.GetComponent<NpcForcedDialogueAvailability>();
            if (forcedDialogueAvailability == null)
            {
                Debug.LogError(
                    $"{nameof(forcedDialogueInteractable)} on {name} requires {nameof(NpcForcedDialogueAvailability)}.",
                    forcedDialogueInteractable);
                return false;
            }

            builder.RegisterInstance(forcedDialogueInteractable).Keyed(ForcedDialogueInteractableKey);
            builder.RegisterComponent(forcedDialogueAvailability).AsSelf();
            return true;
        }
    }
}
