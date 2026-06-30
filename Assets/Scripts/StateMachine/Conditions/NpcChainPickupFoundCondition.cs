using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcChainPickupFoundCondition", menuName = "configs/StateMachine/Conditions/NPC Chain Pickup Found")]
    public sealed class NpcChainPickupFoundCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context != null
                && context.TryGetValue<bool>(NpcItemStateKeys.ChainPickupFound, out var found)
                && found;
        }
    }
}
