using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "PlantMonsterActionAnimationFinishedCondition", menuName = "configs/StateMachine/Conditions/Plant Monster Action Animation Finished")]
    public sealed class PlantMonsterActionAnimationFinishedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context) =>
            context?.GetService<PlantMonsterAnimationController>()?.IsExpectedActionFinished == true;
    }
}
