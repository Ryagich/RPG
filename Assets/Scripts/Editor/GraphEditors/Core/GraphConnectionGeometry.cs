using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Geometry primitives shared by graph-connection renderers.  The type deliberately
    /// contains no graph model or editor-window state, so every editor gets identical
    /// connection anchors and cache-comparison semantics.
    /// </summary>
    internal static class GraphConnectionGeometry
    {
        public static Vector2 GetNearestSideCenter(Rect rect, Vector2 point)
        {
            float leftDistance = Mathf.Abs(point.x - rect.xMin);
            float rightDistance = Mathf.Abs(point.x - rect.xMax);
            float topDistance = Mathf.Abs(point.y - rect.yMin);
            float bottomDistance = Mathf.Abs(point.y - rect.yMax);

            if (Mathf.Min(leftDistance, rightDistance) < Mathf.Min(topDistance, bottomDistance))
            {
                return leftDistance <= rightDistance
                    ? new Vector2(rect.xMin, rect.center.y)
                    : new Vector2(rect.xMax, rect.center.y);
            }

            return topDistance <= bottomDistance
                ? new Vector2(rect.center.x, rect.yMin)
                : new Vector2(rect.center.x, rect.yMax);
        }

        public static Vector2 GetDirectionForRectPoint(Rect rect, Vector2 point)
        {
            const float epsilon = 0.01f;

            if (Mathf.Abs(point.x - rect.xMin) < epsilon) return Vector2.left;
            if (Mathf.Abs(point.x - rect.xMax) < epsilon) return Vector2.right;
            if (Mathf.Abs(point.y - rect.yMin) < epsilon) return Vector2.up;
            if (Mathf.Abs(point.y - rect.yMax) < epsilon) return Vector2.down;

            Vector2 fallback = point - rect.center;
            return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector2.left;
        }

        public static Rect Expand(Rect rect, float margin) =>
            Rect.MinMaxRect(rect.xMin - margin, rect.yMin - margin, rect.xMax + margin, rect.yMax + margin);

        public static bool ApproximatelyEqual(Vector2 a, Vector2 b) =>
            Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);

        public static bool ApproximatelyEqual(Rect a, Rect b) =>
            Mathf.Approximately(a.x, b.x) &&
            Mathf.Approximately(a.y, b.y) &&
            Mathf.Approximately(a.width, b.width) &&
            Mathf.Approximately(a.height, b.height);
    }
}
