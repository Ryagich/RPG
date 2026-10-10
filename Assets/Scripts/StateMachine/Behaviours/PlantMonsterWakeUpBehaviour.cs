using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "PlantMonsterWakeUpBehaviour", menuName = "configs/StateMachine/Behaviours/Plant Monster Wake Up")]
    public sealed class PlantMonsterWakeUpBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<PlantMonsterCombatVisionController>()?.SetEnabled(false);
            context?.GetService<PlantMonsterNavMeshController>()?.Stop();
            context?.GetService<PlantMonsterAnimationController>()?.ExitPlantedForm();
        }

        public override void Exit(StateMachineContext context) =>
            context?.GetService<PlantMonsterCombatVisionController>()?.SetEnabled(true);
    }
}
