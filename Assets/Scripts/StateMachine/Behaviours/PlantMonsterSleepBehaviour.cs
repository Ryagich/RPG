using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "PlantMonsterSleepBehaviour", menuName = "configs/StateMachine/Behaviours/Plant Monster Sleep")]
    public sealed class PlantMonsterSleepBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<PlantMonsterCombatController>()?.EndDamageWindow();
            context?.GetService<PlantMonsterNavMeshController>()?.Stop();
            context?.GetService<PlantMonsterTargetingController>()?.ClearTarget();
            context?.GetService<PlantMonsterAnimationController>()?.EnterPlantedForm();
        }
    }
}
