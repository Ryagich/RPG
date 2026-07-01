using Combat;
using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcHitReactionBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Hit Reaction")]
    public sealed class NpcHitReactionBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.GetService<NpcItemInterest>()?.SetState("HitReaction");
            context?.GetService<NpcNavMeshController>()?.Stop();
        }

        public override void Logic(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.Stop();
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<CharacterActionState>()?.SetActionBlocked(false);
        }
    }
}
