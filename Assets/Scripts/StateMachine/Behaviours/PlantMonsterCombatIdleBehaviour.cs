using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "PlantMonsterCombatIdleBehaviour", menuName = "configs/StateMachine/Behaviours/Plant Monster Combat Idle")]
    public sealed class PlantMonsterCombatIdleBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<PlantMonsterNavMeshController>()?.Stop();
            context?.GetService<PlantMonsterAnimationController>()?.SetLocomotion(0f, force: true);
        }
    }
}
