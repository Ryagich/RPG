using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcShouldSearchLastKnownTargetCondition", menuName = "configs/StateMachine/Conditions/NPC Should Search Last Known Target")]
    public sealed class NpcShouldSearchLastKnownTargetCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<NpcCombatService>()?.ShouldSearchLastKnownTarget == true;
        }
    }
}
