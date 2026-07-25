using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcVisibleThreatWithoutWeaponCondition", menuName = "configs/StateMachine/Conditions/NPC Visible Threat Without Weapon")]
    public sealed class NpcVisibleThreatWithoutWeaponCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            combat?.RefreshTargetVisibility();
            return combat != null && combat.HasCombatTarget && combat.IsTargetVisible && !combat.HasAnyWeaponAvailable;
        }
    }
}
