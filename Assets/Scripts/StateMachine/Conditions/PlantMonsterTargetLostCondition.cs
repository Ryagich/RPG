using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "PlantMonsterTargetLostCondition", menuName = "configs/StateMachine/Conditions/Plant Monster Target Lost")]
    public sealed class PlantMonsterTargetLostCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            PlantMonsterTargetingController targeting = context?.GetService<PlantMonsterTargetingController>();
            return targeting != null && !targeting.IsTargetVisible;
        }
    }
}
