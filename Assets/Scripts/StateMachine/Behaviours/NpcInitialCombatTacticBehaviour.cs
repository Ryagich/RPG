using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Behaviours
{
    [CreateAssetMenu(fileName = "NpcInitialCombatTacticBehaviour", menuName = "configs/StateMachine/Behaviours/NPC Initial Combat Tactic")]
    public sealed class NpcInitialCombatTacticBehaviour : BaseBehaviour
    {
        public override void Enter(StateMachineContext context)
        {
            context?.RemoveValue(NpcCombatStateKeys.InitialCircleRequested);
            var combat = context?.GetService<NpcCombatService>();
            if (combat != null && combat.ShouldStartInitialCircle())
            {
                context.SetValue(NpcCombatStateKeys.InitialCircleRequested, true);
            }
        }
    }
}
