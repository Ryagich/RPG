using System;
using System.Collections.Generic;
using EditorTools;
using StateMachine.Graph.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateMachine.Graph.Editor
{
    /// <summary>
    /// Owns connection presentation and its transient caches. Graph topology and selection
    /// remain in the editor window; this type only resolves authored transition links into
    /// drawable routes.
    /// </summary>
    internal sealed class StateMachineGraphConnectionPresenter
    {
        private readonly Dictionary<Transition, CachedConnectionTangents> retainedConnectionTangentsCache = new();
        private readonly GraphEditorConnectionAnchorRegistry<Transition> transitionPickerAnchors = new();
        private readonly List<Rect> obstacleRects = new();
        private int layoutVersion;

        public void ClearNodePresentationState()
        {
            transitionPickerAnchors.Clear();
            InvalidateRoutes();
        }

        public bool RecordTransitionPicker(Transition transition, Rect pickerRect) =>
            transitionPickerAnchors.Record(transition, pickerRect);

        public bool ClearTransitionPickers(State state)
        {
            if (state?.Transitions == null)
            {
                return false;
            }

            bool changed = false;
            foreach (Transition transition in state.Transitions)
            {
                changed |= transitionPickerAnchors.Remove(transition);
            }

            return changed;
        }

        public void InvalidateRoutes()
        {
            retainedConnectionTangentsCache.Clear();
            layoutVersion++;
        }

        public void DrawRetainedConnection(
            Painter2D painter,
            Transition transition,
            Node sourceNode,
            Node targetNode,
            Rect sourceRect,
            Rect targetRect,
            bool isDragging,
            bool useObstacleAvoidance,
            IReadOnlyDictionary<Node, Rect> nodeRects,
            Color connectionColor)
        {
            (Vector2 defaultStart, Vector2 endPosition) = GraphConnectionGeometry.GetConnectionAnchors(sourceRect, targetRect);
            Vector2 startPosition = transitionPickerAnchors.ResolvePickerPoint(transition, sourceRect, defaultStart);
            Vector2 startTangent = startPosition + GraphConnectionGeometry.GetDirectionForRectPoint(sourceRect, startPosition) * 60f;
            Vector2 endTangent = endPosition + GraphConnectionGeometry.GetDirectionForRectPoint(targetRect, endPosition) * 60f;
            if (!isDragging && useObstacleAvoidance)
            {
                (startTangent, endTangent) = GetOrBuildRetainedTangents(
                    transition, startPosition, endPosition, sourceRect, targetRect, sourceNode, targetNode, nodeRects);
            }

            GraphConnectionDrawing.DrawBezier(painter, startPosition, startTangent, endTangent, endPosition, connectionColor);
        }

        private (Vector2 StartTangent, Vector2 EndTangent) GetOrBuildRetainedTangents(
            Transition transition,
            Vector2 startPosition,
            Vector2 endPosition,
            Rect sourceRect,
            Rect targetRect,
            Node sourceNode,
            Node targetNode,
            IReadOnlyDictionary<Node, Rect> nodeRects)
        {
            if (retainedConnectionTangentsCache.TryGetValue(transition, out CachedConnectionTangents cachedTangents) &&
                cachedTangents.LayoutVersion == layoutVersion &&
                GraphConnectionGeometry.ApproximatelyEqual(cachedTangents.StartPosition, startPosition) &&
                GraphConnectionGeometry.ApproximatelyEqual(cachedTangents.SourceRect, sourceRect) &&
                GraphConnectionGeometry.ApproximatelyEqual(cachedTangents.TargetRect, targetRect))
            {
                return (cachedTangents.StartTangent, cachedTangents.EndTangent);
            }

            obstacleRects.Clear();
            foreach (KeyValuePair<Node, Rect> pair in nodeRects)
            {
                if (!ReferenceEquals(pair.Key, sourceNode) && !ReferenceEquals(pair.Key, targetNode))
                {
                    obstacleRects.Add(GraphConnectionGeometry.Expand(pair.Value, 8f));
                }
            }

            (Vector2 startTangent, Vector2 endTangent) = GraphBezierConnectionRouter.ResolveTangentsFast(
                startPosition, endPosition, sourceRect, targetRect, obstacleRects);
            retainedConnectionTangentsCache[transition] = new CachedConnectionTangents(
                layoutVersion, startPosition, sourceRect, targetRect, startTangent, endTangent);
            return (startTangent, endTangent);
        }

        private readonly struct CachedConnectionTangents
        {
            public CachedConnectionTangents(int layoutVersion, Vector2 startPosition, Rect sourceRect, Rect targetRect, Vector2 startTangent, Vector2 endTangent)
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
