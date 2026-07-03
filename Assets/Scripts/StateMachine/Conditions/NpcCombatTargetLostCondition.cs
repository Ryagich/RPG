using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcCombatTargetLostCondition", menuName = "configs/StateMachine/Conditions/NPC Combat Target Lost")]
    public sealed class NpcCombatTargetLostCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            combat?.RefreshTargetVisibility();
            return combat != null
                   && ((combat.HasCombatTarget && !combat.IsTargetVisible)
                       || (!combat.HasCombatTarget && combat.HasLastKnownTargetPosition));
        }
    }
}
