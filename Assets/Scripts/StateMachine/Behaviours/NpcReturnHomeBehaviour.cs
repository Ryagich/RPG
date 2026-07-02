using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcReturnHomeBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Return Home")]
    public sealed class NpcReturnHomeBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.SetFacingLocked(false);
            context?.GetService<NpcItemInterest>()?.SetState("ReturningHome");
            MoveHome(context);
        }

        public override void Logic(StateMachineContext context)
        {
            MoveHome(context);
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<NpcItemInterest>()?.Clear();
            context?.RemoveValue(NpcItemStateKeys.TargetItem);
            context?.RemoveValue(NpcItemStateKeys.PickupPlan);
            context?.RemoveValue(NpcItemStateKeys.PickupPlanQueue);
            context?.RemoveValue(NpcItemStateKeys.PickupCompleted);
            context?.RemoveValue(NpcItemStateKeys.ChainPickupFound);
        }

        private static void MoveHome(StateMachineContext context)
        {
            if (context == null)
            {
                return;
            }

            if (!context.TryGetValue<Vector3>(NpcItemStateKeys.HomePosition, out var homePosition))
            {
                homePosition = context.GetService<NpcItemInterest>()?.HomePosition ?? Vector3.zero;
            }

            context.GetService<NpcNavMeshController>()?.MoveTo(homePosition);
            RotateHomeIfArrived(context, homePosition);
        }

        private static void RotateHomeIfArrived(StateMachineContext context, Vector3 homePosition)
        {
            if (context.Owner == null || !context.TryGetValue<Quaternion>(NpcItemStateKeys.HomeRotation, out var homeRotation))
            {
                return;
            }

            var reachedDistance = context.GetService<NpcItemPickupConfig>()?.HomeReachedDistance ?? 0.35f;
            if (Vector3.Distance(context.Owner.transform.position, homePosition) > reachedDistance)
            {
                return;
            }

            context.GetService<NpcNavMeshController>()?.Stop();
            context.Owner.transform.rotation = Quaternion.RotateTowards(
                context.Owner.transform.rotation,
                homeRotation,
                720f * Time.deltaTime);
        }
    }
}
