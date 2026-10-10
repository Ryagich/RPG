using Monsters;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "PlantMonsterReachedLastKnownTargetPositionCondition", menuName = "configs/StateMachine/Conditions/Plant Monster Reached Last Known Position")]
    public sealed class PlantMonsterReachedLastKnownTargetPositionCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var targeting = context?.GetService<PlantMonsterTargetingController>();
            var settings = context?.GetService<PlantMonsterSearchConfig>();
            if (context?.Owner == null || targeting?.HasLastKnownTargetPosition != true)
            {
                return false;
            }

            var delta = targeting.LastKnownTargetPosition - context.Owner.transform.position;
            delta.y = 0f;
            var distance = settings != null ? settings.ReachedDistance : 1.2f;
            return delta.sqrMagnitude <= distance * distance;
        }
    }
}
