using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcShouldFleeCondition", menuName = "configs/StateMachine/Conditions/NPC Should Flee")]
    public sealed class NpcShouldFleeCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            return combat != null && combat.ShouldFlee;
        }
    }
}
