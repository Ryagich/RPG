using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcDialogueRequestedCondition", menuName = "configs/StateMachine/Conditions/NPC Dialogue Requested")]
    public sealed class NpcDialogueRequestedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<NpcDialogueController>()?.IsDialogueRequested == true;
        }
    }
}
