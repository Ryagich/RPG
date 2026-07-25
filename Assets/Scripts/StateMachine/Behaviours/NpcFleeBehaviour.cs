using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcFleeBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Flee")]
    public sealed class NpcFleeBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.EngagementOriginPosition);
            context?.RemoveValue(NpcCombatStateKeys.EngagementOriginRotation);
            context?.RemoveValue(NpcCombatStateKeys.FleeCompleted);
            context?.SetValue(NpcCombatStateKeys.FleeLookTimer, 0f);
            context?.SetValue(NpcCombatStateKeys.FleeLookingBack, false);
            context?.GetService<NpcItemInterest>()?.Clear();
            StoreHomeIfMissing(context);
            var nav = context?.GetService<NpcNavMeshController>();
            nav?.SetFacingLocked(false);
            nav?.SetSpeedMultiplier(context?.GetService<NpcCombatConfig>()?.FleeSpeedMultiplier ?? 1.65f);

            var combat = context?.GetService<NpcCombatService>();
            context?.SetValue(NpcCombatStateKeys.FleeDamageTriggered, combat?.ShouldFleeFromDamageThreat == true);
            combat?.ClearDamageFleeRequest();
            combat?.SheatheWeapon();
            combat?.TrySelectFleeDestination();
            Move(context);
        }

        public override void Logic(StateMachineContext context)
        {
            Move(context);
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.ResetSpeed();
            context?.RemoveValue(NpcCombatStateKeys.FleeLookTimer);
            context?.RemoveValue(NpcCombatStateKeys.FleeLookingBack);
            context?.RemoveValue(NpcCombatStateKeys.FleeDamageTriggered);
            context?.RemoveValue(NpcCombatStateKeys.FleeCompleted);
        }

        private static void Move(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            var nav = context?.GetService<NpcNavMeshController>();
            if (combat == null || nav == null || context.Owner == null)
            {
                return;
            }

            combat.RefreshTargetVisibility();
            if (!combat.HasFleeDestination && !combat.TrySelectFleeDestination())
            {
                nav.Stop();
                combat.ClearTarget();
                context.SetValue(NpcCombatStateKeys.FleeCompleted, true);
                return;
            }

            var config = context.GetService<NpcCombatConfig>();
            var reachedDistance = config != null ? config.FleeReachedDistance : 0.75f;
            context.TryGetValue<bool>(NpcCombatStateKeys.FleeLookingBack, out var isLookingBack);
            if (!isLookingBack && !HasReachedFleeDestination(context, nav, combat.FleeDestination, reachedDistance))
            {
                context.SetValue(NpcCombatStateKeys.FleeLookTimer, 0f);
                if (!nav.MoveTo(combat.FleeDestination, stoppingDistance: reachedDistance))
                {
                    combat.ClearFleeDestination();
                }

                return;
            }

            context.SetValue(NpcCombatStateKeys.FleeLookingBack, true);
            nav.Stop();
            combat.FaceLastKnownPosition();

            context.TryGetValue<float>(NpcCombatStateKeys.FleeLookTimer, out var timer);
            timer += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.FleeLookTimer, timer);
            context.TryGetValue<bool>(NpcCombatStateKeys.FleeDamageTriggered, out var damageTriggered);
            var lookDuration = combat.GetFleeDecisionDuration(damageTriggered);
            if (timer < lookDuration)
            {
                return;
            }

            combat.ClearFleeDestination();
            combat.ScanForEnemy(true);
            if (combat.ShouldFlee)
            {
                context.SetValue(NpcCombatStateKeys.FleeLookTimer, 0f);
                context.SetValue(NpcCombatStateKeys.FleeLookingBack, false);
                context.SetValue(NpcCombatStateKeys.FleeDamageTriggered, false);
                if (combat.TrySelectFleeDestination())
                {
                    nav.MoveTo(combat.FleeDestination, stoppingDistance: reachedDistance);
                    return;
                }
            }

            combat.ClearTarget();
            context.SetValue(NpcCombatStateKeys.FleeCompleted, true);
        }

        private static bool HasReachedFleeDestination(
            StateMachineContext context,
            NpcNavMeshController nav,
            Vector3 destination,
            float reachedDistance)
        {
            if (nav.HasReachedDestination)
            {
                return true;
            }

            if (context.Owner == null)
            {
                return false;
            }

            var currentPosition = context.Owner.transform.position;
            currentPosition.y = 0f;
            destination.y = 0f;
            return Vector3.Distance(currentPosition, destination) <= reachedDistance;
        }

        private static void StoreHomeIfMissing(StateMachineContext context)
        {
            if (context?.Owner == null)
            {
                return;
            }

            if (!context.TryGetValue<Vector3>(NpcItemStateKeys.HomePosition, out _))
            {
                context.SetValue(NpcItemStateKeys.HomePosition, context.Owner.transform.position);
            }

            if (!context.TryGetValue<Quaternion>(NpcItemStateKeys.HomeRotation, out _))
            {
                context.SetValue(NpcItemStateKeys.HomeRotation, context.Owner.transform.rotation);
            }
        }
    }
}
