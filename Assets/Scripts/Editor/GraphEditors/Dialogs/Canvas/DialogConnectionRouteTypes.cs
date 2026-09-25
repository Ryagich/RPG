using System;
using Dialogs.Graph.Model;
using UnityEngine;

namespace Dialogs.Graph.Editor
{
    /// <summary>Geometry cache entries used only by dialog connection routing.</summary>
    internal readonly struct CachedConnectionRoute
    {
        public CachedConnectionRoute(int layoutVersion, Vector2 startPos, Rect sourceRect, Rect targetRect, Vector2 startTangent, Vector2 endTangent)
        {
            LayoutVersion = layoutVersion;
            StartPos = startPos;
            SourceRect = sourceRect;
            TargetRect = targetRect;
            StartTangent = startTangent;
            EndTangent = endTangent;
        }

        public int LayoutVersion { get; }
        public Vector2 StartPos { get; }
        public Rect SourceRect { get; }
        public Rect TargetRect { get; }
        public Vector2 StartTangent { get; }
        public Vector2 EndTangent { get; }
    }

    internal readonly struct ConnectionPort
    {
        public ConnectionPort(Vector2 edgePoint, Vector2 outerPoint)
        {
            EdgePoint = edgePoint;
            OuterPoint = outerPoint;
        }

        public Vector2 EdgePoint { get; }
        public Vector2 OuterPoint { get; }
    }

    /// <summary>Stable editor-only identity for an implicit connection without a DialogAnswer.</summary>
    internal readonly struct ImplicitConnectionRouteKey : IEquatable<ImplicitConnectionRouteKey>
    {
        public ImplicitConnectionRouteKey(DialogNode sourceNode, DialogNode targetNode)
        {
            SourceNode = sourceNode;
            TargetNode = targetNode;
        }

        public DialogNode SourceNode { get; }
        public DialogNode TargetNode { get; }

        public bool Equals(ImplicitConnectionRouteKey other) =>
            ReferenceEquals(SourceNode, other.SourceNode) && ReferenceEquals(TargetNode, other.TargetNode);

        public override bool Equals(object obj) => obj is ImplicitConnectionRouteKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((SourceNode != null ? SourceNode.GetHashCode() : 0) * 397) ^
                       (TargetNode != null ? TargetNode.GetHashCode() : 0);
            }
        }
    }
}
