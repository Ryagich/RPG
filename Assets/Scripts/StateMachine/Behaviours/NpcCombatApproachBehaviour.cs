using Combat;
using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatApproachBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Approach")]
    public sealed class NpcCombatApproachBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            RememberEngagementOrigin(context);
            context?.GetService<NpcNavMeshController>()?.SetFacingLocked(true);
            context?.GetService<NpcItemInterest>()?.Clear();
            context?.GetService<NpcCombatService>()?.TryPrepareWeapon();
            context?.SetValue(NpcCombatStateKeys.ApproachBurstRequested, false);
            context?.SetValue(NpcCombatStateKeys.ApproachBurstElapsed, 0f);
            context?.SetValue(NpcCombatStateKeys.ApproachBurstBlockObserved, false);
            MoveToTarget(context);
        }

        public override void Logic(StateMachineContext context)
        {
            MoveToTarget(context);
        }

        public override void Exit(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.ApproachBurstRequested);
            context?.RemoveValue(NpcCombatStateKeys.ApproachBurstElapsed);
            context?.RemoveValue(NpcCombatStateKeys.ApproachBurstBlockObserved);
        }

        private static void MoveToTarget(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            if (combat == null || !combat.HasCombatTarget)
            {
                return;
            }

            combat.RefreshTargetVisibility();
            var nav = context.GetService<NpcNavMeshController>();
            var config = context.GetService<NpcCombatConfig>();
            if (nav == null)
            {
                return;
            }

            if (HandleApproachBurst(context, combat, nav, config))
            {
                return;
            }

            combat.FaceTarget();

            if (!combat.TryGetApproachDestination(out var destination, out var stoppingDistance))
            {
                destination = combat.CurrentTarget.transform.position;
                stoppingDistance = config != null ? config.ApproachStoppingDistance : 1.6f;
            }

            if (combat.HasDirectCombatSlot()
             && nav.HasReachedDestination
             && !combat.CanStartAttack
             && combat.TryGetCloserAttackApproachDestination(out var closerDestination, out var closerStoppingDistance))
            {
                destination = closerDestination;
                stoppingDistance = closerStoppingDistance;
            }

            if (nav.MoveTo(destination, stoppingDistance: stoppingDistance))
            {
                return;
            }

            if (combat.TryGetAlternativeApproachDestination(out destination, out stoppingDistance)
             && nav.MoveTo(destination, stoppingDistance: stoppingDistance))
            {
                return;
            }

            if (!combat.HasDirectCombatSlot())
            {
                // A participant without a close-combat sector must never fall back to the
                // target's centre. Queue state will keep it circling outside until a slot opens.
                if (combat.TrySelectQueueCircleDestination())
                {
                    nav.MoveTo(combat.CombatMoveDestination, stoppingDistance: config != null ? config.CombatMoveReachedDistance : 0.45f);
                }
                else
                {
                    nav.Stop();
                }

                return;
            }

            nav.MoveTo(combat.CurrentTarget.transform.position, stoppingDistance: config != null ? config.ApproachStoppingDistance : 1.6f);
        }

        private static bool HandleApproachBurst(
            StateMachineContext context,
            NpcCombatService combat,
            NpcNavMeshController nav,
            NpcCombatConfig config)
        {
            context.TryGetValue<bool>(NpcCombatStateKeys.ApproachBurstRequested, out var requested);
            if (!requested)
            {
                // The Animator receives an evasion direction in local space. Face the target
                // before requesting a burst so a forward dodge/roll stays a forward lunge.
                combat.FaceTarget();
                if (!combat.TryRequestApproachBurst())
                {
                    return false;
                }

                nav.Stop();
                context.SetValue(NpcCombatStateKeys.ApproachBurstRequested, true);
                context.SetValue(NpcCombatStateKeys.ApproachBurstElapsed, 0f);
                context.SetValue(NpcCombatStateKeys.ApproachBurstBlockObserved, false);
                return true;
            }

            nav.Stop();
            context.TryGetValue<float>(NpcCombatStateKeys.ApproachBurstElapsed, out var elapsed);
            elapsed += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.ApproachBurstElapsed, elapsed);

            var actionState = context.GetService<CharacterActionState>();
            if (actionState?.IsActionBlocked == true)
            {
                context.SetValue(NpcCombatStateKeys.ApproachBurstBlockObserved, true);
                return true;
            }

            context.TryGetValue<bool>(NpcCombatStateKeys.ApproachBurstBlockObserved, out var blockObserved);
            var timeout = config?.EvasionStateTimeout ?? 1.35f;
            if (!blockObserved && elapsed < timeout)
            {
                return true;
            }

            context.SetValue(NpcCombatStateKeys.ApproachBurstRequested, false);
            context.SetValue(NpcCombatStateKeys.ApproachBurstElapsed, 0f);
            context.SetValue(NpcCombatStateKeys.ApproachBurstBlockObserved, false);
            return false;
        }

        private static void RememberEngagementOrigin(StateMachineContext context)
        {
            if (context?.Owner == null
             || context.TryGetValue<Vector3>(NpcCombatStateKeys.EngagementOriginPosition, out _))
            {
                return;
            }

            context.SetValue(NpcCombatStateKeys.EngagementOriginPosition, context.Owner.transform.position);
            context.SetValue(NpcCombatStateKeys.EngagementOriginRotation, context.Owner.transform.rotation);
        }
    }
}
