using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcPickupCompletedCondition", menuName = "configs/StateMachine/Conditions/NPC Pickup Completed")]
    public sealed class NpcPickupCompletedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context != null
                && context.TryGetValue<bool>(NpcItemStateKeys.PickupCompleted, out var completed)
                && completed;
        }
    }
}
