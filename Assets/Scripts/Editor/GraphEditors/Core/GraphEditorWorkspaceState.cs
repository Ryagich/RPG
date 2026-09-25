using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Editor-only presentation state shared by graph workspaces. It intentionally contains
    /// no graph, asset, or domain knowledge.
    /// </summary>
    internal sealed class GraphEditorWorkspaceState
    {
        public float Zoom { get; set; } = 1f;
        public Vector2 PanOffset { get; set; } = Vector2.zero;
        public bool UseLightTheme { get; set; }
    }

    /// <summary>
    /// Domain-free camera interaction state for retained graph workspaces. The canvas
    /// remains responsible for pointer capture and applying the resulting transform.
    /// </summary>
    internal sealed class GraphEditorViewportController
    {
        private int panPointerId = -1;
        private Vector2 panStartPointer;
        private Vector2 panStartOffset;

        public bool IsPanning => panPointerId >= 0;

        public void BeginPan(int pointerId, Vector2 pointerPosition, Vector2 currentOffset)
        {
            panPointerId = pointerId;
            panStartPointer = pointerPosition;
            panStartOffset = currentOffset;
        }

        public bool TryGetPannedOffset(int pointerId, Vector2 pointerPosition, out Vector2 offset)
        {
            offset = default;
            if (pointerId != panPointerId)
            {
                return false;
            }

            offset = panStartOffset + pointerPosition - panStartPointer;
            return true;
        }

        public bool EndPan(int pointerId)
        {
            if (pointerId != panPointerId)
            {
                return false;
            }

            panPointerId = -1;
            return true;
        }

        public static bool TryZoomToCursor(
            float currentZoom,
            Vector2 currentOffset,
            Vector2 cursorPosition,
            float scrollDelta,
            float minZoom,
            float maxZoom,
            out float zoom,
            out Vector2 offset)
        {
            zoom = Mathf.Clamp(currentZoom - scrollDelta * .05f, minZoom, maxZoom);
            offset = currentOffset;
            if (Mathf.Approximately(currentZoom, zoom))
            {
                return false;
            }

            Vector2 graphPoint = (cursorPosition - currentOffset) / currentZoom;
            offset = cursorPosition - graphPoint * zoom;
            return true;
        }
    }
}
