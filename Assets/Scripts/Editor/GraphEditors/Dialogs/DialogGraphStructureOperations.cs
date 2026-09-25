using System.Collections.Generic;
using UnityEditor;

namespace Dialogs.Graph.Editor
{
    /// <summary>
    /// Repairs only the serialized container invariants of a dialogue graph. Rendering,
    /// selection and cache invalidation remain the window's responsibility.
    /// </summary>
    internal static class DialogGraphStructureOperations
    {
        public static bool EnsureNodes(DialogGraph graph)
        {
            if (graph == null || graph.Nodes != null)
            {
                return false;
            }

            graph.Nodes = new List<DialogNode>();
            return true;
        }

        public static bool RemoveMissingNodes(DialogGraph graph)
        {
            if (graph?.Nodes == null)
            {
                return false;
            }

            bool changed = false;
            for (int index = graph.Nodes.Count - 1; index >= 0; index--)
            {
                DialogNode node = graph.Nodes[index];
                if (node == null || node.Phrase == null || !AssetDatabase.Contains(node.Phrase))
                {
                    if (node != null && graph.IsEntryPhrase(node.Phrase))
                    {
                        graph.SetEntryPhrase(null);
                    }

                    graph.Nodes.RemoveAt(index);
                    changed = true;
                }
            }

            return changed;
        }
    }
}
