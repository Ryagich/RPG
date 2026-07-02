using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcPostAttackDecisionBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Post Attack Decision")]
    public sealed class NpcPostAttackDecisionBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            var decision = combat != null ? combat.SelectPostAttackDecision() : NpcCombatDecision.Approach;
            context?.SetValue(NpcCombatStateKeys.PostAttackDecision, decision);
        }

        public override void Exit(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.PostAttackDecision);
        }
    }
}
