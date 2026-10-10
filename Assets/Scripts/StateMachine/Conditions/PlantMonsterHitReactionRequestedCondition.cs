using Combat;
using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "PlantMonsterHitReactionRequestedCondition", menuName = "configs/StateMachine/Conditions/Plant Monster Hit Reaction Requested")]
    public sealed class PlantMonsterHitReactionRequestedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context) =>
            context?.GetService<PlantMonsterHitReactionController>()?.IsReacting == true;
    }
}
