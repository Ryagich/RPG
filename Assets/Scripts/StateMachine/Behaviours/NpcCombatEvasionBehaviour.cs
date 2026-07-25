using Combat;
using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatEvasionBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Evasion")]
    public sealed class NpcCombatEvasionBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            var nav = context?.GetService<NpcNavMeshController>();
            nav?.SetFacingLocked(true);
            nav?.Stop();

            context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, false);
            context?.SetValue(NpcCombatStateKeys.EvasionElapsed, 0f);
            context?.SetValue(NpcCombatStateKeys.EvasionBlockObserved, false);
            var requested = context?.GetService<NpcCombatService>()?.TryRequestSpacingEvasion() == true;
            context?.SetValue(NpcCombatStateKeys.EvasionRequested, requested);
            if (!requested)
            {
                context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
            }
        }

        public override void Logic(StateMachineContext context)
        {
            if (context == null)
            {
                return;
            }

            context.TryGetValue<bool>(NpcCombatStateKeys.EvasionRequested, out var requested);
            if (!requested)
            {
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            var combat = context.GetService<NpcCombatService>();
            combat?.RefreshTargetVisibility();

            context.TryGetValue<float>(NpcCombatStateKeys.EvasionElapsed, out var elapsed);
            elapsed += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.EvasionElapsed, elapsed);

            var actionState = context.GetService<CharacterActionState>();
            if (actionState?.IsActionBlocked == true)
            {
                context.SetValue(NpcCombatStateKeys.EvasionBlockObserved, true);
                return;
            }

            context.TryGetValue<bool>(NpcCombatStateKeys.EvasionBlockObserved, out var blockObserved);
            var timeout = context.GetService<NpcCombatConfig>()?.EvasionStateTimeout ?? 1.35f;
            if (blockObserved || elapsed >= timeout)
            {
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
            }
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<NpcCombatService>()?.ClearAttackRequest();
            context?.RemoveValue(NpcCombatStateKeys.CombatMoveCompleted);
            context?.RemoveValue(NpcCombatStateKeys.EvasionElapsed);
            context?.RemoveValue(NpcCombatStateKeys.EvasionRequested);
            context?.RemoveValue(NpcCombatStateKeys.EvasionBlockObserved);
        }
    }
}
