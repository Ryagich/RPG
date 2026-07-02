using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcCombatTargetDownCondition", menuName = "configs/StateMachine/Conditions/NPC Combat Target Down")]
    public sealed class NpcCombatTargetDownCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<NpcCombatService>()?.IsCurrentTargetDown == true;
        }
    }
}
