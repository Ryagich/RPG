using Inventory.Item;
using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcReachedInterestedItemCondition", menuName = "configs/StateMachine/Conditions/NPC Reached Interested Item")]
    public sealed class NpcReachedInterestedItemCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            if (context == null || !context.TryGetValue<ItemHolder>(NpcItemStateKeys.TargetItem, out var item) || item == null)
            {
                return false;
            }

            var owner = context.Owner;
            if (owner == null)
            {
                return false;
            }

            var radius = context.GetService<NpcItemPickupConfig>()?.InteractionRadius ?? 1.2f;
            var distance = Vector3.Distance(owner.transform.position, item.transform.position);
            return distance <= radius;
        }
    }
}
