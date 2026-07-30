using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcKeepDistanceAttackReadyCondition", menuName = "configs/StateMachine/Conditions/NPC Keep Distance Attack Ready")]
    public sealed class NpcKeepDistanceAttackReadyCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            combat?.RefreshTargetVisibility();
            return combat?.ShouldAttackWhileKeepingDistance() == true;
        }
    }
}
