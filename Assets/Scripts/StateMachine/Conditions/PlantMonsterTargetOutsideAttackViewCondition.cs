using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "PlantMonsterTargetOutsideAttackViewCondition", menuName = "configs/StateMachine/Conditions/Plant Monster Target Outside Attack View")]
    public sealed class PlantMonsterTargetOutsideAttackViewCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context) =>
            context?.GetService<PlantMonsterTargetingController>()?.IsTargetInAttackView != true;
    }
}
