using UnityEngine;

namespace StateMachine.Graph.Model
{
    public abstract class BaseCondition : ScriptableObject
    {
        public virtual bool Enter(StateMachineContext context)
        {
            return false;
        }
        
        public virtual bool IsCondition(StateMachineContext context)
        {
            return false;
        }
    }
}