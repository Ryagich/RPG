using System;
using System.Collections.Generic;
using UnityEngine;

namespace EditorTools
{
    /// <summary>Recovers persisted node positions that are no longer reachable in a viewport.</summary>
    internal static class GraphEditorNodePositionRecovery
    {
        public static int ClampToViewport<TNode>(
            IEnumerable<TNode> nodes,
            Func<TNode, Vector2> getPosition,
            Action<TNode, Vector2> setPosition,
            Rect viewport,
            Vector2 nodeSize,
            float minimumVisibleHeight)
            where TNode : class
        {
            if (nodes == null || getPosition == null || setPosition == null || viewport.width <= 0f || viewport.height <= 0f)
            {
                return 0;
            }

            int recoveredCount = 0;
            foreach (TNode node in nodes)
            {
                if (node == null)
                {
                    continue;
                }

                Vector2 position = getPosition(node);
                Vector2 clampedPosition = ClampPosition(position, viewport, nodeSize, minimumVisibleHeight);
                if (!GraphConnectionGeometry.ApproximatelyEqual(position, clampedPosition))
                {
                    setPosition(node, clampedPosition);
                    recoveredCount++;
                }
            }

            return recoveredCount;
        }

        public static Vector2 ClampPosition(Vector2 position, Rect viewport, Vector2 nodeSize, float minimumVisibleHeight)
        {
            float maxX = Mathf.Max(viewport.xMin, viewport.xMax - nodeSize.x);
            float maxY = Mathf.Max(viewport.yMin, viewport.yMax - minimumVisibleHeight);
            return new Vector2(
                Mathf.Clamp(position.x, viewport.xMin, maxX),
                Mathf.Clamp(position.y, viewport.yMin, maxY));
        }
    }
}
