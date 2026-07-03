using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcKeepDistanceAttackReadyCondition", menuName = "configs/StateMachine/Conditions/NPC Keep Distance Attack Ready")]
    public sealed class NpcKeepDistanceAttackReadyCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var combat = context?.GetService<NpcCombatService>();
            combat?.RefreshTargetVisibility();
            if (combat?.CanStartAttack != true)
            {
                return false;
            }

            context.TryGetValue<float>(NpcCombatStateKeys.KeepDistanceTimer, out var timer);
            var delay = context.GetService<NpcCombatConfig>()?.KeepDistanceAttackDelay ?? 0.35f;
            return timer >= delay;
        }
    }
}
