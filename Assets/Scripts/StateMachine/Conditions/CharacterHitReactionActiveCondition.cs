using Combat;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "CharacterHitReactionActiveCondition", menuName = "configs/StateMachine/Conditions/Character Hit Reaction Active")]
    public sealed class CharacterHitReactionActiveCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<ICharacterHitReactionController>()?.IsReacting == true;
        }
    }
}
