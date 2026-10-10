using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "PlantMonsterIdleBehaviour", menuName = "configs/StateMachine/Behaviours/Plant Monster Idle")]
    public sealed class PlantMonsterIdleBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<PlantMonsterCombatVisionController>()?.SetEnabled(false);
            context?.GetService<PlantMonsterNavMeshController>()?.Stop();
            var animation = context?.GetService<PlantMonsterAnimationController>();
            animation?.SetLocomotion(0f, force: true);
            animation?.PlayIdle();
        }

        public override void Exit(StateMachineContext context) =>
            context?.GetService<PlantMonsterCombatVisionController>()?.SetEnabled(true);
    }
}
