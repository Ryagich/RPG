using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatSearchLastKnownBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Search Last Known")]
    public sealed class NpcCombatSearchLastKnownBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.SetFacingLocked(true);
            context?.SetValue(NpcCombatStateKeys.LastKnownLookTimer, 0f);
            Move(context);
        }

        public override void Logic(StateMachineContext context)
        {
            Move(context);
        }

        private static void Move(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            if (combat == null || !combat.HasLastKnownTargetPosition)
            {
                return;
            }

            var stoppingDistance = context.GetService<NpcCombatConfig>()?.LastKnownReachedDistance ?? 1.2f;
            context.GetService<NpcNavMeshController>()?.MoveTo(combat.LastKnownTargetPosition, stoppingDistance: stoppingDistance);
            combat.FaceLastKnownPosition();
        }
    }
}
