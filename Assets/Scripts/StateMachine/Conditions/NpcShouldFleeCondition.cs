using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcShouldFleeCondition", menuName = "configs/StateMachine/Conditions/NPC Should Flee")]
    public sealed class NpcShouldFleeCondition : BaseCondition
    {
        public override bool Enter(StateMachineContext context)
        {
            context?.SetValue(NpcCombatStateKeys.FleeDecisionTimer, 0f);
            return false;
        }

        public override bool IsCondition(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            if (combat == null)
            {
                ResetDecisionTimer(context);
                return false;
            }

            if (combat.ShouldFleeFromDamageThreat)
            {
                ResetDecisionTimer(context);
                return true;
            }

            if (!combat.ShouldFlee)
            {
                ResetDecisionTimer(context);
                return false;
            }

            context.TryGetValue<float>(NpcCombatStateKeys.FleeDecisionTimer, out var timer);
            timer += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.FleeDecisionTimer, timer);
            if (timer < combat.GetFleeDecisionDuration())
            {
                return false;
            }

            ResetDecisionTimer(context);
            return true;
        }

        private static void ResetDecisionTimer(StateMachineContext context)
        {
            context?.SetValue(NpcCombatStateKeys.FleeDecisionTimer, 0f);
        }
    }
}
