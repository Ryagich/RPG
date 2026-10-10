using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "PlantMonsterSearchLastKnownBehaviour", menuName = "configs/StateMachine/Behaviours/Plant Monster Search Last Known")]
    public sealed class PlantMonsterSearchLastKnownBehaviour : BaseBehaviour
    {
        public override void Logic(StateMachineContext context)
        {
            var targeting = context?.GetService<PlantMonsterTargetingController>();
            var navigation = context?.GetService<PlantMonsterNavMeshController>();
            var settings = context?.GetService<PlantMonsterSearchConfig>();
            if (targeting == null || navigation == null || !targeting.HasLastKnownTargetPosition)
            {
                return;
            }

            var stoppingDistance = settings != null ? settings.ReachedDistance : 1.2f;
            navigation.MoveTo(targeting.LastKnownTargetPosition, stoppingDistance: stoppingDistance);
        }

        public override void Exit(StateMachineContext context)
        {
            var targeting = context?.GetService<PlantMonsterTargetingController>();
            if (targeting?.HasTarget != true)
            {
                targeting?.ClearLastKnownTargetPosition();
            }

            context?.GetService<PlantMonsterNavMeshController>()?.Stop();
        }
    }
}
