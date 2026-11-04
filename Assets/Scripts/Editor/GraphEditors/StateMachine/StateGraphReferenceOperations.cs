using System.Collections.Generic;
using System.Linq;
using EditorTools;
using StateMachine.Graph.Model;

namespace StateMachine.Graph.Editor
{
    /// <summary>
    /// Maintains state-machine graph references and owned transition assets independently
    /// from editor-window interaction flow.
    /// </summary>
    internal static class StateGraphReferenceOperations
    {
        public static void RemoveIncomingReferences(StateMachineGraph graph, State target)
        {
            UpdateIncomingReferences(graph, target, null, "Remove state reference");
        }

        public static void ReplaceIncomingReferences(StateMachineGraph graph, State oldState, State newState)
        {
            if (oldState != newState)
            {
                UpdateIncomingReferences(graph, oldState, newState, "Replace state reference");
            }
        }

        /// <summary>
        /// Deletes only transition assets that cease to have project-wide references when the
        /// owning State is removed. Shared transition assets are intentionally retained.
        /// </summary>
        public static void DeleteUnsharedTransitions(State state)
        {
            if (state == null)
            {
                return;
            }

            EnsureTransitions(state);
            foreach (Transition transition in state.Transitions.ToList())
            {
                if (transition != null && StateGraphTransitionReferenceIndex.CountReferencesOutside(state, transition) == 0)
                {
                    GraphEditorAssetService.DeleteAsset(transition, "Delete state transition");
                }
            }

            GraphEditorAssetService.MarkDirty(state, "Remove state transition references");
            state.Transitions.Clear();
        }

        public static bool CanDeleteTransitionAsset(Transition transition) =>
            transition != null && StateGraphTransitionReferenceIndex.CountReferencesOutside(null, transition) == 0;

        private static void UpdateIncomingReferences(
            StateMachineGraph graph,
            State oldState,
            State newState,
            string undoLabel)
        {
            if (graph?.Nodes == null || oldState == null)
            {
                return;
            }

            foreach (Node node in graph.Nodes)
            {
                State state = node?.State;
                if (state == null)
                {
                    continue;
                }

                EnsureTransitions(state);
                foreach (Transition transition in state.Transitions)
                {
                    if (transition != null && transition.TargetState == oldState)
                    {
                        transition.TargetState = newState;
                        GraphEditorAssetService.MarkDirty(transition, undoLabel);
                    }
                }
            }
        }

        private static void EnsureTransitions(State state) => state.Transitions ??= new List<Transition>();
    }
}
