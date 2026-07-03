using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcDialogueEndedCondition", menuName = "configs/StateMachine/Conditions/NPC Dialogue Ended")]
    public sealed class NpcDialogueEndedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            return context?.GetService<NpcDialogueController>()?.IsDialogueRequested != true;
        }
    }
}
