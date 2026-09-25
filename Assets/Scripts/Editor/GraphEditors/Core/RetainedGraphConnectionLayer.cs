using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools
{
    /// <summary>
    /// Renders retained graph connections and coordinates their temporary drag filters.
    /// Spatial lookup is delegated to <see cref="GraphConnectionSpatialIndex{TNode,TConnection}"/>.
    /// </summary>
    internal sealed class RetainedGraphConnectionLayer<TNode, TConnection> : VisualElement
        where TNode : class
        where TConnection : class
    {
        private readonly IReadOnlyList<RetainedGraphConnection<TNode, TConnection>> connections;
        private readonly GraphConnectionSpatialIndex<TNode, TConnection> spatialIndex;
        private readonly Func<Rect> getVisibleGraphRect;
        private readonly Func<TNode, Rect?> getNodeRect;
        private readonly Func<TNode, bool> isDraggingNode;
        private readonly IRetainedGraphCanvasConnectionRenderer<TNode, TConnection> connectionRenderer;
        private readonly List<int> candidateConnectionIndices = new();
        private TNode excludedNode;
        private TNode includedNode;
        private bool includesOnlyNode;

        public RetainedGraphConnectionLayer(
            IReadOnlyList<RetainedGraphConnection<TNode, TConnection>> connections,
            Func<Rect> getVisibleGraphRect,
            Func<TNode, Rect?> getNodeRect,
            Func<TNode, bool> isDraggingNode,
            IRetainedGraphCanvasConnectionRenderer<TNode, TConnection> connectionRenderer,
            float workspaceWidth,
            float workspaceHeight)
        {
            this.connections = connections;
            this.getVisibleGraphRect = getVisibleGraphRect;
            this.getNodeRect = getNodeRect;
            this.isDraggingNode = isDraggingNode;
            this.connectionRenderer = connectionRenderer;
            spatialIndex = new GraphConnectionSpatialIndex<TNode, TConnection>(connections, getNodeRect);
            name = "retained-graph-connections";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.width = workspaceWidth;
            style.height = workspaceHeight;
            generateVisualContent += DrawConnections;
        }

        public bool IsConnectedTo(TNode node) => spatialIndex.IsConnectedTo(node);

        public void UpdateConnectionsFor(TNode node) => spatialIndex.UpdateConnectionsFor(node);

        public void ExcludeNode(TNode node)
        {
            excludedNode = node;
            includedNode = null;
            includesOnlyNode = false;
        }

        public void IncludeNode(TNode node)
        {
            includedNode = node;
            excludedNode = null;
            includesOnlyNode = true;
        }

        public void ClearNodeFilter()
        {
            excludedNode = null;
            includedNode = null;
            includesOnlyNode = false;
        }

        public void HideAll()
        {
            excludedNode = null;
            includedNode = null;
            includesOnlyNode = true;
        }

        private void DrawConnections(MeshGenerationContext context)
        {
            if (includesOnlyNode)
            {
                spatialIndex.FillConnectionsForNode(includedNode, candidateConnectionIndices);
            }
            else
            {
                spatialIndex.FillVisibleConnections(getVisibleGraphRect(), candidateConnectionIndices);
            }

            foreach (int index in candidateConnectionIndices)
            {
                RetainedGraphConnection<TNode, TConnection> connection = connections[index];
                bool isConnectedToExcludedNode = excludedNode != null &&
                    (ReferenceEquals(connection.Source, excludedNode) || ReferenceEquals(connection.Target, excludedNode));
                bool isConnectedToIncludedNode = includedNode != null &&
                    (ReferenceEquals(connection.Source, includedNode) || ReferenceEquals(connection.Target, includedNode));
                if (isConnectedToExcludedNode ||
                    (includesOnlyNode && (includedNode == null || !isConnectedToIncludedNode)))
                {
                    continue;
                }

                Rect? sourceRect = getNodeRect(connection.Source);
                Rect? targetRect = getNodeRect(connection.Target);
                if (!sourceRect.HasValue || !targetRect.HasValue)
                {
                    continue;
                }

                connectionRenderer.DrawRetainedConnection(
                    context.painter2D,
                    connection.Connection,
                    connection.Source,
                    connection.Target,
                    sourceRect.Value,
                    targetRect.Value,
                    isDraggingNode(connection.Source) || isDraggingNode(connection.Target));
            }
        }
    }
}
