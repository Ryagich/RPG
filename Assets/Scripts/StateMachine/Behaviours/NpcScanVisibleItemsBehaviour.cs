using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcScanVisibleItemsBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Scan Visible Items")]
    public sealed class NpcScanVisibleItemsBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.RemoveValue(NpcItemStateKeys.TargetItem);
            context?.RemoveValue(NpcItemStateKeys.PickupPlan);
            context?.RemoveValue(NpcItemStateKeys.PickupPlanQueue);
            context?.RemoveValue(NpcItemStateKeys.PickupCompleted);
            context?.RemoveValue(NpcItemStateKeys.ChainPickupFound);
            context?.SetValue(NpcItemStateKeys.ScanTimer, 0f);
            context?.GetService<NpcItemInterest>()?.Clear();
        }

        public override void Logic(StateMachineContext context)
        {
            if (context == null)
            {
                return;
            }

            var sensor = context.GetService<NpcVisionSensor>();
            var vision = context.GetService<NpcVision>();
            var config = context.GetService<NpcItemPickupConfig>();
            if (sensor == null || vision == null || config == null)
            {
                return;
            }

            sensor.PruneInvalidCandidates();
            if (!sensor.HasItemCandidates)
            {
                context.SetValue(NpcItemStateKeys.ScanTimer, 0f);
                return;
            }

            context.TryGetValue<float>(NpcItemStateKeys.ScanTimer, out var timer);
            timer += context.DeltaTime;
            if (timer < config.ScanInterval)
            {
                context.SetValue(NpcItemStateKeys.ScanTimer, timer);
                return;
            }

            context.SetValue(NpcItemStateKeys.ScanTimer, 0f);
            var home = context.Owner != null ? context.Owner.transform.position : Vector3.zero;
            var rotation = context.Owner != null ? context.Owner.transform.rotation : Quaternion.identity;
            NpcVisibleItemSearch.TryScanAndSetBestTarget(context, home, rotation);
        }
    }
}
