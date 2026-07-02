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
            context?.RemoveValue(NpcCombatStateKeys.FleeCompleted);
            context?.SetValue(NpcCombatStateKeys.FleeLookTimer, 0f);
            context?.GetService<NpcItemInterest>()?.Clear();
            StoreHomeIfMissing(context);
            var nav = context?.GetService<NpcNavMeshController>();
            nav?.SetFacingLocked(false);
            nav?.SetSpeedMultiplier(context?.GetService<NpcCombatConfig>()?.FleeSpeedMultiplier ?? 1.65f);

            var combat = context?.GetService<NpcCombatService>();
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

            if (!combat.HasFleeDestination && !combat.TrySelectFleeDestination())
            {
                context.SetValue(NpcCombatStateKeys.FleeCompleted, true);
                return;
            }

            var config = context.GetService<NpcCombatConfig>();
            var reachedDistance = config != null ? config.FleeReachedDistance : 0.75f;
            var distanceToDestination = Vector3.Distance(context.Owner.transform.position, combat.FleeDestination);
            if (distanceToDestination > reachedDistance)
            {
                context.SetValue(NpcCombatStateKeys.FleeLookTimer, 0f);
                nav.MoveTo(combat.FleeDestination, stoppingDistance: reachedDistance);
                return;
            }

            nav.Stop();
            combat.FaceLastKnownPosition();

            context.TryGetValue<float>(NpcCombatStateKeys.FleeLookTimer, out var timer);
            timer += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.FleeLookTimer, timer);
            var lookDuration = config != null ? config.FleeLookBackDuration : 1.5f;
            if (timer < lookDuration)
            {
                return;
            }

            combat.ClearFleeDestination();
            combat.ScanForEnemy(true);
            if (combat.IsTargetVisible && combat.ShouldFlee)
            {
                context.SetValue(NpcCombatStateKeys.FleeLookTimer, 0f);
                combat.TrySelectFleeDestination();
                return;
            }

            combat.ClearTarget();
            context.SetValue(NpcCombatStateKeys.FleeCompleted, true);
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
