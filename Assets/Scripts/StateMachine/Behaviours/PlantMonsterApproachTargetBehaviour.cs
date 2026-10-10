using Monsters;
using StateMachine.Graph.Model;
using TargetLock;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "PlantMonsterApproachTargetBehaviour", menuName = "configs/StateMachine/Behaviours/Plant Monster Approach Target")]
    public sealed class PlantMonsterApproachTargetBehaviour : BaseBehaviour
    {
        public override void Logic(StateMachineContext context)
        {
            PlantMonsterTargetingController targeting = context?.GetService<PlantMonsterTargetingController>();
            PlantMonsterNavMeshController navigation = context?.GetService<PlantMonsterNavMeshController>();
            TargetLockTarget target = targeting?.CurrentTarget;
            if (target == null || navigation == null || targeting.AttackViewDistance <= 0f)
            {
                return;
            }

            navigation.MoveTo(target.transform.position, stoppingDistance: targeting.AttackViewDistance);
        }

        public override void Exit(StateMachineContext context) =>
            context?.GetService<PlantMonsterNavMeshController>()?.Stop();
    }
}
