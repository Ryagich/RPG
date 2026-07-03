using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatApproachBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Approach")]
    public sealed class NpcCombatApproachBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.SetFacingLocked(true);
            context?.GetService<NpcItemInterest>()?.Clear();
            context?.GetService<NpcCombatService>()?.TryPrepareWeapon();
            MoveToTarget(context);
        }

        public override void Logic(StateMachineContext context)
        {
            MoveToTarget(context);
        }

        private static void MoveToTarget(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            if (combat == null || !combat.HasCombatTarget)
            {
                return;
            }

            combat.RefreshTargetVisibility();
            combat.FaceTarget();
            var nav = context.GetService<NpcNavMeshController>();
            var config = context.GetService<NpcCombatConfig>();
            if (nav == null)
            {
                return;
            }

            if (!combat.TryGetApproachDestination(out var destination, out var stoppingDistance))
            {
                destination = combat.CurrentTarget.transform.position;
                stoppingDistance = config != null ? config.ApproachStoppingDistance : 1.6f;
            }

            if (nav.HasReachedDestination
             && !combat.CanStartAttack
             && combat.TryGetCloserAttackApproachDestination(out var closerDestination, out var closerStoppingDistance))
            {
                destination = closerDestination;
                stoppingDistance = closerStoppingDistance;
            }

            if (nav.MoveTo(destination, stoppingDistance: stoppingDistance))
            {
                return;
            }

            if (combat.TryGetAlternativeApproachDestination(out destination, out stoppingDistance)
             && nav.MoveTo(destination, stoppingDistance: stoppingDistance))
            {
                return;
            }

            nav.MoveTo(combat.CurrentTarget.transform.position, stoppingDistance: config != null ? config.ApproachStoppingDistance : 1.6f);
        }
    }
}
