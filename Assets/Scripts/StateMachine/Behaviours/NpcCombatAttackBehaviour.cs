using NPC;
using Combat;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatAttackBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Attack")]
    public sealed class NpcCombatAttackBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            var nav = context?.GetService<NpcNavMeshController>();
            nav?.SetFacingLocked(true);
            nav?.Stop();
            context?.SetValue(NpcCombatStateKeys.AttackRequested, false);
            context?.SetValue(NpcCombatStateKeys.AttackBlockObserved, false);
            context?.SetValue(NpcCombatStateKeys.AttackElapsed, 0f);
            context?.SetValue(NpcCombatStateKeys.AttackCompleted, false);
            TryRequestAttack(context);
        }

        public override void Logic(StateMachineContext context)
        {
            if (context == null)
            {
                return;
            }

            var combat = context.GetService<NpcCombatService>();
            if (combat == null || !combat.HasCombatTarget)
            {
                return;
            }

            combat.RefreshTargetVisibility();
            combat.FaceTarget();

            context.TryGetValue<float>(NpcCombatStateKeys.AttackElapsed, out var elapsed);
            elapsed += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.AttackElapsed, elapsed);
            var attackStateTimeout = context.GetService<NpcCombatConfig>()?.AttackStateTimeout ?? 3f;

            context.TryGetValue<bool>(NpcCombatStateKeys.AttackRequested, out var requested);
            if (!requested)
            {
                if (!combat.HasClearAttackLane())
                {
                    context.SetValue(NpcCombatStateKeys.AttackCompleted, true);
                    return;
                }

                TryRequestAttack(context);
                if (elapsed >= attackStateTimeout)
                {
                    context.SetValue(NpcCombatStateKeys.AttackCompleted, true);
                }

                return;
            }

            var actionState = context.GetService<CharacterActionState>();
            var isActionBlocked = actionState?.IsActionBlocked == true;
            if (isActionBlocked)
            {
                context.SetValue(NpcCombatStateKeys.AttackBlockObserved, true);
                return;
            }

            context.TryGetValue<bool>(NpcCombatStateKeys.AttackBlockObserved, out var blockObserved);
            if (blockObserved || elapsed >= attackStateTimeout)
            {
                context.SetValue(NpcCombatStateKeys.AttackCompleted, true);
            }
        }

        private static void TryRequestAttack(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            if (combat != null && combat.TryPrepareWeapon() && combat.RequestAttack())
            {
                context.SetValue(NpcCombatStateKeys.AttackRequested, true);
            }
        }

        public override void Exit(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.AttackRequested);
            context?.RemoveValue(NpcCombatStateKeys.AttackBlockObserved);
            context?.RemoveValue(NpcCombatStateKeys.AttackElapsed);
            context?.RemoveValue(NpcCombatStateKeys.AttackCompleted);
        }
    }
}
