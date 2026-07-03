using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatCircleBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Circle")]
    public sealed class NpcCombatCircleBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.SetFacingLocked(true);
            context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, false);
            NpcCombatMoveProgress.Reset(context);
            var combat = context?.GetService<NpcCombatService>();
            if (combat != null && !combat.HasCombatMoveDestination)
            {
                combat.TrySelectCombatManeuverDestination(NpcCombatManeuverKind.Circle);
            }

            Move(context);
        }

        public override void Logic(StateMachineContext context)
        {
            Move(context);
        }

        public override void Exit(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.CombatMoveCompleted);
            context?.RemoveValue(NpcCombatStateKeys.InitialCircleRequested);
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
                combat.ClearCombatMoveDestination();
                NpcCombatMoveProgress.Reset(context);
                if (!combat.TrySelectCombatManeuverDestination(NpcCombatManeuverKind.Circle))
                {
                    nav.Stop();
                    context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                    return;
                }
            }

            if (nav.MoveTo(combat.CombatMoveDestination, stoppingDistance: reachedDistance))
            {
                return;
            }

            combat.ClearCombatMoveDestination();
            NpcCombatMoveProgress.Reset(context);
            if (combat.TrySelectCombatManeuverDestination(NpcCombatManeuverKind.Circle)
             && nav.MoveTo(combat.CombatMoveDestination, stoppingDistance: reachedDistance))
            {
                return;
            }

            nav.Stop();
            context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
        }
    }

    internal static class NpcCombatMoveProgress
    {
        public static void Reset(StateMachineContext context)
        {
            if (context?.Owner == null)
            {
                return;
            }

            context.SetValue(NpcCombatStateKeys.CombatMoveLastPosition, context.Owner.transform.position);
            context.SetValue(NpcCombatStateKeys.CombatMoveStuckTimer, 0f);
        }

        public static void Clear(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.CombatMoveLastPosition);
            context?.RemoveValue(NpcCombatStateKeys.CombatMoveStuckTimer);
        }

        public static bool IsStuck(StateMachineContext context, NpcCombatConfig config)
        {
            if (context?.Owner == null)
            {
                return false;
            }

            var currentPosition = context.Owner.transform.position;
            if (!context.TryGetValue<Vector3>(NpcCombatStateKeys.CombatMoveLastPosition, out var lastPosition))
            {
                Reset(context);
                return false;
            }

            var progressDistance = config != null ? config.CombatMoveProgressDistance : 0.08f;
            if (PlanarDistance(currentPosition, lastPosition) >= progressDistance)
            {
                context.SetValue(NpcCombatStateKeys.CombatMoveLastPosition, currentPosition);
                context.SetValue(NpcCombatStateKeys.CombatMoveStuckTimer, 0f);
                return false;
            }

            context.TryGetValue<float>(NpcCombatStateKeys.CombatMoveStuckTimer, out var timer);
            timer += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.CombatMoveStuckTimer, timer);

            var stuckTimeout = config != null ? config.CombatMoveStuckTimeout : 0.75f;
            return timer >= stuckTimeout;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
