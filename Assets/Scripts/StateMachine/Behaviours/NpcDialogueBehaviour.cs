using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcDialogueBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Dialogue")]
    public sealed class NpcDialogueBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcDialogueController>()?.EnterDialogueState();
        }

        public override void Logic(StateMachineContext context)
        {
            context?.GetService<NpcDialogueController>()?.TickDialogue();
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<NpcDialogueController>()?.ExitDialogueState();
        }
    }
}
