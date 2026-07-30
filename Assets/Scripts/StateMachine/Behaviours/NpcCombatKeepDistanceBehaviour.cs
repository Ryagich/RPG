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
            var combat = context?.GetService<NpcCombatService>();
            combat?.BeginKeepDistanceOrbit();
            context?.SetValue(NpcCombatStateKeys.CombatMoveCompleted, false);
            context?.SetValue(NpcCombatStateKeys.KeepDistanceTimer, 0f);
            context?.SetValue(NpcCombatStateKeys.KeepDistanceNextRepositionTime, 0f);
            context?.RemoveValue(NpcCombatStateKeys.PostAttackDecision);
            NpcCombatMoveProgress.Reset(context);

            var config = context?.GetService<NpcCombatConfig>();
            var minDuration = config != null ? config.KeepDistanceMinDuration : 1.2f;
            var maxDuration = Mathf.Max(minDuration, config != null ? config.KeepDistanceMaxDuration : 2.4f);
            context?.SetValue(NpcCombatStateKeys.KeepDistanceDuration, Random.Range(minDuration, maxDuration));

            if (combat?.ShouldAttackWhileKeepingDistance() != true
                && combat?.ShouldEvadeWhileKeepingDistance() != true)
            {
                Reposition(context, force: true);
            }
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

            if (combat.ShouldEvadeWhileKeepingDistance())
            {
                nav.Stop();
                context.SetValue(NpcCombatStateKeys.PostAttackDecision, NpcCombatDecision.Evade);
                return;
            }

            if (combat.ShouldAttackWhileKeepingDistance())
            {
                nav.Stop();
                return;
            }

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

            Reposition(context, force: false);

            if (!combat.HasCombatMoveDestination)
            {
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            var reachedDistance = config?.CombatMoveReachedDistance ?? 0.45f;
            if (Vector3.Distance(context.Owner.transform.position, combat.CombatMoveDestination) <= reachedDistance)
            {
                // Do not spend the rest of this state standing at a completed point. Pick the
                // next point on the same arc immediately so "keep distance" visibly reads as
                // circling the opponent, not as a frozen stare.
                combat.ClearCombatMoveDestination();
                NpcCombatMoveProgress.Reset(context);
                if (Reposition(context, force: true)
                 && nav.MoveTo(combat.CombatMoveDestination, stoppingDistance: reachedDistance))
                {
                    return;
                }

                nav.Stop();
                context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                return;
            }

            if (NpcCombatMoveProgress.IsStuck(context, config))
            {
                combat.ClearCombatMoveDestination();
                NpcCombatMoveProgress.Reset(context);
                if (!Reposition(context, force: true))
                {
                    nav.Stop();
                    context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
                    return;
                }
            }

            if (combat.HasCombatMoveDestination
             && nav.MoveTo(combat.CombatMoveDestination, stoppingDistance: reachedDistance))
            {
                return;
            }

            // A failed path must not turn a tactical state into a static wait until its timer
            // expires. Retry from a fresh orbit point once, then return to the decision node.
            combat.ClearCombatMoveDestination();
            NpcCombatMoveProgress.Reset(context);
            if (Reposition(context, force: true)
             && nav.MoveTo(combat.CombatMoveDestination, stoppingDistance: reachedDistance))
            {
                return;
            }

            nav.Stop();
            context.SetValue(NpcCombatStateKeys.CombatMoveCompleted, true);
        }

        public override void Exit(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            combat?.ClearCombatMoveDestination();
            combat?.EndKeepDistanceOrbit();
            context?.RemoveValue(NpcCombatStateKeys.CombatMoveCompleted);
            context?.RemoveValue(NpcCombatStateKeys.KeepDistanceTimer);
            context?.RemoveValue(NpcCombatStateKeys.KeepDistanceDuration);
            context?.RemoveValue(NpcCombatStateKeys.KeepDistanceNextRepositionTime);
            context?.RemoveValue(NpcCombatStateKeys.PostAttackDecision);
            NpcCombatMoveProgress.Clear(context);
        }

        private static bool Reposition(StateMachineContext context, bool force)
        {
            if (context == null)
            {
                return false;
            }

            context.TryGetValue<float>(NpcCombatStateKeys.KeepDistanceTimer, out var timer);
            context.TryGetValue<float>(NpcCombatStateKeys.KeepDistanceNextRepositionTime, out var nextTime);
            if (!force && timer < nextTime)
            {
                return context.GetService<NpcCombatService>()?.HasCombatMoveDestination == true;
            }

            var combat = context.GetService<NpcCombatService>();
            var config = context.GetService<NpcCombatConfig>();
            if (combat?.TrySelectKeepDistanceDestination() != true)
            {
                return false;
            }

            var interval = config != null ? config.KeepDistanceRepositionInterval : 0.35f;
            context.SetValue(NpcCombatStateKeys.KeepDistanceNextRepositionTime, timer + Mathf.Max(0.05f, interval));
            return true;
        }
    }
}
