using System;
using System.Collections.Generic;
using System.Linq;
using EditorTools;
using UnityEditor;
using UnityEngine;

namespace Dialogs.Graph.Editor
{
    /// <summary>Builds obstacle-aware connection geometry for the dialog graph canvas.</summary>
    internal static class DialogConnectionRouter
    {
        private static Vector2[] BuildConnectionRoute(Vector2 startPos, Rect sourceRect, Rect targetRect, IReadOnlyList<Rect> expandedNodeRects)
        {
            const float clearance = 24f;

            ConnectionPort startPort = GetSourcePort(startPos, sourceRect, targetRect, clearance);
            ConnectionPort endPort = GetTargetPort(sourceRect, targetRect, clearance);

            return SimplifyRoute(new[]
            {
                startPos,
                startPort.OuterPoint,
                endPort.OuterPoint,
                endPort.EdgePoint
            });
        }

        private static Vector2[] BuildSoftDetourRoute(
            Vector2 startPos,
            ConnectionPort startPort,
            ConnectionPort endPort,
            IReadOnlyList<Rect> obstacles)
        {
            const float detourPadding = 18f;

            List<Rect> blockingRects = obstacles
                .Where(rect => DoesStraightSegmentIntersectRect(startPort.OuterPoint, endPort.OuterPoint, rect))
                .ToList();

            if (blockingRects.Count == 0)
            {
                return null;
            }

            float minX = blockingRects.Min(rect => rect.xMin) - detourPadding;
            float maxX = blockingRects.Max(rect => rect.xMax) + detourPadding;
            float minY = blockingRects.Min(rect => rect.yMin) - detourPadding;
            float maxY = blockingRects.Max(rect => rect.yMax) + detourPadding;
            float startX = startPort.OuterPoint.x;
            float endX = endPort.OuterPoint.x;
            float startY = startPort.OuterPoint.y;
            float endY = endPort.OuterPoint.y;

            var candidates = new List<Vector2[]>
            {
                BuildCandidateRoute(
                    startPos,
                    startPort,
                    endPort,
                    new Vector2(Mathf.Lerp(startX, endX, 0.3f), minY),
                    new Vector2(Mathf.Lerp(startX, endX, 0.7f), minY),
                    obstacles),
                BuildCandidateRoute(
                    startPos,
                    startPort,
                    endPort,
                    new Vector2(Mathf.Lerp(startX, endX, 0.3f), maxY),
                    new Vector2(Mathf.Lerp(startX, endX, 0.7f), maxY),
                    obstacles),
                BuildCandidateRoute(
                    startPos,
                    startPort,
                    endPort,
                    new Vector2(minX, Mathf.Lerp(startY, endY, 0.3f)),
                    new Vector2(minX, Mathf.Lerp(startY, endY, 0.7f)),
                    obstacles),
                BuildCandidateRoute(
                    startPos,
                    startPort,
                    endPort,
                    new Vector2(maxX, Mathf.Lerp(startY, endY, 0.3f)),
                    new Vector2(maxX, Mathf.Lerp(startY, endY, 0.7f)),
                    obstacles)
            };

            return candidates
                .Where(route => route != null)
                .OrderBy(ScoreConnectionRoute)
                .FirstOrDefault();
        }

        private static Vector2[] BuildCandidateRoute(
            Vector2 startPos,
            ConnectionPort startPort,
            ConnectionPort endPort,
            Vector2 viaA,
            Vector2 viaB,
            IReadOnlyList<Rect> obstacles)
        {
            Vector2[] route = SimplifyRoute(new[]
            {
                startPos,
                startPort.OuterPoint,
                viaA,
                viaB,
                endPort.OuterPoint,
                endPort.EdgePoint
            });

            for (int i = 1; i < route.Length - 2; i++)
            {
                if (IsStraightSegmentBlocked(route[i], route[i + 1], obstacles))
                {
                    return null;
                }
            }

            return route;
        }

        private static float ScoreConnectionRoute(IReadOnlyList<Vector2> route)
        {
            if (route == null || route.Count < 2)
            {
                return float.PositiveInfinity;
            }

            float length = 0f;
            for (int i = 0; i < route.Count - 1; i++)
            {
                length += Vector2.Distance(route[i], route[i + 1]);
            }

            float turnPenalty = Mathf.Max(0, route.Count - 4) * 18f;
            return length + turnPenalty;
        }

        internal static void DrawConnectionArrow(Vector2 tipPosition, Vector2 direction)
        {
            Vector2 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            Vector2 right = new Vector2(-normalizedDirection.y, normalizedDirection.x);
            Vector2 arrowBase = tipPosition - normalizedDirection * 18f;
            Vector3[] arrow =
            {
                tipPosition,
                arrowBase + right * 7.5f,
                arrowBase - right * 7.5f
            };
            Handles.DrawAAConvexPolygon(arrow);
        }

        internal static (Vector2 StartTangent, Vector2 EndTangent) ResolveConnectionTangents(
            Vector2 startPos,
            Vector2 endPos,
            Rect sourceRect,
            Rect targetRect,
            IReadOnlyList<Rect> obstacles)
        {
            Vector2 startDirection = GetConnectionDirectionForRectPoint(sourceRect, startPos);
            Vector2 endDirection = GetConnectionDirectionForRectPoint(targetRect, endPos);
            Vector2 defaultStartTangent = startPos + startDirection * 60f;
            Vector2 defaultEndTangent = endPos + endDirection * 60f;

            if (!IsBezierBlocked(startPos, defaultStartTangent, defaultEndTangent, endPos, obstacles))
            {
                return (defaultStartTangent, defaultEndTangent);
            }

            List<Rect> blockingRects = obstacles
                .Where(rect =>
                    DoesBezierIntersectRect(startPos, defaultStartTangent, defaultEndTangent, endPos, rect) ||
                    DoesStraightSegmentIntersectRect(startPos, endPos, rect))
                .ToList();

            if (blockingRects.Count == 0)
            {
                return (defaultStartTangent, defaultEndTangent);
            }

            float routeDistance = Vector2.Distance(startPos, endPos);
            float extendedMarginA = Mathf.Max(140f, routeDistance * 0.25f);
            float extendedMarginB = Mathf.Max(220f, routeDistance * 0.45f);
            float minX = blockingRects.Min(rect => rect.xMin) - 36f;
            float maxX = blockingRects.Max(rect => rect.xMax) + 36f;
            float minY = blockingRects.Min(rect => rect.yMin) - 36f;
            float maxY = blockingRects.Max(rect => rect.yMax) + 36f;
            float middleX = Mathf.Lerp(startPos.x, endPos.x, 0.5f);
            float middleY = Mathf.Lerp(startPos.y, endPos.y, 0.5f);
            var laneXs = new List<float> { minX, maxX };
            var laneYs = new List<float> { minY, maxY };
            float[] laneMargins = { 28f, 52f, 80f, 112f, extendedMarginA, extendedMarginB };

            foreach (Rect rect in blockingRects)
            {
                foreach (float margin in laneMargins)
                {
                    AddApproximatelyUnique(laneXs, rect.xMin - margin);
                    AddApproximatelyUnique(laneXs, rect.xMax + margin);
                    AddApproximatelyUnique(laneYs, rect.yMin - margin);
                    AddApproximatelyUnique(laneYs, rect.yMax + margin);
                }
            }

            AddApproximatelyUnique(laneXs, minX - extendedMarginA);
            AddApproximatelyUnique(laneXs, maxX + extendedMarginA);
            AddApproximatelyUnique(laneXs, minX - extendedMarginB);
            AddApproximatelyUnique(laneXs, maxX + extendedMarginB);
            AddApproximatelyUnique(laneYs, minY - extendedMarginA);
            AddApproximatelyUnique(laneYs, maxY + extendedMarginA);
            AddApproximatelyUnique(laneYs, minY - extendedMarginB);
            AddApproximatelyUnique(laneYs, maxY + extendedMarginB);

            var candidates = new List<(Vector2 StartTangent, Vector2 EndTangent)>();
            float nearStartX = Mathf.Lerp(startPos.x, endPos.x, 0.12f);
            float farEndX = Mathf.Lerp(startPos.x, endPos.x, 0.88f);
            float nearStartY = Mathf.Lerp(startPos.y, endPos.y, 0.12f);
            float farEndY = Mathf.Lerp(startPos.y, endPos.y, 0.88f);

            foreach (float laneY in laneYs)
            {
                AddBezierCandidate(candidates, new Vector2(Mathf.Lerp(startPos.x, endPos.x, 0.30f), laneY), new Vector2(Mathf.Lerp(startPos.x, endPos.x, 0.70f), laneY));
                AddBezierCandidate(candidates, new Vector2(middleX, laneY), new Vector2(middleX, laneY));
                AddBezierCandidate(candidates, new Vector2(Mathf.Lerp(startPos.x, endPos.x, 0.20f), laneY), new Vector2(Mathf.Lerp(startPos.x, endPos.x, 0.80f), laneY));
                AddBezierCandidate(candidates, new Vector2(nearStartX, laneY), new Vector2(farEndX, laneY));
            }

            foreach (float laneX in laneXs)
            {
                AddBezierCandidate(candidates, new Vector2(laneX, Mathf.Lerp(startPos.y, endPos.y, 0.30f)), new Vector2(laneX, Mathf.Lerp(startPos.y, endPos.y, 0.70f)));
                AddBezierCandidate(candidates, new Vector2(laneX, middleY), new Vector2(laneX, middleY));
                AddBezierCandidate(candidates, new Vector2(laneX, Mathf.Lerp(startPos.y, endPos.y, 0.20f)), new Vector2(laneX, Mathf.Lerp(startPos.y, endPos.y, 0.80f)));
                AddBezierCandidate(candidates, new Vector2(laneX, nearStartY), new Vector2(laneX, farEndY));
            }

            (Vector2 StartTangent, Vector2 EndTangent) bestCandidate = (defaultStartTangent, defaultEndTangent);
            int bestIntersectionCount = CountBezierIntersections(startPos, defaultStartTangent, defaultEndTangent, endPos, obstacles);
            float bestScore = ScoreBezierCandidate(startPos, defaultStartTangent, defaultEndTangent, endPos);

            foreach ((Vector2 StartTangent, Vector2 EndTangent) candidate in candidates)
            {
                int intersectionCount = CountBezierIntersections(startPos, candidate.StartTangent, candidate.EndTangent, endPos, obstacles);
                float candidateScore = ScoreBezierCandidate(startPos, candidate.StartTangent, candidate.EndTangent, endPos);
                if (intersectionCount < bestIntersectionCount ||
                    intersectionCount == bestIntersectionCount && candidateScore < bestScore)
                {
                    bestIntersectionCount = intersectionCount;
                    bestScore = candidateScore;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        private static float ScoreBezierCandidate(Vector2 startPos, Vector2 startTangent, Vector2 endTangent, Vector2 endPos)
        {
            return Vector2.Distance(startPos, startTangent) +
                   Vector2.Distance(startTangent, endTangent) +
                   Vector2.Distance(endTangent, endPos);
        }

        private static void AddBezierCandidate(
            ICollection<(Vector2 StartTangent, Vector2 EndTangent)> candidates,
            Vector2 startTangent,
            Vector2 endTangent)
        {
            foreach ((Vector2 StartTangent, Vector2 EndTangent) existing in candidates)
            {
                if (ApproximatelyEqual(existing.StartTangent, startTangent) &&
                    ApproximatelyEqual(existing.EndTangent, endTangent))
                {
                    return;
                }
            }

            candidates.Add((startTangent, endTangent));
        }

        private static void AddApproximatelyUnique(ICollection<float> values, float value)
        {
            foreach (float existing in values)
            {
                if (Mathf.Abs(existing - value) < 0.5f)
                {
                    return;
                }
            }

            values.Add(value);
        }

        private static bool IsBezierBlocked(
            Vector2 startPos,
            Vector2 startTangent,
            Vector2 endTangent,
            Vector2 endPos,
            IReadOnlyList<Rect> obstacles)
        {
            return CountBezierIntersections(startPos, startTangent, endTangent, endPos, obstacles) > 0;
        }

        private static int CountBezierIntersections(
            Vector2 startPos,
            Vector2 startTangent,
            Vector2 endTangent,
            Vector2 endPos,
            IReadOnlyList<Rect> obstacles)
        {
            int intersections = 0;
            foreach (Rect obstacle in obstacles)
            {
                if (DoesBezierIntersectRect(startPos, startTangent, endTangent, endPos, obstacle))
                {
                    intersections++;
                }
            }

            return intersections;
        }

        private static bool DoesBezierIntersectRect(
            Vector2 startPos,
            Vector2 startTangent,
            Vector2 endTangent,
            Vector2 endPos,
            Rect rect)
        {
            const int samples = 40;
            const float inset = 0.5f;

            Rect innerRect = Rect.MinMaxRect(
                rect.xMin + inset,
                rect.yMin + inset,
                rect.xMax - inset,
                rect.yMax - inset);

            Vector2 previousPoint = startPos;
            for (int i = 1; i < samples; i++)
            {
                float t = i / (float)samples;
                Vector2 point = EvaluateBezierPoint(startPos, startTangent, endTangent, endPos, t);
                if (innerRect.Contains(point) || DoesStraightSegmentIntersectRect(previousPoint, point, innerRect))
                {
                    return true;
                }

                previousPoint = point;
            }

            return DoesStraightSegmentIntersectRect(previousPoint, endPos, innerRect);
        }

        private static Vector2 EvaluateBezierPoint(
            Vector2 startPos,
            Vector2 startTangent,
            Vector2 endTangent,
            Vector2 endPos,
            float t)
        {
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * oneMinusT * startPos +
                   3f * oneMinusT * oneMinusT * t * startTangent +
                   3f * oneMinusT * t * t * endTangent +
                   t * t * t * endPos;
        }

        internal static Vector2 GetNearestSideCenter(Rect rect, Vector2 point)
        {
            return GraphConnectionGeometry.GetNearestSideCenter(rect, point);
        }

        /// <summary>
        /// Chooses one port on each node independently: the centre of the edge nearest
        /// to the other node.  Thus a connection always leaves and enters through the
        /// closest facing edges, never through an arbitrary control inside a node.
        /// </summary>
        internal static (Vector2 Start, Vector2 End) GetConnectionAnchors(Rect sourceRect, Rect targetRect)
        {
            return (
                GetNearestSideCenter(sourceRect, targetRect.center),
                GetNearestSideCenter(targetRect, sourceRect.center));
        }

        internal static Vector2 GetConnectionDirectionForRectPoint(Rect rect, Vector2 point)
        {
            return GraphConnectionGeometry.GetDirectionForRectPoint(rect, point);
        }

        private static void DrawSmoothedConnection(IReadOnlyList<Vector2> routePoints)
        {
            if (routePoints == null || routePoints.Count < 2)
            {
                return;
            }

            const float lineWidth = 3.5f;
            const float cornerRadius = 22f;

            if (routePoints.Count == 2)
            {
                Handles.DrawAAPolyLine(lineWidth, routePoints.Select(point => (Vector3)point).ToArray());
                return;
            }

            Vector2 currentStart = routePoints[0];

            for (int i = 1; i < routePoints.Count - 1; i++)
            {
                Vector2 corner = routePoints[i];
                Vector2 previous = routePoints[i - 1];
                Vector2 next = routePoints[i + 1];

                Vector2 incomingDirection = (corner - previous).normalized;
                Vector2 outgoingDirection = (next - corner).normalized;

                float incomingLength = Vector2.Distance(previous, corner);
                float outgoingLength = Vector2.Distance(corner, next);
                float radius = Mathf.Min(cornerRadius, incomingLength * 0.5f, outgoingLength * 0.5f);

                if (radius <= 0.01f || ApproximatelyEqual(incomingDirection, outgoingDirection))
                {
                    Handles.DrawAAPolyLine(lineWidth, new Vector3[] { currentStart, corner });
                    currentStart = corner;
                    continue;
                }

                Vector2 curveStart = corner - incomingDirection * radius;
                Vector2 curveEnd = corner + outgoingDirection * radius;

                Handles.DrawAAPolyLine(lineWidth, new Vector3[] { currentStart, curveStart });

                Handles.DrawBezier(
                    curveStart,
                    curveEnd,
                    curveStart + incomingDirection * radius,
                    curveEnd - outgoingDirection * radius,
                    Handles.color,
                    null,
                    lineWidth);

                currentStart = curveEnd;
            }

            Handles.DrawAAPolyLine(lineWidth, new Vector3[] { currentStart, routePoints[routePoints.Count - 1] });
        }

        private static List<Vector2> FindOrthogonalPath(Vector2 startPoint, Vector2 endPoint, IReadOnlyList<Rect> obstacles)
        {
            var xCoords = new List<float>();
            var yCoords = new List<float>();

            AddUniqueCoordinate(xCoords, startPoint.x);
            AddUniqueCoordinate(xCoords, endPoint.x);
            AddUniqueCoordinate(yCoords, startPoint.y);
            AddUniqueCoordinate(yCoords, endPoint.y);

            foreach (Rect obstacle in obstacles)
            {
                AddUniqueCoordinate(xCoords, obstacle.xMin);
                AddUniqueCoordinate(xCoords, obstacle.xMax);
                AddUniqueCoordinate(yCoords, obstacle.yMin);
                AddUniqueCoordinate(yCoords, obstacle.yMax);
            }

            var points = new List<Vector2>();
            var pointIndex = new Dictionary<string, int>();

            foreach (float x in xCoords)
            {
                foreach (float y in yCoords)
                {
                    Vector2 point = new Vector2(x, y);
                    if (IsPointInsideAnyRect(point, obstacles))
                    {
                        continue;
                    }

                    pointIndex[GetPointKey(point)] = points.Count;
                    points.Add(point);
                }
            }

            if (!TryGetPointIndex(pointIndex, startPoint, out int startIndex))
            {
                return null;
            }

            int endIndex = TryGetPointIndex(pointIndex, endPoint, out int resolvedEndIndex)
                ? resolvedEndIndex
                : -1;

            var adjacency = new List<int>[points.Count];
            for (int i = 0; i < adjacency.Length; i++)
            {
                adjacency[i] = new List<int>();
            }

            foreach (float y in yCoords)
            {
                List<int> row = points
                    .Select((point, index) => new { point, index })
                    .Where(item => Mathf.Approximately(item.point.y, y))
                    .OrderBy(item => item.point.x)
                    .Select(item => item.index)
                    .ToList();

                ConnectAdjacentPoints(row, points, adjacency, obstacles);
            }

            foreach (float x in xCoords)
            {
                List<int> column = points
                    .Select((point, index) => new { point, index })
                    .Where(item => Mathf.Approximately(item.point.x, x))
                    .OrderBy(item => item.point.y)
                    .Select(item => item.index)
                    .ToList();

                ConnectAdjacentPoints(column, points, adjacency, obstacles);
            }

            return FindShortestPath(points, adjacency, startIndex, endIndex, endPoint);
        }

        private static List<Vector2> FindShortestPath(
            IReadOnlyList<Vector2> points,
            IReadOnlyList<int>[] adjacency,
            int startIndex,
            int endIndex,
            Vector2 targetPoint)
        {
            float[] distances = Enumerable.Repeat(float.PositiveInfinity, points.Count).ToArray();
            int[] previous = Enumerable.Repeat(-1, points.Count).ToArray();
            bool[] visited = new bool[points.Count];

            distances[startIndex] = 0f;
            int bestReachableIndex = startIndex;
            float bestReachableDistanceToEnd = Vector2.Distance(points[startIndex], targetPoint);

            while (true)
            {
                int current = -1;
                float bestDistance = float.PositiveInfinity;
                float bestHeuristic = float.PositiveInfinity;

                for (int i = 0; i < points.Count; i++)
                {
                    if (visited[i] || float.IsPositiveInfinity(distances[i]))
                    {
                        continue;
                    }

                    float heuristic = distances[i] + Vector2.Distance(points[i], targetPoint);
                    if (heuristic < bestHeuristic || Mathf.Approximately(heuristic, bestHeuristic) && distances[i] < bestDistance)
                    {
                        current = i;
                        bestDistance = distances[i];
                        bestHeuristic = heuristic;
                    }
                }

                if (current == -1)
                {
                    return ReconstructPath(points, previous, bestReachableIndex);
                }

                if (endIndex >= 0 && current == endIndex)
                {
                    return ReconstructPath(points, previous, endIndex);
                }

                visited[current] = true;
                float currentDistanceToEnd = Vector2.Distance(points[current], targetPoint);
                if (currentDistanceToEnd + 0.01f < bestReachableDistanceToEnd ||
                    Mathf.Approximately(currentDistanceToEnd, bestReachableDistanceToEnd) && distances[current] < distances[bestReachableIndex])
                {
                    bestReachableDistanceToEnd = currentDistanceToEnd;
                    bestReachableIndex = current;
                }

                foreach (int neighbor in adjacency[current])
                {
                    if (visited[neighbor])
                    {
                        continue;
                    }

                    float candidateDistance = distances[current] + Vector2.Distance(points[current], points[neighbor]);
                    if (candidateDistance + 0.01f < distances[neighbor])
                    {
                        distances[neighbor] = candidateDistance;
                        previous[neighbor] = current;
                    }
                }
            }

        }

        private static List<Vector2> ReconstructPath(IReadOnlyList<Vector2> points, IReadOnlyList<int> previous, int endIndex)
        {
            var path = new List<Vector2>();
            for (int node = endIndex; node != -1; node = previous[node])
            {
                path.Add(points[node]);
            }

            path.Reverse();
            return path;
        }

        private static void ConnectAdjacentPoints(
            IReadOnlyList<int> indices,
            IReadOnlyList<Vector2> points,
            IList<int>[] adjacency,
            IReadOnlyList<Rect> obstacles)
        {
            for (int i = 0; i < indices.Count - 1; i++)
            {
                int a = indices[i];
                int b = indices[i + 1];
                if (!IsOrthogonalSegmentBlocked(points[a], points[b], obstacles))
                {
                    adjacency[a].Add(b);
                    adjacency[b].Add(a);
                }
            }
        }

        private static ConnectionPort GetSourcePort(Vector2 startPos, Rect sourceRect, Rect targetRect, float clearance)
        {
            bool preferHorizontal = Mathf.Abs(targetRect.center.x - sourceRect.center.x) >= Mathf.Abs(targetRect.center.y - sourceRect.center.y);
            if (preferHorizontal)
            {
                if (targetRect.center.x >= sourceRect.center.x)
                {
                    return new ConnectionPort(
                        new Vector2(sourceRect.xMax, startPos.y),
                        new Vector2(sourceRect.xMax + clearance, startPos.y));
                }

                return new ConnectionPort(
                    new Vector2(sourceRect.xMin, startPos.y),
                    new Vector2(sourceRect.xMin - clearance, startPos.y));
            }

            if (targetRect.center.y >= sourceRect.center.y)
            {
                return new ConnectionPort(
                    new Vector2(startPos.x, sourceRect.yMax),
                    new Vector2(startPos.x, sourceRect.yMax + clearance));
            }

            return new ConnectionPort(
                new Vector2(startPos.x, sourceRect.yMin),
                new Vector2(startPos.x, sourceRect.yMin - clearance));
        }

        private static ConnectionPort GetTargetPort(Rect sourceRect, Rect targetRect, float clearance)
        {
            Vector2 sourceCenter = sourceRect.center;
            ConnectionPort[] candidatePorts =
            {
                new(
                    new Vector2(targetRect.xMin, targetRect.center.y),
                    new Vector2(targetRect.xMin - clearance, targetRect.center.y)),
                new(
                    new Vector2(targetRect.xMax, targetRect.center.y),
                    new Vector2(targetRect.xMax + clearance, targetRect.center.y)),
                new(
                    new Vector2(targetRect.center.x, targetRect.yMin),
                    new Vector2(targetRect.center.x, targetRect.yMin - clearance)),
                new(
                    new Vector2(targetRect.center.x, targetRect.yMax),
                    new Vector2(targetRect.center.x, targetRect.yMax + clearance))
            };

            ConnectionPort bestPort = candidatePorts[0];
            float bestDistance = Vector2.SqrMagnitude(sourceCenter - bestPort.EdgePoint);

            for (int i = 1; i < candidatePorts.Length; i++)
            {
                float distance = Vector2.SqrMagnitude(sourceCenter - candidatePorts[i].EdgePoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPort = candidatePorts[i];
                }
            }

            return bestPort;
        }

        internal static Rect ExpandRect(Rect rect, float margin)
        {
            return GraphConnectionGeometry.Expand(rect, margin);
        }

        private static bool IsPointInsideAnyRect(Vector2 point, IReadOnlyList<Rect> rects)
        {
            const float epsilon = 0.01f;
            foreach (Rect rect in rects)
            {
                if (point.x > rect.xMin + epsilon && point.x < rect.xMax - epsilon &&
                    point.y > rect.yMin + epsilon && point.y < rect.yMax - epsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOrthogonalSegmentBlocked(Vector2 start, Vector2 end, IReadOnlyList<Rect> rects)
        {
            const float epsilon = 0.01f;
            if (!Mathf.Approximately(start.x, end.x) && !Mathf.Approximately(start.y, end.y))
            {
                return true;
            }

            foreach (Rect rect in rects)
            {
                if (Mathf.Approximately(start.y, end.y))
                {
                    float y = start.y;
                    float minX = Mathf.Min(start.x, end.x);
                    float maxX = Mathf.Max(start.x, end.x);
                    bool overlapsY = y > rect.yMin + epsilon && y < rect.yMax - epsilon;
                    bool overlapsX = maxX > rect.xMin + epsilon && minX < rect.xMax - epsilon;
                    if (overlapsY && overlapsX)
                    {
                        return true;
                    }
                }
                else
                {
                    float x = start.x;
                    float minY = Mathf.Min(start.y, end.y);
                    float maxY = Mathf.Max(start.y, end.y);
                    bool overlapsX = x > rect.xMin + epsilon && x < rect.xMax - epsilon;
                    bool overlapsY = maxY > rect.yMin + epsilon && minY < rect.yMax - epsilon;
                    if (overlapsX && overlapsY)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsStraightSegmentBlocked(Vector2 start, Vector2 end, IReadOnlyList<Rect> rects)
        {
            foreach (Rect rect in rects)
            {
                if (DoesStraightSegmentIntersectRect(start, end, rect))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DoesStraightSegmentIntersectRect(Vector2 start, Vector2 end, Rect rect)
        {
            const int samples = 24;
            const float inset = 0.5f;

            Rect innerRect = Rect.MinMaxRect(
                rect.xMin + inset,
                rect.yMin + inset,
                rect.xMax - inset,
                rect.yMax - inset);

            for (int i = 1; i < samples; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / (float)samples);
                if (innerRect.Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddUniqueCoordinate(List<float> coordinates, float value)
        {
            if (coordinates.All(existing => !Mathf.Approximately(existing, value)))
            {
                coordinates.Add(value);
            }
        }

        private static bool TryGetPointIndex(IReadOnlyDictionary<string, int> pointIndex, Vector2 point, out int index)
        {
            return pointIndex.TryGetValue(GetPointKey(point), out index);
        }

        private static string GetPointKey(Vector2 point)
        {
            return $"{point.x:F3}|{point.y:F3}";
        }

        private static Vector2[] SimplifyRoute(IEnumerable<Vector2> points)
        {
            var simplified = new List<Vector2>();

            foreach (Vector2 point in points)
            {
                if (simplified.Count == 0 || !ApproximatelyEqual(simplified[simplified.Count - 1], point))
                {
                    simplified.Add(point);
                }
            }

            int index = 1;
            while (index < simplified.Count - 1)
            {
                Vector2 previous = simplified[index - 1];
                Vector2 current = simplified[index];
                Vector2 next = simplified[index + 1];

                bool sameX = Mathf.Approximately(previous.x, current.x) && Mathf.Approximately(current.x, next.x);
                bool sameY = Mathf.Approximately(previous.y, current.y) && Mathf.Approximately(current.y, next.y);
                if (sameX || sameY)
                {
                    simplified.RemoveAt(index);
                    continue;
                }

                index++;
            }

            return simplified.ToArray();
        }
        private static bool ApproximatelyEqual(Vector2 a, Vector2 b)
        {
            return GraphConnectionGeometry.ApproximatelyEqual(a, b);
        }
    }
}
