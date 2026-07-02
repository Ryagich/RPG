using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcCombatTargetInAttackViewCondition", menuName = "configs/StateMachine/Conditions/NPC Combat Target In Attack View")]
    public sealed class NpcCombatTargetInAttackViewCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<NpcCombatService>()?.IsTargetInAttackView == true;
        }
    }
}
