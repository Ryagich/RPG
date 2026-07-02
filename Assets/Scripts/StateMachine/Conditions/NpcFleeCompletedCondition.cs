using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcFleeCompletedCondition", menuName = "configs/StateMachine/Conditions/NPC Flee Completed")]
    public sealed class NpcFleeCompletedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context != null
                   && context.TryGetValue<bool>(NpcCombatStateKeys.FleeCompleted, out var completed)
                   && completed;
        }
    }
}
