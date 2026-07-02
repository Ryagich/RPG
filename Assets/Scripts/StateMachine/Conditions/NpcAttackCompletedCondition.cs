using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcAttackCompletedCondition", menuName = "configs/StateMachine/Conditions/NPC Attack Completed")]
    public sealed class NpcAttackCompletedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context != null
                   && context.TryGetValue<bool>(NpcCombatStateKeys.AttackCompleted, out var completed)
                   && completed;
        }
    }
}
