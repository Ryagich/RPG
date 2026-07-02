using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcCombatMoveCompletedCondition", menuName = "configs/StateMachine/Conditions/NPC Combat Move Completed")]
    public sealed class NpcCombatMoveCompletedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context != null
                   && context.TryGetValue<bool>(NpcCombatStateKeys.CombatMoveCompleted, out var completed)
                   && completed;
        }
    }
}
