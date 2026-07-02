using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcCanFightTargetCondition", menuName = "configs/StateMachine/Conditions/NPC Can Fight Target")]
    public sealed class NpcCanFightTargetCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            return combat != null && combat.HasCombatTarget && combat.HasAnyWeaponAvailable;
        }
    }
}
