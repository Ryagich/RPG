using System.Linq;
using EditorTools;
using StateMachine.Graph.Model;

namespace StateMachine.Graph.Editor
{
    /// <summary>Queries project-wide ownership of transition assets before destructive operations.</summary>
    internal static class StateGraphTransitionReferenceIndex
    {
        public static int CountReferencesOutside(State excludedState, Transition transition)
        {
            if (transition == null)
            {
                return 0;
            }

            return GraphEditorAssetReferenceCounter.CountReferencesOutside(
                excludedState,
                transition,
                "t:State",
                state => state.Transitions ?? Enumerable.Empty<Transition>());
        }
    }
}
