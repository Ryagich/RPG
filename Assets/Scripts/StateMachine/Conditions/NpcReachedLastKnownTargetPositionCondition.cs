using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcReachedLastKnownTargetPositionCondition", menuName = "configs/StateMachine/Conditions/NPC Reached Last Known Target Position")]
    public sealed class NpcReachedLastKnownTargetPositionCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            if (context?.Owner == null || combat == null || !combat.HasLastKnownTargetPosition)
            {
                return false;
            }

            var distance = Vector3.Distance(context.Owner.transform.position, combat.LastKnownTargetPosition);
            var threshold = context.GetService<NpcCombatConfig>()?.LastKnownReachedDistance ?? 1.2f;
            return distance <= threshold;
        }
    }
}
