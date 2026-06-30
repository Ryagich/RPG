using Inventory.Item;
using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcLostItemCompetitionCondition", menuName = "configs/StateMachine/Conditions/NPC Lost Item Competition")]
    public sealed class NpcLostItemCompetitionCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            if (context == null
             || !context.TryGetValue<ItemHolder>(NpcItemStateKeys.TargetItem, out var targetItem)
             || targetItem == null)
            {
                return false;
            }

            var sensor = context.GetService<NpcVisionSensor>();
            var vision = context.GetService<NpcVision>();
            var nav = context.GetService<NpcNavMeshController>();
            var config = context.GetService<NpcItemPickupConfig>();
            if (sensor == null || vision == null || nav == null || config == null)
            {
                return false;
            }

            if (!nav.TryCalculateEta(targetItem.transform.position, out var ourEta))
            {
                return false;
            }

            ourEta += config.PickupDelay;
            context.GetService<NpcItemInterest>()?.UpdateEstimatedPickupTime(ourEta);

            sensor.PruneInvalidCandidates();
            foreach (var other in sensor.NpcCandidates)
            {
                if (other == null
                 || other.gameObject == context.Owner
                 || other.TargetItem != targetItem
                 || !vision.IsInView(other.transform.position))
                {
                    continue;
                }

                if (other.EstimatedPickupTime + config.EtaWinMarginSeconds < ourEta)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
