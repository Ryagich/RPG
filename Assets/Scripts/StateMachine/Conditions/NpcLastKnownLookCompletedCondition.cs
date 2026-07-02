using NPC;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "NpcLastKnownLookCompletedCondition", menuName = "configs/StateMachine/Conditions/NPC Last Known Look Completed")]
    public sealed class NpcLastKnownLookCompletedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            if (context == null)
            {
                return false;
            }

            var combat = context.GetService<NpcCombatService>();
            if (combat == null || !combat.HasLastKnownTargetPosition)
            {
                return true;
            }

            var reached = Vector3.Distance(context.Owner.transform.position, combat.LastKnownTargetPosition)
                       <= (context.GetService<NpcCombatConfig>()?.LastKnownReachedDistance ?? 1.2f);
            if (!reached)
            {
                return false;
            }

            combat.FaceLastKnownPosition();
            context.TryGetValue<float>(NpcCombatStateKeys.LastKnownLookTimer, out var timer);
            timer += context.DeltaTime;
            context.SetValue(NpcCombatStateKeys.LastKnownLookTimer, timer);
            return timer >= (context.GetService<NpcCombatConfig>()?.LookAtLastKnownDuration ?? 2f);
        }
    }
}
