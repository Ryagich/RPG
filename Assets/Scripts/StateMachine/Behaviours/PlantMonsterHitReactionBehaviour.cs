using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "PlantMonsterHitReactionBehaviour", menuName = "configs/StateMachine/Behaviours/Plant Monster Hit Reaction")]
    public sealed class PlantMonsterHitReactionBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<PlantMonsterCombatController>()?.EndDamageWindow();
            context?.GetService<PlantMonsterAnimationController>()?.CancelExpectedAction();
            context?.GetService<PlantMonsterNavMeshController>()?.SetMovementLocked(true);
            context?.GetService<PlantMonsterHitReactionController>()?.NotifyHitAnimationStarted();
            context?.GetService<PlantMonsterAnimationController>()?.PlayHitReaction();
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<PlantMonsterNavMeshController>()?.SetMovementLocked(false);
        }
    }
}
