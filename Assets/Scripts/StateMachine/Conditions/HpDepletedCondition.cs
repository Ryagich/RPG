using StateMachine.Graph.Model;
using Stats;
using UnityEngine;

namespace StateMachine.Conditions
{
    [CreateAssetMenu(fileName = "HpDepletedCondition", menuName = "configs/StateMachine/Conditions/HP Depleted")]
    public sealed class HpDepletedCondition : BaseCondition
    {
        public override bool IsCondition(StateMachineContext context)
        {
            var statsController = context?.GetService<StatsController>();
            if (statsController == null)
            {
                return false;
            }

            var hp = statsController.Hp;
            return hp.Value.Value <= hp.Min;
        }
    }
}
