using EditorTools;
using StateMachine.Graph.Model;
using UnityEditor;
using UnityEngine;

namespace StateMachine.Graph.Editor
{
    /// <summary>Performs asset and graph mutations without owning window interaction state.</summary>
    internal static class StateMachineGraphEditorCommands
    {
        public static StateMachineGraph CreateGraph()
        {
            var graph = ScriptableObject.CreateInstance<StateMachineGraph>();
            GraphEditorAssetService.CreateProjectAsset(graph, "NewStateMachineGraph.asset", "Create state machine graph");
            return graph;
        }

        public static bool TryCreateState(StateMachineGraph graph, string folderPath, Vector2 position, out Node node)
        {
            node = null;
            if (graph == null || !GraphEditorAssetService.EnsureFolderExists(folderPath, "Please specify the folder for saving states."))
            {
                return false;
            }

            StateGraphStructureOperations.EnsureNodes(graph);
            var state = ScriptableObject.CreateInstance<State>();
            GraphEditorAssetService.CreateAsset(state, folderPath, $"State_{graph.Nodes.Count}.asset", "Create state");
            node = new Node(state) { Position = position };
            GraphEditorAssetService.MarkDirty(graph, "Add state node");
            graph.Nodes.Add(node);
            return true;
        }

        public static bool TryCreateTransition(State state, string folderPath)
        {
            if (state == null || !GraphEditorAssetService.EnsureFolderExists(folderPath, "Please specify the folder for saving transitions."))
            {
                return false;
            }

            state.Transitions ??= new System.Collections.Generic.List<Transition>();
            var transition = ScriptableObject.CreateInstance<Transition>();
            GraphEditorAssetService.CreateAsset(
                transition,
                folderPath,
                $"{state.name}_Transition_{state.Transitions.Count}.asset",
                "Create state transition");
            GraphEditorAssetService.MarkDirty(state, "Add state transition");
            state.Transitions.Add(transition);
            return true;
        }

        public static Transition RemoveTransitionReference(State state, int index)
        {
            if (state?.Transitions == null || index < 0 || index >= state.Transitions.Count)
            {
                return null;
            }

            Transition transition = state.Transitions[index];
            GraphEditorAssetService.MarkDirty(state, "Remove state transition");
            state.Transitions.RemoveAt(index);
            return transition;
        }

        public static bool RemoveNode(StateMachineGraph graph, Node node)
        {
            if (graph?.Nodes == null || node == null || !graph.Nodes.Remove(node))
            {
                return false;
            }

            GraphEditorAssetService.MarkDirty(graph, "Delete state node");
            return true;
        }
    }
}
