using System.Linq;
using EditorTools;
using Quests.Graph.Model;

namespace Quests.Editor
{
    /// <summary>Project-wide quest-transition reference query for safe asset deletion.</summary>
    internal static class QuestGraphTransitionReferenceIndex
    {
        public static int CountReferencesOutside(QuestNodeData excludedNode, QuestTransition transition)
        {
            return GraphEditorAssetReferenceCounter.CountReferencesOutside(
                excludedNode,
                transition,
                "t:QuestNodeData",
                node => node.Transitions ?? Enumerable.Empty<QuestTransition>());
        }
    }
}
