using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "PlantMonsterHitReactionFinishedCondition", menuName = "configs/StateMachine/Conditions/Plant Monster Hit Reaction Finished")]
    public sealed class PlantMonsterHitReactionFinishedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context) =>
            context?.GetService<PlantMonsterHitReactionController>()?.IsHitAnimationFinished == true;
    }
}
