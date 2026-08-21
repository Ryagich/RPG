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
            var nav = context?.GetService<NpcNavMeshController>();
            nav?.ResetSpeed();
            nav?.Stop();
            // A hit reaction owns the full-body interruption. Requesting a draw here lets the
            // Weapon Handling layer fire its transfer event while the hit clip is still active.
            // The next combat/search state prepares the weapon after this reaction has ended.
            FaceThreat(context);
        }

        public override void Logic(StateMachineContext context)
        {
            context?.GetService<NpcNavMeshController>()?.Stop();
            FaceThreat(context);
        }

        public override void Exit(StateMachineContext context)
        {
            context?.GetService<CharacterActionState>()?.SetActionBlocked(false);
        }

        private static void FaceThreat(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            if (combat == null)
            {
                return;
            }

            if (combat.HasCombatTarget)
            {
                combat.FaceTarget();
            }
            else
            {
                combat.FaceLastKnownPosition();
            }
        }
    }
}
