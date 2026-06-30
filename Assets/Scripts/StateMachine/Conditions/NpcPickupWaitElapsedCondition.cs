using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcPickupWaitElapsedCondition", menuName = "configs/StateMachine/Conditions/NPC Pickup Wait Elapsed")]
    public sealed class NpcPickupWaitElapsedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            if (context == null)
            {
                return false;
            }

            context.TryGetValue<float>(NpcItemStateKeys.PickupWaitTimer, out var timer);
            var pickupDelay = context.GetService<NpcItemPickupConfig>()?.PickupDelay ?? 2f;
            return timer >= pickupDelay;
        }
    }
}
