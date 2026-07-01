using Combat;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "CharacterActionUnblockedCondition", menuName = "configs/StateMachine/Conditions/Character Action Unblocked")]
    public sealed class CharacterActionUnblockedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<CharacterActionState>()?.IsActionBlocked == false;
        }
    }
}
