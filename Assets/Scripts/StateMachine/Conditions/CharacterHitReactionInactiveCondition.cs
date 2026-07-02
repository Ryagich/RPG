using Combat;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "CharacterHitReactionInactiveCondition", menuName = "configs/StateMachine/Conditions/Character Hit Reaction Inactive")]
    public sealed class CharacterHitReactionInactiveCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<ICharacterHitReactionController>()?.IsReacting == false;
        }
    }
}
