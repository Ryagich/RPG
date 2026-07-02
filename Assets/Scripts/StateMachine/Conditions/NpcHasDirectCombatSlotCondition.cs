using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcHasDirectCombatSlotCondition", menuName = "configs/StateMachine/Conditions/NPC Has Direct Combat Slot")]
    public sealed class NpcHasDirectCombatSlotCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<NpcCombatService>()?.HasDirectCombatSlot() == true;
        }
    }
}
