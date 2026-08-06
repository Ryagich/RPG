using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    internal static class GraphEditorCanvasUtility
    {
        private const float DefaultGridSpacing = 20f;

        public static Rect GetVisibleGraphRect(Rect editorRect, Vector2 panOffset, float zoom)
        {
            float safeZoom = Mathf.Max(zoom, 0.0001f);
            return new Rect(
                -panOffset.x / safeZoom,
                -panOffset.y / safeZoom,
                editorRect.width / safeZoom,
                editorRect.height / safeZoom);
        }

        public static bool IsAtLeastPartiallyVisible(Rect rect, Rect visibleGraphRect)
        {
            return rect.xMax >= visibleGraphRect.xMin &&
                   rect.xMin <= visibleGraphRect.xMax &&
                   rect.yMax >= visibleGraphRect.yMin &&
                   rect.yMin <= visibleGraphRect.yMax;
        }

        public static void DrawBackgroundGrid(
            Rect visibleGraphRect,
            Color canvasColor,
            Color minorGridColor,
            Color majorGridColor,
            float gridSpacing = DefaultGridSpacing)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            gridSpacing = Mathf.Max(1f, gridSpacing);
            EditorGUI.DrawRect(visibleGraphRect, canvasColor);

            Handles.BeginGUI();
            DrawGridLines(visibleGraphRect, gridSpacing, minorGridColor);
            DrawGridLines(visibleGraphRect, gridSpacing * 5f, majorGridColor);
            Handles.EndGUI();
        }

        private static void DrawGridLines(Rect visibleGraphRect, float spacing, Color color)
        {
            Handles.color = color;

            float firstX = Mathf.Floor(visibleGraphRect.xMin / spacing) * spacing;
            float firstY = Mathf.Floor(visibleGraphRect.yMin / spacing) * spacing;

            for (float x = firstX; x <= visibleGraphRect.xMax; x += spacing)
            {
                Handles.DrawLine(new Vector3(x, visibleGraphRect.yMin, 0f), new Vector3(x, visibleGraphRect.yMax, 0f));
            }

            for (float y = firstY; y <= visibleGraphRect.yMax; y += spacing)
            {
                Handles.DrawLine(new Vector3(visibleGraphRect.xMin, y, 0f), new Vector3(visibleGraphRect.xMax, y, 0f));
            }
        }
    }
}
