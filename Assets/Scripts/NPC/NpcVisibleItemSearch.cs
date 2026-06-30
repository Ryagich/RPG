using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NPC
{
    public static class NpcVisibleItemSearch
    {
        public static bool TryFindBestVisiblePickup(StateMachine.StateMachineContext context, out NpcItemPickupPlan bestPlan)
        {
            if (TryBuildVisiblePickupPlans(context, out var plans))
            {
                bestPlan = plans[0];
                return true;
            }

            bestPlan = null;
            return false;
        }

        public static bool TryBuildVisiblePickupPlans(StateMachine.StateMachineContext context, out List<NpcItemPickupPlan> plans)
        {
            plans = null;
            if (context == null)
            {
                return false;
            }

            var sensor = context.GetService<NpcVisionSensor>();
            var vision = context.GetService<NpcVision>();
            var planner = context.GetService<NpcInventoryPlanner>();
            if (sensor == null || vision == null || planner == null)
            {
                return false;
            }

            sensor.PruneInvalidCandidates();
            if (!sensor.HasItemCandidates)
            {
                return false;
            }

            plans = new List<NpcItemPickupPlan>();
            foreach (var item in sensor.ItemCandidates)
            {
                if (item == null || !item.CanInteractable || item.Config == null || !vision.IsInView(item.transform.position))
                {
                    continue;
                }

                if (planner.TryBuildPickupPlan(item, out var plan))
                {
                    plans.Add(plan);
                }
            }

            plans = plans
                .Where(plan => plan?.ItemHolder != null)
                .OrderByDescending(plan => plan.Gain)
                .ThenByDescending(plan => plan.CandidateScore)
                .ToList();
            return plans.Count > 0;
        }

        public static void SetPickupTarget(StateMachine.StateMachineContext context, NpcItemPickupPlan plan, Vector3 homePosition, Quaternion homeRotation)
        {
            if (context == null || plan?.ItemHolder == null)
            {
                return;
            }

            var config = context.GetService<NpcItemPickupConfig>();
            var eta = 0f;
            if (context.GetService<NpcNavMeshController>()?.TryCalculateEta(plan.ItemHolder.transform.position, out eta) != true)
            {
                eta = Vector3.Distance(context.Owner != null ? context.Owner.transform.position : homePosition, plan.ItemHolder.transform.position);
            }

            context.SetValue(NpcItemStateKeys.TargetItem, plan.ItemHolder);
            context.SetValue(NpcItemStateKeys.PickupPlan, plan);
            context.SetValue(NpcItemStateKeys.HomePosition, homePosition);
            context.SetValue(NpcItemStateKeys.HomeRotation, homeRotation);
            context.GetService<NpcItemInterest>()?.SetTarget(plan.ItemHolder, homePosition, eta + (config?.PickupDelay ?? 0f));
        }

        public static bool TryScanAndSetBestTarget(StateMachine.StateMachineContext context, Vector3 homePosition, Quaternion homeRotation)
        {
            if (!TryBuildVisiblePickupPlans(context, out var plans))
            {
                context?.RemoveValue(NpcItemStateKeys.PickupPlanQueue);
                return false;
            }

            var bestPlan = plans[0];
            var queue = plans.Skip(1).ToList();
            context.SetValue(NpcItemStateKeys.PickupPlanQueue, queue);
            SetPickupTarget(context, bestPlan, homePosition, homeRotation);
            return true;
        }

        public static bool TrySwitchToNextQueuedTarget(StateMachine.StateMachineContext context)
        {
            if (context == null
             || !context.TryGetValue<Vector3>(NpcItemStateKeys.HomePosition, out var homePosition)
             || !context.TryGetValue<Quaternion>(NpcItemStateKeys.HomeRotation, out var homeRotation))
            {
                return false;
            }

            if (context.TryGetValue<List<NpcItemPickupPlan>>(NpcItemStateKeys.PickupPlanQueue, out var queue))
            {
                while (queue.Count > 0)
                {
                    var nextPlan = queue[0];
                    queue.RemoveAt(0);
                    if (nextPlan?.ItemHolder == null || !nextPlan.ItemHolder.CanInteractable)
                    {
                        continue;
                    }

                    context.SetValue(NpcItemStateKeys.PickupPlanQueue, queue);
                    SetPickupTarget(context, nextPlan, homePosition, homeRotation);
                    return true;
                }
            }

            return TryScanAndSetBestTarget(context, homePosition, homeRotation);
        }
    }
}
