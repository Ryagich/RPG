using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Shared editor-window viewport behavior, originally established by the dialog editor.
    /// It owns only transient presentation state; graph data and domain mutations remain in
    /// the derived editor.
    /// </summary>
    public abstract partial class GraphEditorWindowBase : EditorWindow
    {
        private readonly GraphEditorWorkspaceState workspaceState = new();

        protected float zoom
        {
            get => workspaceState.Zoom;
            set => workspaceState.Zoom = value;
        }

        protected Vector2 panOffset
        {
            get => workspaceState.PanOffset;
            set => workspaceState.PanOffset = value;
        }

        protected bool useLightTheme
        {
            get => workspaceState.UseLightTheme;
            set => workspaceState.UseLightTheme = value;
        }

        protected Vector2 GetCenteredNodePosition(Vector2 nodeSize)
        {
            Vector2 screenCenter = new(position.width * 0.5f, position.height * 0.5f);
            Vector2 graphCenter = (screenCenter - panOffset) / zoom;
            graphCenter.y += 120f;
            return graphCenter - nodeSize * 0.5f;
        }

        protected void ClampPanToWorkspace(float workspaceWidth, float workspaceHeight)
        {
            Vector2 clampedPanOffset = panOffset;
            float viewWidth = position.width;
            float viewHeight = position.height;
            float minX = viewWidth - workspaceWidth * zoom;
            float minY = viewHeight - workspaceHeight * zoom;

            clampedPanOffset.x = workspaceWidth * zoom <= viewWidth
                ? Mathf.Round((viewWidth - workspaceWidth * zoom) * 0.5f)
                : Mathf.Clamp(clampedPanOffset.x, minX, 0f);
            clampedPanOffset.y = workspaceHeight * zoom <= viewHeight
                ? Mathf.Round((viewHeight - workspaceHeight * zoom) * 0.5f)
                : Mathf.Clamp(clampedPanOffset.y, minY, 0f);
            panOffset = clampedPanOffset;
        }

        protected void HandleZoom(Event currentEvent, float zoomMin, float zoomMax, float workspaceWidth, float workspaceHeight)
        {
            if (currentEvent.type != EventType.ScrollWheel)
            {
                return;
            }

            float oldZoom = zoom;
            zoom = Mathf.Clamp(zoom - currentEvent.delta.y * 0.05f, zoomMin, zoomMax);
            Vector2 windowCenter = new(position.width * 0.5f, position.height * 0.5f);
            panOffset = (panOffset - windowCenter) * (zoom / oldZoom) + windowCenter;
            ClampPanToWorkspace(workspaceWidth, workspaceHeight);
            currentEvent.Use();
        }

        protected void HandlePan(Event currentEvent, float workspaceWidth, float workspaceHeight)
        {
            if (currentEvent.type != EventType.MouseDrag || currentEvent.button != 1)
            {
                return;
            }

            panOffset += currentEvent.delta;
            ClampPanToWorkspace(workspaceWidth, workspaceHeight);
            currentEvent.Use();
            Repaint();
        }
    }
}
