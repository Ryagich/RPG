using System;
using UnityEngine;

namespace StateMachine.Graph.Model
{
    public abstract class BaseBehaviour : ScriptableObject, IEquatable<BaseBehaviour>
    {
        public virtual void Enter(StateMachineContext context) { }
        public virtual void Logic(StateMachineContext context) { }
        public virtual void Exit(StateMachineContext context) { }
        
        public bool Equals(BaseBehaviour other)
        {
            return other != null && other.GetType() == GetType();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != GetType())
            {
                return false;
            }
            return Equals((BaseBehaviour)obj);
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }
    }
}