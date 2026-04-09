using StateMachine.Graph.Model;
using UnityEngine;

namespace StateMachine.Graph
{
    [System.Serializable]
    public class Node
    {
        public Vector2 Position;
        public State State;

        public Node(State state)
        {
            State = state;
        }
    }
}