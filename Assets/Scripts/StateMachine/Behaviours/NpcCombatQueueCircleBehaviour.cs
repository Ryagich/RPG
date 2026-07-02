using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatQueueCircleBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Queue Circle")]
    public sealed class NpcCombatQueueCircleBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.SetFacingLocked(true);
            context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, false);
            var combat = context?.GetService<NpcCombatService>();
            combat?.TrySelectQueueCircleDestination();
            Move(context);
        }

        public override void Logic(StateMachineContext context)
        {
            Move(context);
        }

        public override void Exit(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.CombatMoveCompleted);
            context?.GetService<NpcCombatService>()?.ClearCombatMoveDestination();
        }

        private static void Move(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            var nav = context?.GetService<NpcNavMeshController>();
            if (combat == null || nav == null || context.Owner == null)
            {
                return;
            }

            if (!combat.HasCombatMoveDestination && !combat.TrySelectQueueCircleDestination())
            {
                return;
            }

            combat.RefreshTargetVisibility();
            combat.FaceTarget();
            var reachedDistance = context.GetService<NpcCombatConfig>()?.CombatMoveReachedDistance ?? 0.45f;
            if (Vector3.Distance(context.Owner.transform.position, combat.CombatMoveDestination) <= reachedDistance)
            {
                nav.Stop();
                combat.ClearCombatMoveDestination();
                combat.TrySelectQueueCircleDestination();
                return;
            }

            nav.MoveTo(combat.CombatMoveDestination, stoppingDistance: reachedDistance);
        }
    }
}
