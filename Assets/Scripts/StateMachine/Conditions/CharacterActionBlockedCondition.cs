using Combat;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "CharacterActionBlockedCondition", menuName = "configs/StateMachine/Conditions/Character Action Blocked")]
    public sealed class CharacterActionBlockedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<CharacterActionState>()?.IsActionBlocked == true;
        }
    }
}
