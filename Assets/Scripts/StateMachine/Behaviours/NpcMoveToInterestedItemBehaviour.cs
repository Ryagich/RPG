using Inventory.Item;
using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcMoveToInterestedItemBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Move To Interested Item")]
    public sealed class NpcMoveToInterestedItemBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcItemInterest>()?.SetState("MovingToItem");
            Move(context);
        }

        public override void Logic(StateMachineContext context)
        {
            Move(context);
        }

        private static void Move(StateMachineContext context)
        {
            if (context == null || !context.TryGetValue<ItemHolder>(NpcItemStateKeys.TargetItem, out var item) || item == null)
            {
                return;
            }

            var nav = context.GetService<NpcNavMeshController>();
            nav?.MoveTo(item.transform.position);

            if (nav != null && nav.TryCalculateEta(item.transform.position, out var eta))
            {
                var pickupDelay = context.GetService<NpcItemPickupConfig>()?.PickupDelay ?? 0f;
                context.GetService<NpcItemInterest>()?.UpdateEstimatedPickupTime(eta + pickupDelay);
            }
        }
    }
}
