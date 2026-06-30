using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcMoveToContextTarget", menuName = "configs/StateMachine/Behaviours/NPC Move To Context Target")]
    public sealed class NpcMoveToContextTargetBehaviour : BaseBehaviour
    {
        [SerializeField] private string targetTransformKey = "NavMeshTarget";
        [SerializeField] private string targetPositionKey = "NavMeshDestination";
        [SerializeField, Min(0f)] private float repathIfStuckSeconds = 1f;

        private float stuckTimer;

        public override void Enter(StateMachineContext context)
        {
            stuckTimer = 0f;
            TryMoveToContextTarget(context);
        }

        public override void Logic(StateMachineContext context)
        {
            var controller = context?.GetService<NpcNavMeshController>();
            if (controller == null || controller.IsMoving || controller.HasReachedDestination)
            {
                stuckTimer = 0f;
                return;
            }

            stuckTimer += context.DeltaTime;
            if (stuckTimer >= repathIfStuckSeconds)
            {
                TryMoveToContextTarget(context);
                stuckTimer = 0f;
            }
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.Stop();
        }

        private void TryMoveToContextTarget(StateMachineContext context)
        {
            var controller = context?.GetService<NpcNavMeshController>();
            if (controller == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(targetTransformKey)
             && context.TryGetValue(targetTransformKey, out Transform target)
             && target != null)
            {
                controller.MoveTo(target.position);
                return;
            }

            if (!string.IsNullOrWhiteSpace(targetPositionKey)
             && context.TryGetValue(targetPositionKey, out Vector3 destination))
            {
                controller.MoveTo(destination);
            }
        }
    }
}
