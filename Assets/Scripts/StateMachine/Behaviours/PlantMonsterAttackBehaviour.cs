using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "PlantMonsterAttackBehaviour", menuName = "configs/StateMachine/Behaviours/Plant Monster Attack")]
    public sealed class PlantMonsterAttackBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<PlantMonsterNavMeshController>()?.Stop();
            context?.GetService<PlantMonsterAnimationController>()?.PlayPrimaryAttack();
        }

        public override void Logic(StateMachineContext context)
        {
            var animation = context?.GetService<PlantMonsterAnimationController>();
            var targeting = context?.GetService<PlantMonsterTargetingController>();
            if (animation?.IsExpectedActionFinished != true || targeting?.IsTargetInAttackView != true)
            {
                return;
            }

            // Stay in the same graph state: leaving through Combat idle between consecutive
            // attacks briefly changes locomotion and causes a visible jerk.
            context?.GetService<PlantMonsterCombatController>()?.EndDamageWindow();
            animation.PlayPrimaryAttack();
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<PlantMonsterCombatController>()?.EndDamageWindow();
            context?.GetService<PlantMonsterAnimationController>()?.CancelExpectedAction();
        }
    }
}
