using System.Collections.Generic;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Decides which graph-node visuals belong in a retained canvas viewport.
    /// It deliberately owns no VisualElements, leaving visual lifecycle to the canvas.
    /// </summary>
    internal sealed class GraphNodeVirtualizer<TNode> where TNode : class
    {
        private readonly HashSet<TNode> visibleNodes = new();
        private readonly HashSet<TNode> pendingNodes = new();

        public bool HasPendingNodes => pendingNodes.Count > 0;

        public void Clear()
        {
            visibleNodes.Clear();
            pendingNodes.Clear();
        }

        public void Reconcile<TVisual>(IReadOnlyDictionary<TNode, Rect> nodeRects,
            IReadOnlyDictionary<TNode, TVisual> renderedNodes, TNode draggedNode, Rect visibleRect,
            List<TNode> nodesToRemove)
        {
            visibleNodes.Clear();
            foreach (KeyValuePair<TNode, Rect> pair in nodeRects)
            {
                if (ReferenceEquals(pair.Key, draggedNode) || pair.Value.Overlaps(visibleRect))
                {
                    visibleNodes.Add(pair.Key);
                }
            }

            nodesToRemove.Clear();
            foreach (TNode node in renderedNodes.Keys)
            {
                if (!visibleNodes.Contains(node))
                {
                    nodesToRemove.Add(node);
                }
            }

            pendingNodes.RemoveWhere(node => !visibleNodes.Contains(node));
            foreach (TNode node in visibleNodes)
            {
                if (!renderedNodes.ContainsKey(node))
                {
                    pendingNodes.Add(node);
                }
            }
        }

        public bool TryTakeNext<TVisual>(IReadOnlyDictionary<TNode, TVisual> renderedNodes, out TNode node)
        {
            foreach (TNode candidate in pendingNodes)
            {
                pendingNodes.Remove(candidate);
                if (visibleNodes.Contains(candidate) && !renderedNodes.ContainsKey(candidate))
                {
                    node = candidate;
                    return true;
                }

                break;
            }

            node = null;
            return false;
        }
    }
}
