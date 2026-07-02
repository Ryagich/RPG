using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcInitialCircleRequestedCondition", menuName = "configs/StateMachine/Conditions/NPC Initial Circle Requested")]
    public sealed class NpcInitialCircleRequestedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context != null
                   && context.TryGetValue<bool>(NpcCombatStateKeys.InitialCircleRequested, out var requested)
                   && requested;
        }
    }
}
