using System.Collections.Generic;
using System.Linq;
using EditorTools;
using StateMachine.Graph.Model;
using UnityEditor;

namespace StateMachine.Graph.Editor
{
    /// <summary>Read-only validation of graph invariants and runtime-relevant links.</summary>
    internal static class StateMachineGraphValidator
    {
        public static IReadOnlyList<GraphEditorValidationIssue> Validate(StateMachineGraph graph)
        {
            var issues = new List<GraphEditorValidationIssue>();
            if (graph == null)
            {
                return issues;
            }

            if (graph.Nodes == null || graph.Nodes.Count == 0)
            {
                issues.Add(Error("Graph has no start state."));
                return issues;
            }

            var states = new HashSet<State>();
            foreach (Node node in graph.Nodes)
            {
                if (node?.State == null || !AssetDatabase.Contains(node.State))
                {
                    issues.Add(Error("Graph contains a node with a missing State asset."));
                    continue;
                }

                if (!states.Add(node.State))
                {
                    issues.Add(Error($"State \"{node.State.name}\" is assigned to more than one node."));
                }
            }

            State startState = StateGraphStructureOperations.GetStartState(graph);
            if (startState == null || !states.Contains(startState))
            {
                issues.Add(Error("The first graph node must reference a valid start state."));
                return issues;
            }

            foreach (State state in states)
            {
                foreach (Transition transition in state.Transitions ?? Enumerable.Empty<Transition>())
                {
                    if (transition == null || !AssetDatabase.Contains(transition))
                    {
                        issues.Add(Error($"State \"{state.name}\" contains a missing Transition asset."));
                    }
                    else if (transition.TargetState == null)
                    {
                        issues.Add(Error($"Transition \"{transition.name}\" has no target state."));
                    }
                    else if (!states.Contains(transition.TargetState))
                    {
                        issues.Add(Error($"Transition \"{transition.name}\" targets a state outside this graph."));
                    }
                }
            }

            var reachable = new HashSet<State> { startState };
            var pending = new Queue<State>();
            pending.Enqueue(startState);
            while (pending.Count > 0)
            {
                State state = pending.Dequeue();
                foreach (Transition transition in state.Transitions ?? Enumerable.Empty<Transition>())
                {
                    if (transition != null && AssetDatabase.Contains(transition) &&
                        transition.TargetState != null && states.Contains(transition.TargetState) &&
                        reachable.Add(transition.TargetState))
                    {
                        pending.Enqueue(transition.TargetState);
                    }
                }
            }

            foreach (State state in states.Where(state => !reachable.Contains(state)))
            {
                issues.Add(new GraphEditorValidationIssue(
                    GraphEditorValidationSeverity.Warning,
                    $"State \"{state.name}\" is unreachable from the start state."));
            }

            return issues;
        }

        private static GraphEditorValidationIssue Error(string message) =>
            new(GraphEditorValidationSeverity.Error, message);
    }
}
