using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "PlantMonsterTargetInAttackViewCondition", menuName = "configs/StateMachine/Conditions/Plant Monster Target In Attack View")]
    public sealed class PlantMonsterTargetInAttackViewCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context) =>
            context?.GetService<PlantMonsterTargetingController>()?.IsTargetInAttackView == true;
    }
}
