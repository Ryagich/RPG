using Inventory.Item;
using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcInterestedItemMissingCondition", menuName = "configs/StateMachine/Conditions/NPC Interested Item Missing")]
    public sealed class NpcInterestedItemMissingCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context == null
                || !context.TryGetValue<ItemHolder>(NpcItemStateKeys.TargetItem, out var item)
                || item == null
                || !item.CanInteractable;
        }
    }
}
