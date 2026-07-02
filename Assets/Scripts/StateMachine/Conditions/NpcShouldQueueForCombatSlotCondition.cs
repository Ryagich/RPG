using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcShouldQueueForCombatSlotCondition", menuName = "configs/StateMachine/Conditions/NPC Should Queue For Combat Slot")]
    public sealed class NpcShouldQueueForCombatSlotCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<NpcCombatService>()?.ShouldQueueForCombatSlot() == true;
        }
    }
}
