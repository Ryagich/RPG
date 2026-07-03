using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatManeuverBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Maneuver")]
    public sealed class NpcCombatManeuverBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.SetFacingLocked(true);
            context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, false);
            NpcCombatMoveProgress.Reset(context);
            Move(context);
        }

        public override void Logic(StateMachineContext context)
        {
            Move(context);
        }

        public override void Exit(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.CombatMoveCompleted);
            NpcCombatMoveProgress.Clear(context);
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

            if (!combat.HasCombatMoveDestination)
            {
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            combat.RefreshTargetVisibility();
            combat.FaceTarget();

            var config = context.GetService<NpcCombatConfig>();
            var reachedDistance = config?.CombatMoveReachedDistance ?? 0.45f;
            if (Vector3.Distance(context.Owner.transform.position, combat.CombatMoveDestination) <= reachedDistance)
            {
                nav.Stop();
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            if (NpcCombatMoveProgress.IsStuck(context, config))
            {
                nav.Stop();
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            if (nav.MoveTo(combat.CombatMoveDestination, stoppingDistance: reachedDistance))
            {
                return;
            }

            nav.Stop();
            context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
        }
    }
}
