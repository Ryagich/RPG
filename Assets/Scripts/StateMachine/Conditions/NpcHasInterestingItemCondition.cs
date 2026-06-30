using Inventory.Item;
using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcHasInterestingItemCondition", menuName = "configs/StateMachine/Conditions/NPC Has Interesting Item")]
    public sealed class NpcHasInterestingItemCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context != null
                && context.TryGetValue<ItemHolder>(NpcItemStateKeys.TargetItem, out var item)
                && item != null
                && item.CanInteractable;
        }
    }
}
