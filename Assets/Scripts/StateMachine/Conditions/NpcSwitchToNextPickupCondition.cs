using Inventory.Item;
using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcSwitchToNextPickupCondition", menuName = "configs/StateMachine/Conditions/NPC Switch To Next Pickup")]
    public sealed class NpcSwitchToNextPickupCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            if (context == null)
            {
                return false;
            }

            var hasMissingTarget = !context.TryGetValue<ItemHolder>(NpcItemStateKeys.TargetItem, out var item)
                                || item == null
                                || !item.CanInteractable;
            return hasMissingTarget && NpcVisibleItemSearch.TrySwitchToNextQueuedTarget(context);
        }
    }
}
