using System;
using System.Collections.Generic;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Keeps cached retained/IMGUI node rectangles aligned with a graph's node collection.
    /// The algorithm comes from the dialog editor and is deliberately domain-agnostic.
    /// </summary>
    internal sealed class GraphEditorNodeLayoutSynchronizer<TNode>
        where TNode : class
    {
        private readonly HashSet<TNode> currentNodes = new();
        private readonly List<TNode> staleNodes = new();

        public bool Synchronize(
            IEnumerable<TNode> nodes,
            IDictionary<TNode, Rect> nodeRects,
            Func<TNode, Vector2> getPosition,
            Vector2 defaultNodeSize)
        {
            currentNodes.Clear();
            bool changed = false;
            if (nodes == null)
            {
                return false;
            }

            foreach (TNode node in nodes)
            {
                if (node == null)
                {
                    continue;
                }

                currentNodes.Add(node);
                Vector2 position = getPosition(node);
                if (!nodeRects.TryGetValue(node, out Rect rect))
                {
                    nodeRects[node] = new Rect(position, defaultNodeSize);
                    changed = true;
                    continue;
                }

                if (rect.position != position)
                {
                    rect.position = position;
                    nodeRects[node] = rect;
                    changed = true;
                }
            }

            staleNodes.Clear();
            foreach (TNode node in nodeRects.Keys)
            {
                if (!currentNodes.Contains(node))
                {
                    staleNodes.Add(node);
                }
            }

            foreach (TNode staleNode in staleNodes)
            {
                nodeRects.Remove(staleNode);
                changed = true;
            }

            return changed;
        }
    }
}
