using Combat;
using Inventory.Inventories;
using Player;
using StateMachine;
using StateMachine.Graph;
using Stats;
using UnityEngine;
using VContainer.Unity;
using RuntimeStateMachine = StateMachine.StateMachine;

namespace NPC
{
    public sealed class NpcStateMachineRunner : IStartable, ITickable
    {
        private readonly StateMachineGraph stateMachineGraph;
        private readonly Transform ownerTransform;
        private readonly StatsController statsController;
        private readonly PlayerInventory playerInventory;
        private readonly CharacterDamageReceiver damageReceiver;
        private readonly PlayerRagdollController ragdollController;
        private readonly CharacterController characterController;
        private readonly Animator animator;
        private readonly NpcNavMeshController navMeshController;
        private readonly NpcVision npcVision;
        private readonly NpcVisionSensor npcVisionSensor;
        private readonly NpcItemInterest itemInterest;
        private readonly NpcInventoryPlanner inventoryPlanner;
        private readonly NpcItemPickupService itemPickupService;
        private readonly NpcEquippedWeaponDropService equippedWeaponDropService;
        private readonly NpcItemPickupConfig itemPickupConfig;

        private RuntimeStateMachine stateMachine;
        private Container.NpcLifetimeScope npcScope;

        public NpcStateMachineRunner(
            StateMachineGraph stateMachineGraph,
            Transform ownerTransform,
            StatsController statsController,
            PlayerInventory playerInventory,
            CharacterDamageReceiver damageReceiver,
            PlayerRagdollController ragdollController,
            CharacterController characterController,
            Animator animator,
            NpcNavMeshController navMeshController,
            NpcVision npcVision,
            NpcVisionSensor npcVisionSensor,
            NpcItemInterest itemInterest,
            NpcInventoryPlanner inventoryPlanner,
            NpcItemPickupService itemPickupService,
            NpcEquippedWeaponDropService equippedWeaponDropService,
            NpcItemPickupConfig itemPickupConfig)
        {
            this.stateMachineGraph = stateMachineGraph;
            this.ownerTransform = ownerTransform;
            this.statsController = statsController;
            this.playerInventory = playerInventory;
            this.damageReceiver = damageReceiver;
            this.ragdollController = ragdollController;
            this.characterController = characterController;
            this.animator = animator;
            this.navMeshController = navMeshController;
            this.npcVision = npcVision;
            this.npcVisionSensor = npcVisionSensor;
            this.itemInterest = itemInterest;
            this.inventoryPlanner = inventoryPlanner;
            this.itemPickupService = itemPickupService;
            this.equippedWeaponDropService = equippedWeaponDropService;
            this.itemPickupConfig = itemPickupConfig;
        }

        public void Start()
        {
            npcScope = ownerTransform != null
                ? ownerTransform.GetComponent<Container.NpcLifetimeScope>()
                : null;
            ragdollController?.ConfigureTriggerRagdoll();

            var context = new StateMachineContext
            {
                Owner = ownerTransform != null ? ownerTransform.gameObject : null
            };

            context.SetService(stateMachineGraph);
            context.SetService(statsController);
            context.SetService(playerInventory);
            context.SetService(damageReceiver);
            context.SetService(ragdollController);
            context.SetService(characterController);
            context.SetService(animator);
            context.SetService(navMeshController);
            context.SetService(npcVision);
            context.SetService(npcVisionSensor);
            context.SetService(itemInterest);
            context.SetService(inventoryPlanner);
            context.SetService(itemPickupService);
            context.SetService(equippedWeaponDropService);
            context.SetService(itemPickupConfig);

            stateMachine = new RuntimeStateMachine(stateMachineGraph, context);
            stateMachine.Start();
            UpdateRuntimeDebugInfo();
        }

        public void Tick()
        {
            stateMachine?.Tick(Time.deltaTime);
            UpdateRuntimeDebugInfo();
        }

        private void UpdateRuntimeDebugInfo()
        {
            if (npcScope == null)
            {
                return;
            }

            var stateName = stateMachine?.CurrentState != null
                ? stateMachine.CurrentState.name
                : "None";
            npcScope.SetRuntimeDebugInfo(statsController.Hp.Value.Value, stateName);
        }
    }
}
