using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "PlantMonsterTargetVisibleCondition", menuName = "configs/StateMachine/Conditions/Plant Monster Target Visible")]
    public sealed class PlantMonsterTargetVisibleCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context) =>
            context?.GetService<PlantMonsterTargetingController>()?.IsTargetVisible == true;
    }
}
