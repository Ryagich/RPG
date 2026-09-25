using System;
using System.Collections.Generic;

namespace EditorTools
{
    /// <summary>
    /// Builds the transient mapping from a graph's serialized asset to its visual node.
    /// The mapping belongs to editor presentation, not the graph asset itself.
    /// </summary>
    internal static class GraphEditorNodeLookup
    {
        public static void Rebuild<TNode, TAsset>(
            IEnumerable<TNode> nodes,
            IDictionary<TAsset, TNode> lookup,
            Func<TNode, TAsset> getAsset)
            where TNode : class
            where TAsset : class
        {
            lookup.Clear();
            if (nodes == null)
            {
                return;
            }

            foreach (TNode node in nodes)
            {
                TAsset asset = node == null ? null : getAsset(node);
                if (asset != null)
                {
                    lookup[asset] = node;
                }
            }
        }
    }
}
