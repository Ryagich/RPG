using EditorTools;
using UnityEngine;

namespace Dialogs.Graph.Editor
{
    /// <summary>
    /// Dialog-facing geometry facade. Shared route solving lives in
    /// <see cref="GraphBezierConnectionRouter"/> so quest and dialog canvases use
    /// identical obstacle-aware tangent rules.
    /// </summary>
    internal static class DialogConnectionRouter
    {
        public static Vector2 GetNearestSideCenter(Rect rect, Vector2 point) =>
            GraphConnectionGeometry.GetNearestSideCenter(rect, point);

        public static (Vector2 Start, Vector2 End) GetConnectionAnchors(Rect sourceRect, Rect targetRect) =>
            GraphConnectionGeometry.GetConnectionAnchors(sourceRect, targetRect);

        public static Vector2 GetConnectionDirectionForRectPoint(Rect rect, Vector2 point) =>
            GraphConnectionGeometry.GetDirectionForRectPoint(rect, point);

        public static Rect ExpandRect(Rect rect, float margin) => GraphConnectionGeometry.Expand(rect, margin);
    }
}
