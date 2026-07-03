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
            context?.SetValue(NpcCombatStateKeys.ComboAttackRequests, 0);
            context?.SetValue(NpcCombatStateKeys.ComboAttackNextRequestTime, GetComboInputDelay(context));
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
                TryRequestComboAttack(context, elapsed);
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

        private static void TryRequestComboAttack(StateMachineContext context, float elapsed)
        {
            var combat = context?.GetService<NpcCombatService>();
            if (combat == null)
            {
                return;
            }

            var config = context.GetService<NpcCombatConfig>();
            var maxComboRequests = config != null ? config.MaxComboAttackRequests : 2;
            context.TryGetValue<int>(NpcCombatStateKeys.ComboAttackRequests, out var comboRequests);
            if (comboRequests >= maxComboRequests)
            {
                return;
            }

            context.TryGetValue<float>(NpcCombatStateKeys.ComboAttackNextRequestTime, out var nextRequestTime);
            if (elapsed < nextRequestTime)
            {
                return;
            }

            context.SetValue(
                NpcCombatStateKeys.ComboAttackNextRequestTime,
                elapsed + (config != null ? config.ComboAttackInputInterval : 0.22f));

            if (!combat.CanStartAttack)
            {
                return;
            }

            var chance = config != null ? config.ComboAttackChance : 0.55f;
            if (Random.value > Mathf.Clamp01(chance))
            {
                context.SetValue(NpcCombatStateKeys.ComboAttackRequests, maxComboRequests);
                return;
            }

            if (!combat.RequestComboAttack())
            {
                return;
            }

            context.SetValue(NpcCombatStateKeys.ComboAttackRequests, comboRequests + 1);
            context.SetValue(NpcCombatStateKeys.AttackRequested, true);
            context.SetValue(NpcCombatStateKeys.AttackElapsed, 0f);
            context.SetValue(NpcCombatStateKeys.ComboAttackNextRequestTime, GetComboInputDelay(context));
        }

        private static float GetComboInputDelay(StateMachineContext context)
        {
            return context?.GetService<NpcCombatConfig>()?.ComboAttackInputDelay ?? 0.18f;
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<NpcCombatService>()?.ClearAttackRequest();
            context?.RemoveValue(NpcCombatStateKeys.AttackRequested);
            context?.RemoveValue(NpcCombatStateKeys.AttackBlockObserved);
            context?.RemoveValue(NpcCombatStateKeys.AttackElapsed);
            context?.RemoveValue(NpcCombatStateKeys.AttackCompleted);
            context?.RemoveValue(NpcCombatStateKeys.ComboAttackRequests);
            context?.RemoveValue(NpcCombatStateKeys.ComboAttackNextRequestTime);
        }
    }
}
