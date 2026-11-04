using System.Collections.Generic;
using Dialogs.Graph.Model;
using EditorTools;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dialogs.Graph.Editor
{
    /// <summary>Owns transient route and selector-anchor state for dialogue connections.</summary>
    internal sealed class DialogGraphConnectionPresenter
    {
        private readonly Dictionary<DialogAnswer, CachedConnectionRoute> answerRoutes = new();
        private readonly Dictionary<ImplicitConnectionRouteKey, CachedConnectionRoute> implicitRoutes = new();
        private readonly GraphEditorConnectionAnchorRegistry<DialogAnswer> pickerAnchors = new();
        private readonly List<Rect> obstacleRects = new();
        private int layoutVersion;

        public void ClearNodePresentationState()
        {
            pickerAnchors.Clear();
            InvalidateRoutes();
        }

        public bool RecordPicker(DialogAnswer answer, Rect pickerRect) => pickerAnchors.Record(answer, pickerRect);

        public void InvalidateRoutes()
        {
            answerRoutes.Clear();
            implicitRoutes.Clear();
            layoutVersion++;
        }

        public void DrawRetainedConnection(
            Painter2D painter,
            DialogAnswer answer,
            DialogNode sourceNode,
            DialogNode targetNode,
            Rect sourceRect,
            Rect targetRect,
            bool isDragging,
            IReadOnlyDictionary<DialogNode, Rect> nodeRects,
            Color color)
        {
            (Vector2 defaultStart, Vector2 endPosition) = GraphConnectionGeometry.GetConnectionAnchors(sourceRect, targetRect);
            Vector2 startPosition = pickerAnchors.Resolve(answer, sourceRect, defaultStart);
            Vector2 startTangent = startPosition + GraphConnectionGeometry.GetDirectionForRectPoint(sourceRect, startPosition) * 60f;
            Vector2 endTangent = endPosition + GraphConnectionGeometry.GetDirectionForRectPoint(targetRect, endPosition) * 60f;
            if (!isDragging)
            {
                (startTangent, endTangent) = GetOrBuildTangents(
                    answer, startPosition, endPosition, sourceRect, targetRect, sourceNode, targetNode, nodeRects);
            }

            GraphConnectionDrawing.DrawBezier(painter, startPosition, startTangent, endTangent, endPosition, color);
        }

        private (Vector2 Start, Vector2 End) GetOrBuildTangents(
            DialogAnswer answer,
            Vector2 start,
            Vector2 end,
            Rect sourceRect,
            Rect targetRect,
            DialogNode sourceNode,
            DialogNode targetNode,
            IReadOnlyDictionary<DialogNode, Rect> nodeRects)
        {
            ImplicitConnectionRouteKey implicitKey = new(sourceNode, targetNode);
            bool cachedRouteFound = answer != null
                ? answerRoutes.TryGetValue(answer, out CachedConnectionRoute cached)
                : implicitRoutes.TryGetValue(implicitKey, out cached);
            if (cachedRouteFound &&
                cached.LayoutVersion == layoutVersion &&
                GraphConnectionGeometry.ApproximatelyEqual(cached.StartPos, start) &&
                GraphConnectionGeometry.ApproximatelyEqual(cached.SourceRect, sourceRect) &&
                GraphConnectionGeometry.ApproximatelyEqual(cached.TargetRect, targetRect))
            {
                return (cached.StartTangent, cached.EndTangent);
            }

            obstacleRects.Clear();
            foreach (KeyValuePair<DialogNode, Rect> pair in nodeRects)
            {
                if (!ReferenceEquals(pair.Key, sourceNode) && !ReferenceEquals(pair.Key, targetNode))
                {
                    obstacleRects.Add(GraphConnectionGeometry.Expand(pair.Value, 8f));
                }
            }

            (Vector2 startTangent, Vector2 endTangent) = GraphBezierConnectionRouter.ResolveTangents(
                start, end, sourceRect, targetRect, obstacleRects);
            var route = new CachedConnectionRoute(
                layoutVersion, start, sourceRect, targetRect, startTangent, endTangent);
            if (answer != null)
            {
                answerRoutes[answer] = route;
            }
            else
            {
                implicitRoutes[implicitKey] = route;
            }

            return (startTangent, endTangent);
        }
    }
}
