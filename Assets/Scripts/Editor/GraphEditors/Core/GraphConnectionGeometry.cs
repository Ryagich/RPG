using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

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

        /// <summary>
        /// Resolves ports at the centres of each node's nearest facing edge.
        /// </summary>
        public static (Vector2 Start, Vector2 End) GetConnectionAnchors(Rect sourceRect, Rect targetRect)
        {
            return (
                GetNearestSideCenter(sourceRect, targetRect.center),
                GetNearestSideCenter(targetRect, sourceRect.center));
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

    /// <summary>
    /// Presentation-only rules shared by graph editors: selecting a connection owner and
    /// choosing the colour for its incoming and outgoing edges. Domain editors retain
    /// ownership of their nodes and target-selection state.
    /// </summary>
    internal static class GraphEditorConnectionPresentation
    {
        public static Color GetColor<TNode>(
            TNode sourceNode,
            TNode targetNode,
            TNode activeNode,
            Color defaultColor,
            Color sourceColor,
            Color targetColor)
            where TNode : class
        {
            if (activeNode == null)
            {
                return defaultColor;
            }

            if (ReferenceEquals(sourceNode, activeNode))
            {
                return sourceColor;
            }

            return ReferenceEquals(targetNode, activeNode) ? targetColor : defaultColor;
        }

        public static bool ShouldClearSelection<TNode>(
            bool isTargetSelectionActive,
            Event currentEvent,
            Vector2 graphMousePosition,
            IReadOnlyDictionary<TNode, Rect> nodeRects,
            TNode activeNode)
            where TNode : class
        {
            if (isTargetSelectionActive || activeNode == null || currentEvent.rawType != EventType.MouseDown || currentEvent.button != 0)
            {
                return false;
            }

            foreach (KeyValuePair<TNode, Rect> pair in nodeRects)
            {
                if (pair.Value.Contains(graphMousePosition))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Shared arrowhead rendering for both IMGUI and retained graph canvases.</summary>
    internal static class GraphConnectionDrawing
    {
        public static void DrawArrow(Vector2 tipPosition, Vector2 direction, float length = 18f, float halfWidth = 7.5f)
        {
            Vector2 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            Vector2 right = new(-normalizedDirection.y, normalizedDirection.x);
            Vector2 arrowBase = tipPosition - normalizedDirection * length;
            Handles.DrawAAConvexPolygon(
                tipPosition,
                arrowBase + right * halfWidth,
                arrowBase - right * halfWidth);
        }

        public static void DrawArrow(Painter2D painter, Vector2 tipPosition, Vector2 direction, float length = 18f, float halfWidth = 7.5f)
        {
            Vector2 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            Vector2 right = new(-normalizedDirection.y, normalizedDirection.x);
            Vector2 arrowBase = tipPosition - normalizedDirection * length;
            painter.BeginPath();
            painter.MoveTo(tipPosition);
            painter.LineTo(arrowBase + right * halfWidth);
            painter.LineTo(arrowBase - right * halfWidth);
            painter.ClosePath();
            painter.Fill();
        }
    }

    /// <summary>
    /// Obstacle-aware Bezier tangent selection shared by graph editors whose connections
    /// use curved routes. The caller owns cache invalidation and supplies current obstacles.
    /// </summary>
    internal static class GraphBezierConnectionRouter
    {
        public static (Vector2 StartTangent, Vector2 EndTangent) ResolveTangents(
            Vector2 startPos,
            Vector2 endPos,
            Rect sourceRect,
            Rect targetRect,
            IReadOnlyList<Rect> obstacles)
        {
            Vector2 startDirection = GraphConnectionGeometry.GetDirectionForRectPoint(sourceRect, startPos);
            Vector2 endDirection = GraphConnectionGeometry.GetDirectionForRectPoint(targetRect, endPos);
            Vector2 defaultStart = startPos + startDirection * 60f;
            Vector2 defaultEnd = endPos + endDirection * 60f;
            if (!IsBlocked(startPos, defaultStart, defaultEnd, endPos, obstacles))
            {
                return (defaultStart, defaultEnd);
            }

            List<Rect> blocking = obstacles.Where(rect =>
                IntersectsBezier(startPos, defaultStart, defaultEnd, endPos, rect) ||
                IntersectsSegment(startPos, endPos, rect)).ToList();
            if (blocking.Count == 0)
            {
                return (defaultStart, defaultEnd);
            }

            float distance = Vector2.Distance(startPos, endPos);
            float marginA = Mathf.Max(140f, distance * .25f);
            float marginB = Mathf.Max(220f, distance * .45f);
            var laneXs = new List<float> { blocking.Min(rect => rect.xMin) - 36f, blocking.Max(rect => rect.xMax) + 36f };
            var laneYs = new List<float> { blocking.Min(rect => rect.yMin) - 36f, blocking.Max(rect => rect.yMax) + 36f };
            float[] margins = { 28f, 52f, 80f, 112f, marginA, marginB };
            foreach (Rect rect in blocking)
            {
                foreach (float margin in margins)
                {
                    AddUnique(laneXs, rect.xMin - margin);
                    AddUnique(laneXs, rect.xMax + margin);
                    AddUnique(laneYs, rect.yMin - margin);
                    AddUnique(laneYs, rect.yMax + margin);
                }
            }

            float minX = laneXs[0];
            float maxX = laneXs[1];
            float minY = laneYs[0];
            float maxY = laneYs[1];
            AddUnique(laneXs, minX - marginA); AddUnique(laneXs, maxX + marginA);
            AddUnique(laneXs, minX - marginB); AddUnique(laneXs, maxX + marginB);
            AddUnique(laneYs, minY - marginA); AddUnique(laneYs, maxY + marginA);
            AddUnique(laneYs, minY - marginB); AddUnique(laneYs, maxY + marginB);

            float middleX = Mathf.Lerp(startPos.x, endPos.x, .5f);
            float middleY = Mathf.Lerp(startPos.y, endPos.y, .5f);
            float nearX = Mathf.Lerp(startPos.x, endPos.x, .12f);
            float farX = Mathf.Lerp(startPos.x, endPos.x, .88f);
            float nearY = Mathf.Lerp(startPos.y, endPos.y, .12f);
            float farY = Mathf.Lerp(startPos.y, endPos.y, .88f);
            var candidates = new List<(Vector2 Start, Vector2 End)>();
            foreach (float y in laneYs)
            {
                AddCandidate(candidates, new Vector2(Mathf.Lerp(startPos.x, endPos.x, .3f), y), new Vector2(Mathf.Lerp(startPos.x, endPos.x, .7f), y));
                AddCandidate(candidates, new Vector2(middleX, y), new Vector2(middleX, y));
                AddCandidate(candidates, new Vector2(Mathf.Lerp(startPos.x, endPos.x, .2f), y), new Vector2(Mathf.Lerp(startPos.x, endPos.x, .8f), y));
                AddCandidate(candidates, new Vector2(nearX, y), new Vector2(farX, y));
            }
            foreach (float x in laneXs)
            {
                AddCandidate(candidates, new Vector2(x, Mathf.Lerp(startPos.y, endPos.y, .3f)), new Vector2(x, Mathf.Lerp(startPos.y, endPos.y, .7f)));
                AddCandidate(candidates, new Vector2(x, middleY), new Vector2(x, middleY));
                AddCandidate(candidates, new Vector2(x, Mathf.Lerp(startPos.y, endPos.y, .2f)), new Vector2(x, Mathf.Lerp(startPos.y, endPos.y, .8f)));
                AddCandidate(candidates, new Vector2(x, nearY), new Vector2(x, farY));
            }

            (Vector2 Start, Vector2 End) best = (defaultStart, defaultEnd);
            int bestIntersections = CountIntersections(startPos, defaultStart, defaultEnd, endPos, obstacles);
            float bestScore = Score(startPos, defaultStart, defaultEnd, endPos);
            foreach ((Vector2 Start, Vector2 End) candidate in candidates)
            {
                int intersections = CountIntersections(startPos, candidate.Start, candidate.End, endPos, obstacles);
                float score = Score(startPos, candidate.Start, candidate.End, endPos);
                if (intersections < bestIntersections || intersections == bestIntersections && score < bestScore)
                {
                    best = candidate;
                    bestIntersections = intersections;
                    bestScore = score;
                }
            }
            return best;
        }

        private static void AddCandidate(ICollection<(Vector2 Start, Vector2 End)> candidates, Vector2 start, Vector2 end)
        {
            if (!candidates.Any(candidate => GraphConnectionGeometry.ApproximatelyEqual(candidate.Start, start) && GraphConnectionGeometry.ApproximatelyEqual(candidate.End, end))) candidates.Add((start, end));
        }
        private static void AddUnique(ICollection<float> values, float value)
        {
            if (!values.Any(existing => Mathf.Abs(existing - value) < .5f)) values.Add(value);
        }
        private static float Score(Vector2 start, Vector2 startTangent, Vector2 endTangent, Vector2 end) => Vector2.Distance(start, startTangent) + Vector2.Distance(startTangent, endTangent) + Vector2.Distance(endTangent, end);
        private static bool IsBlocked(Vector2 start, Vector2 startTangent, Vector2 endTangent, Vector2 end, IReadOnlyList<Rect> obstacles) => CountIntersections(start, startTangent, endTangent, end, obstacles) > 0;
        private static int CountIntersections(Vector2 start, Vector2 startTangent, Vector2 endTangent, Vector2 end, IReadOnlyList<Rect> obstacles) => obstacles.Count(rect => IntersectsBezier(start, startTangent, endTangent, end, rect));
        private static bool IntersectsBezier(Vector2 start, Vector2 startTangent, Vector2 endTangent, Vector2 end, Rect rect)
        {
            Vector2 previous = start;
            for (int index = 1; index < 40; index++)
            {
                float t = index / 40f;
                float inverse = 1f - t;
                Vector2 point = inverse * inverse * inverse * start + 3f * inverse * inverse * t * startTangent + 3f * inverse * t * t * endTangent + t * t * t * end;
                if (rect.Contains(point) || IntersectsSegment(previous, point, rect)) return true;
                previous = point;
            }
            return IntersectsSegment(previous, end, rect);
        }
        private static bool IntersectsSegment(Vector2 start, Vector2 end, Rect rect)
        {
            Rect inset = Rect.MinMaxRect(rect.xMin + .5f, rect.yMin + .5f, rect.xMax - .5f, rect.yMax - .5f);
            for (int index = 1; index < 24; index++) if (inset.Contains(Vector2.Lerp(start, end, index / 24f))) return true;
            return false;
        }
    }
}
