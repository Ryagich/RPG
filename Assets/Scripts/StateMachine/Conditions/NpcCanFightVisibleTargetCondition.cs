using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcCanFightVisibleTargetCondition", menuName = "configs/StateMachine/Conditions/NPC Can Fight Visible Target")]
    public sealed class NpcCanFightVisibleTargetCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            return combat != null && combat.HasCombatTarget && combat.IsTargetVisible && combat.HasAnyWeaponAvailable;
        }
    }
}
