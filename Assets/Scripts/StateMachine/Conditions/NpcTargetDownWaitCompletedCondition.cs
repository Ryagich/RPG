using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcTargetDownWaitCompletedCondition", menuName = "configs/StateMachine/Conditions/NPC Target Down Wait Completed")]
    public sealed class NpcTargetDownWaitCompletedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context != null
                   && context.TryGetValue<bool>(NpcCombatStateKeys.TargetDownWaitCompleted, out var completed)
                   && completed;
        }
    }
}
