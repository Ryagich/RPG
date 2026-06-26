using System.Collections.Generic;
using System.Linq;
using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Graph
{
    [CreateAssetMenu(fileName = "StateMachineGraph", menuName = "configs/StateMachine/Graph")]
    public class StateMachineGraph : ScriptableObject
    {
        public List<Node> Nodes = new();

        public State GetEntryState() => Nodes.FirstOrDefault(node => node?.State != null)?.State;
    }
}
