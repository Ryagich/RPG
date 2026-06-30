using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcReachedHomeCondition", menuName = "configs/StateMachine/Conditions/NPC Reached Home")]
    public sealed class NpcReachedHomeCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            if (context?.Owner == null)
            {
                return false;
            }

            if (!context.TryGetValue<Vector3>(NpcItemStateKeys.HomePosition, out var homePosition))
            {
                homePosition = context.GetService<NpcItemInterest>()?.HomePosition ?? context.Owner.transform.position;
            }

            var distance = Vector3.Distance(context.Owner.transform.position, homePosition);
            var reachedDistance = context.GetService<NpcItemPickupConfig>()?.HomeReachedDistance ?? 0.35f;
            if (distance > reachedDistance)
            {
                return false;
            }

            if (!context.TryGetValue<Quaternion>(NpcItemStateKeys.HomeRotation, out var homeRotation))
            {
                return true;
            }

            return Quaternion.Angle(context.Owner.transform.rotation, homeRotation) <= 1f;
        }
    }
}
