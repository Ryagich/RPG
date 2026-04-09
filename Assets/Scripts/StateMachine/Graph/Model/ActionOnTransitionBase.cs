using UnityEngine;

namespace StateMachine.Graph.Model
{
    public abstract class ActionOnTransitionBase : ScriptableObject
    {
        public virtual void DoAction(StateMachineContext context) { }
    }
}