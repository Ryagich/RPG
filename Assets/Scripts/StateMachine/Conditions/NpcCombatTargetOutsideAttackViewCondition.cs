using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcCombatTargetOutsideAttackViewCondition", menuName = "configs/StateMachine/Conditions/NPC Combat Target Outside Attack View")]
    public sealed class NpcCombatTargetOutsideAttackViewCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            combat?.RefreshTargetVisibility();
            return combat != null && combat.HasCombatTarget && !combat.CanStartAttack;
        }
    }
}
