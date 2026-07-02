using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcHasCombatTargetCondition", menuName = "configs/StateMachine/Conditions/NPC Has Combat Target")]
    public sealed class NpcHasCombatTargetCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<NpcCombatService>()?.HasCombatTarget == true;
        }
    }
}
