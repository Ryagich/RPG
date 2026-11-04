using System.Collections.Generic;
using EditorTools;
using Quests.Graph.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Quests.Graph.Editor
{
    /// <summary>Owns transient quest-transition anchors and cached draw routes.</summary>
    internal sealed class QuestGraphConnectionPresenter
    {
        private readonly Dictionary<QuestTransition, CachedTangents> tangentsCache = new();
        private readonly GraphEditorConnectionAnchorRegistry<QuestTransition> pickerAnchors = new();
        private readonly List<Rect> obstacleRects = new();
        private int layoutVersion;

        public void ClearNodePresentationState()
        {
            pickerAnchors.Clear();
            InvalidateRoutes();
        }

        public bool RecordPicker(QuestTransition transition, Rect pickerRect) => pickerAnchors.Record(transition, pickerRect);

        public void InvalidateRoutes()
        {
            tangentsCache.Clear();
            layoutVersion++;
        }

        public void DrawRetainedConnection(
            Painter2D painter,
            QuestTransition transition,
            QuestNode sourceNode,
            QuestNode targetNode,
            Rect sourceRect,
            Rect targetRect,
            bool isDragging,
            IReadOnlyDictionary<QuestNode, Rect> nodeRects,
            Color color)
        {
            (Vector2 defaultStart, Vector2 endPosition) = GraphConnectionGeometry.GetConnectionAnchors(sourceRect, targetRect);
            Vector2 startPosition = pickerAnchors.Resolve(transition, sourceRect, defaultStart);
            Vector2 startTangent = startPosition + GraphConnectionGeometry.GetDirectionForRectPoint(sourceRect, startPosition) * 60f;
            Vector2 endTangent = endPosition + GraphConnectionGeometry.GetDirectionForRectPoint(targetRect, endPosition) * 60f;
            if (!isDragging)
            {
                (startTangent, endTangent) = GetOrBuildTangents(
                    transition, startPosition, endPosition, sourceRect, targetRect, sourceNode, targetNode, nodeRects);
            }

            GraphConnectionDrawing.DrawBezier(painter, startPosition, startTangent, endTangent, endPosition, color);
        }

        private (Vector2 Start, Vector2 End) GetOrBuildTangents(
            QuestTransition transition,
            Vector2 start,
            Vector2 end,
            Rect sourceRect,
            Rect targetRect,
            QuestNode sourceNode,
            QuestNode targetNode,
            IReadOnlyDictionary<QuestNode, Rect> nodeRects)
        {
            if (tangentsCache.TryGetValue(transition, out CachedTangents cached) &&
                cached.LayoutVersion == layoutVersion &&
                GraphConnectionGeometry.ApproximatelyEqual(cached.StartPosition, start) &&
                GraphConnectionGeometry.ApproximatelyEqual(cached.SourceRect, sourceRect) &&
                GraphConnectionGeometry.ApproximatelyEqual(cached.TargetRect, targetRect))
            {
                return (cached.StartTangent, cached.EndTangent);
            }

            obstacleRects.Clear();
            foreach (KeyValuePair<QuestNode, Rect> pair in nodeRects)
            {
                if (!ReferenceEquals(pair.Key, sourceNode) && !ReferenceEquals(pair.Key, targetNode))
                {
                    obstacleRects.Add(GraphConnectionGeometry.Expand(pair.Value, 8f));
                }
            }

            (Vector2 startTangent, Vector2 endTangent) = GraphBezierConnectionRouter.ResolveTangents(
                start, end, sourceRect, targetRect, obstacleRects);
            tangentsCache[transition] = new CachedTangents(
                layoutVersion, start, sourceRect, targetRect, startTangent, endTangent);
            return (startTangent, endTangent);
        }

        private readonly struct CachedTangents
        {
            public CachedTangents(int layoutVersion, Vector2 startPosition, Rect sourceRect, Rect targetRect, Vector2 startTangent, Vector2 endTangent)
            {
                LayoutVersion = layoutVersion;
                StartPosition = startPosition;
                SourceRect = sourceRect;
                TargetRect = targetRect;
                StartTangent = startTangent;
                EndTangent = endTangent;
            }

            public int LayoutVersion { get; }
            public Vector2 StartPosition { get; }
            public Rect SourceRect { get; }
            public Rect TargetRect { get; }
            public Vector2 StartTangent { get; }
            public Vector2 EndTangent { get; }
        }
    }
}
