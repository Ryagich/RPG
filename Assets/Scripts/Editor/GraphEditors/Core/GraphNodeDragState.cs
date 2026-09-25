using System;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Keeps transient node-drag state separate from the canvas visual tree and domain presenter.
    /// </summary>
    internal sealed class GraphNodeDragState<TNode> where TNode : class
    {
        private TNode draggedNode;
        private Vector2 previewPosition;
        private bool hasPreviewPosition;

        public TNode DraggedNode => draggedNode;

        public bool IsDragging(TNode node) => ReferenceEquals(draggedNode, node);

        public void Begin(TNode node, Vector2 initialPosition)
        {
            draggedNode = node;
            previewPosition = initialPosition;
            hasPreviewPosition = true;
        }

        public bool TrySetPreview(TNode node, Vector2 position, Vector2 nodeSize, float workspaceWidth,
            float workspaceHeight, float minimumVisibleHeight, out Vector2 clampedPosition)
        {
            if (!IsDragging(node))
            {
                clampedPosition = default;
                return false;
            }

            clampedPosition = new Vector2(
                Mathf.Clamp(position.x, 0f, workspaceWidth - nodeSize.x),
                Mathf.Clamp(position.y, 0f, workspaceHeight - minimumVisibleHeight));
            previewPosition = clampedPosition;
            hasPreviewPosition = true;
            return true;
        }

        public Vector2 GetCommittedPosition(Vector2 fallbackPosition) => hasPreviewPosition ? previewPosition : fallbackPosition;

        public Vector2 GetEffectivePosition(TNode node, Vector2 persistedPosition) =>
            IsDragging(node) && hasPreviewPosition ? previewPosition : persistedPosition;

        public void Clear()
        {
            draggedNode = null;
            hasPreviewPosition = false;
        }
    }
}
