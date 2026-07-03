using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcCombatKeepDistanceBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Combat Keep Distance")]
    public sealed class NpcCombatKeepDistanceBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.SetFacingLocked(true);
            context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, false);
            context?.SetValue(NpcCombatStateKeys.KeepDistanceTimer, 0f);
            context?.SetValue(NpcCombatStateKeys.KeepDistanceNextRepositionTime, 0f);
            NpcCombatMoveProgress.Reset(context);

            var config = context?.GetService<NpcCombatConfig>();
            var minDuration = config != null ? config.KeepDistanceMinDuration : 1.2f;
            var maxDuration = Mathf.Max(minDuration, config != null ? config.KeepDistanceMaxDuration : 2.4f);
            context?.SetValue(NpcCombatStateKeys.KeepDistanceDuration, Random.Range(minDuration, maxDuration));

            Reposition(context, force: true);
        }

        public override void Logic(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            var nav = context?.GetService<NpcNavMeshController>();
            if (combat == null || nav == null || context.Owner == null || !combat.HasCombatTarget)
            {
                context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            combat.RefreshTargetVisibility();
            combat.FaceTarget();

            context.TryGetValue<float>(NpcCombatStateKeys.KeepDistanceTimer, out var timer);
            context.TryGetValue<float>(NpcCombatStateKeys.KeepDistanceDuration, out var duration);
            timer += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.KeepDistanceTimer, timer);
            if (timer >= duration)
            {
                nav.Stop();
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            var config = context.GetService<NpcCombatConfig>();
            var attackDelay = config != null ? config.KeepDistanceAttackDelay : 0.35f;
            if (combat.CanStartAttack && timer >= attackDelay)
            {
                nav.Stop();
                return;
            }

            Reposition(context, force: false);

            if (!combat.HasCombatMoveDestination)
            {
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            var reachedDistance = config?.CombatMoveReachedDistance ?? 0.45f;
            if (Vector3.Distance(context.Owner.transform.position, combat.CombatMoveDestination) <= reachedDistance)
            {
                nav.Stop();
                return;
            }

            if (NpcCombatMoveProgress.IsStuck(context, config))
            {
                combat.ClearCombatMoveDestination();
                NpcCombatMoveProgress.Reset(context);
                Reposition(context, force: true);
            }

            if (combat.HasCombatMoveDestination
             && nav.MoveTo(combat.CombatMoveDestination, stoppingDistance: reachedDistance))
            {
                return;
            }

            nav.Stop();
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<NpcCombatService>()?.ClearCombatMoveDestination();
            context?.RemoveValue(NpcCombatStateKeys.CombatMoveCompleted);
            context?.RemoveValue(NpcCombatStateKeys.KeepDistanceTimer);
            context?.RemoveValue(NpcCombatStateKeys.KeepDistanceDuration);
            context?.RemoveValue(NpcCombatStateKeys.KeepDistanceNextRepositionTime);
            NpcCombatMoveProgress.Clear(context);
        }

        private static void Reposition(StateMachineContext context, bool force)
        {
            if (context == null)
            {
                return;
            }

            context.TryGetValue<float>(NpcCombatStateKeys.KeepDistanceTimer, out var timer);
            context.TryGetValue<float>(NpcCombatStateKeys.KeepDistanceNextRepositionTime, out var nextTime);
            if (!force && timer < nextTime)
            {
                return;
            }

            var combat = context.GetService<NpcCombatService>();
            var config = context.GetService<NpcCombatConfig>();
            if (combat?.TrySelectKeepDistanceDestination() != true)
            {
                return;
            }

            var interval = config != null ? config.KeepDistanceRepositionInterval : 0.35f;
            context.SetValue(NpcCombatStateKeys.KeepDistanceNextRepositionTime, timer + Mathf.Max(0.05f, interval));
        }
    }
}
