using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatTargetDownBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Target Down")]
    public sealed class NpcCombatTargetDownBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.SetValue(NpcCombatStateKeys.TargetDownWaitTimer, 0f);
            context?.SetValue(NpcCombatStateKeys.TargetDownWaitCompleted, false);
            var nav = context?.GetService<NpcNavMeshController>();
            nav?.SetFacingLocked(true);
            nav?.Stop();
            context?.GetService<NpcCombatService>()?.TryResolveCurrentTargetDown();
        }

        public override void Logic(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            if (combat == null)
            {
                return;
            }

            if (combat.HasCombatTarget || combat.TryAdoptNearbyCombatTarget() || combat.ScanForEnemy(true))
            {
                return;
            }

            context.GetService<NpcNavMeshController>()?.Stop();
            combat.FaceLastKnownPosition();
            context.TryGetValue<float>(NpcCombatStateKeys.TargetDownWaitTimer, out var timer);
            timer += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.TargetDownWaitTimer, timer);
            var waitDuration = context.GetService<NpcCombatConfig>()?.TargetDownWaitDuration ?? 2f;
            if (timer >= waitDuration)
            {
                context.SetValue(NpcCombatStateKeys.TargetDownWaitCompleted, true);
            }
        }

        public override void Exit(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.TargetDownWaitTimer);
            context?.RemoveValue(NpcCombatStateKeys.TargetDownWaitCompleted);
        }
    }
}
