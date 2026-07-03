using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatWaitBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Wait")]
    public sealed class NpcCombatWaitBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            var nav = context?.GetService<NpcNavMeshController>();
            nav?.SetFacingLocked(true);
            nav?.Stop();

            var config = context?.GetService<NpcCombatConfig>();
            var minDuration = config != null ? config.WaitMinDuration : 0.35f;
            var maxDuration = Mathf.Max(minDuration, config != null ? config.WaitMaxDuration : 1.1f);
            context?.SetValue(NpcCombatStateKeys.WaitTimer, 0f);
            context?.SetValue(NpcCombatStateKeys.WaitDuration, Random.Range(minDuration, maxDuration));
            context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, false);
        }

        public override void Logic(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            if (combat == null || !combat.HasCombatTarget)
            {
                context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            combat.RefreshTargetVisibility();
            combat.FaceTarget();

            context.TryGetValue<float>(NpcCombatStateKeys.WaitTimer, out var timer);
            context.TryGetValue<float>(NpcCombatStateKeys.WaitDuration, out var duration);
            timer += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.WaitTimer, timer);

            if (timer >= duration)
            {
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
            }
        }

        public override void Exit(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.WaitTimer);
            context?.RemoveValue(NpcCombatStateKeys.WaitDuration);
            context?.RemoveValue(NpcCombatStateKeys.CombatMoveCompleted);
        }
    }
}
