using System.Collections.Generic;
using UnityEngine;

namespace StateMachine.Graph.Model
{
    [CreateAssetMenu(fileName = "State", menuName = "configs/StateMachine/State")]
    public class State : ScriptableObject
    {
        [field: SerializeField] public string Name { get; private set; }
        
        public List<BaseBehaviour> Behaviours = new();
        public List<Transition> Transitions = new();
    }
}
