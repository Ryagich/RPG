using System.Collections.Generic;
using System.Linq;
using StateMachine.Graph.Model;
using UnityEditor;

namespace StateMachine.Graph.Editor
{
    /// <summary>Queries and repairs state-machine graph-container invariants outside window UI.</summary>
    internal static class StateGraphStructureOperations
    {
        public static bool EnsureNodes(StateMachineGraph graph)
        {
            if (graph == null || graph.Nodes != null) return false;
            graph.Nodes = new List<Node>();
            return true;
        }

        public static bool RemoveMissingNodes(StateMachineGraph graph)
        {
            if (graph?.Nodes == null) return false;
            bool changed = false;
            for (int index = graph.Nodes.Count - 1; index >= 0; index--)
            {
                Node node = graph.Nodes[index];
                if (node == null || node.State == null || !AssetDatabase.Contains(node.State))
                {
                    graph.Nodes.RemoveAt(index);
                    changed = true;
                }
            }

            return changed;
        }

        public static State GetStartState(StateMachineGraph graph) =>
            graph?.Nodes != null && graph.Nodes.Count > 0 ? graph.Nodes[0]?.State : null;

        public static bool ContainsState(StateMachineGraph graph, State state) =>
            graph?.Nodes != null && graph.Nodes.Any(node => node?.State == state);

        public static bool IsStartNode(StateMachineGraph graph, Node node) =>
            graph?.Nodes != null && graph.Nodes.Count > 0 && graph.Nodes[0] == node && node?.State != null;

        public static bool IsOrphanState(StateMachineGraph graph, State state)
        {
            if (graph?.Nodes == null || state == null || GetStartState(graph) == state) return false;
            return !graph.Nodes.Where(node => node?.State != null)
                .SelectMany(node => node.State.Transitions ?? new List<Transition>())
                .Any(transition => transition != null && transition.TargetState == state);
        }
    }
}
