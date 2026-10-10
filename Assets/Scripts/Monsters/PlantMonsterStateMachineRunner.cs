using Combat;
using Inventory.Looting;
using StateMachine;
using StateMachine.Graph;
using Stats;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using RuntimeStateMachine = StateMachine.StateMachine;

namespace Monsters
{
    /// <summary>Builds the monster-specific context and runs its authored state graph.</summary>
    public sealed class PlantMonsterStateMachineRunner : IStartable, ITickable
    {
        private readonly StateMachineGraph graph;
        private readonly Transform ownerTransform;
        private readonly PlantMonsterAnimationController animation;
        private readonly PlantMonsterNavMeshController navigation;
        private readonly PlantMonsterCombatController combat;
        private readonly PlantMonsterHitReactionController hitReaction;
        private readonly PlantMonsterTargetingController targeting;
        private readonly PlantMonsterCombatVisionController combatVision;
        private readonly StatsController stats;
        private readonly CorpseLootController corpseLoot;
        private RuntimeStateMachine stateMachine;

        public PlantMonsterStateMachineRunner(
            StateMachineGraph graph,
            Transform ownerTransform,
            PlantMonsterAnimationController animation,
            PlantMonsterNavMeshController navigation,
            PlantMonsterCombatController combat,
            PlantMonsterHitReactionController hitReaction,
            PlantMonsterTargetingController targeting,
            PlantMonsterCombatVisionController combatVision,
            StatsController stats,
            IObjectResolver resolver)
        {
            this.graph = graph;
            this.ownerTransform = ownerTransform;
            this.animation = animation;
            this.navigation = navigation;
            this.combat = combat;
            this.hitReaction = hitReaction;
            this.targeting = targeting;
            this.combatVision = combatVision;
            this.stats = stats;
            corpseLoot = resolver.TryResolve<CorpseLootController>(out var resolvedCorpseLoot)
                ? resolvedCorpseLoot
                : null;
        }

        public void Start()
        {
            var context = new StateMachineContext { Owner = ownerTransform != null ? ownerTransform.gameObject : null };
            context.SetService(animation);
            context.SetService(navigation);
            context.SetService(combat);
            context.SetService(hitReaction);
            context.SetService(targeting);
            context.SetService(combatVision);
            context.SetService(stats);
            if (corpseLoot != null)
            {
                context.SetService(corpseLoot);
            }
            stateMachine = new RuntimeStateMachine(graph, context);
            stateMachine.Start();
        }

        public void Tick() => stateMachine?.Tick(Time.deltaTime);
    }
}
