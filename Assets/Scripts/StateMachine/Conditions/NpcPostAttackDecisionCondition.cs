using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcPostAttackDecisionCondition", menuName = "configs/StateMachine/Conditions/NPC Post Attack Decision")]
    public sealed class NpcPostAttackDecisionCondition : BaseCondition
    {
        [SerializeField] private NpcCombatDecision expectedDecision;

        public NpcCombatDecision ExpectedDecision
        {
            get => expectedDecision;
            set => expectedDecision = value;
        }

        public override bool IsCondition(StateMachineContext context)
        {
            return context != null
                   && context.TryGetValue<NpcCombatDecision>(NpcCombatStateKeys.PostAttackDecision, out var decision)
                   && decision == expectedDecision;
        }
    }
}
