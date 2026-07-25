using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatReturnHomeBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Return Home")]
    public sealed class NpcCombatReturnHomeBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, false);
            context?.GetService<NpcCombatService>()?.ClearTarget();
            context?.GetService<NpcCombatService>()?.SheatheWeapon();
            context?.GetService<NpcNavMeshController>()?.SetFacingLocked(false);
            ReturnToEngagementOrigin(context);
        }

        public override void Logic(StateMachineContext context)
        {
            ReturnToEngagementOrigin(context);
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.Stop();
            context?.RemoveValue(NpcCombatStateKeys.CombatMoveCompleted);
            context?.RemoveValue(NpcCombatStateKeys.EngagementOriginPosition);
            context?.RemoveValue(NpcCombatStateKeys.EngagementOriginRotation);
        }

        private static void ReturnToEngagementOrigin(StateMachineContext context)
        {
            if (context?.Owner == null)
            {
                context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            if (!context.TryGetValue<Vector3>(NpcCombatStateKeys.EngagementOriginPosition, out var originPosition))
            {
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            var nav = context.GetService<NpcNavMeshController>();
            var reachedDistance = context.GetService<NpcCombatConfig>()?.CombatMoveReachedDistance ?? 0.45f;
            if (!HasReachedPosition(context.Owner.transform.position, originPosition, reachedDistance))
            {
                if (nav?.MoveTo(originPosition, stoppingDistance: reachedDistance) != true)
                {
                    context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                }

                return;
            }

            nav?.Stop();
            if (!context.TryGetValue<Quaternion>(NpcCombatStateKeys.EngagementOriginRotation, out var originRotation)
             || Quaternion.Angle(context.Owner.transform.rotation, originRotation) <= 1f)
            {
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            context.Owner.transform.rotation = Quaternion.RotateTowards(
                context.Owner.transform.rotation,
                originRotation,
                720f * context.DeltaTime);
        }

        private static bool HasReachedPosition(Vector3 currentPosition, Vector3 targetPosition, float reachedDistance)
        {
            currentPosition.y = 0f;
            targetPosition.y = 0f;
            return Vector3.Distance(currentPosition, targetPosition) <= reachedDistance;
        }
    }
}
