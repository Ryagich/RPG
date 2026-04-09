using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StateMachine.Graph.Model
{
    [CreateAssetMenu(fileName = "Transition", menuName = "configs/StateMachine/Transition")]
    public class Transition : ScriptableObject
    {
        [field: SerializeField] public TransitionType Type = TransitionType.All;
        [field: SerializeField] public List<BaseCondition> Conditions { get; private set; } = new();
        [field: SerializeField] public List<ActionOnTransitionBase> ActionOnTransitions { get; private set; } = new();
        [field: SerializeField] public State TargetState;
        
        public bool CanTransition(StateMachineContext context)
        {
            switch (Type)
            {
                case TransitionType.All:
                    return Conditions.All(condition => condition.IsCondition(context));
                case TransitionType.Any:
                    return Conditions.Any(condition => condition.IsCondition(context));
                default:
                    return false;
            }
        }
    }

    public enum TransitionType
    {
        Any,
        All
    }
}