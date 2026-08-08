using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools
{
    public readonly struct RetainedGraphConnection<TNode, TConnection>
        where TNode : class
        where TConnection : class
    {
        public RetainedGraphConnection(TNode source, TNode target, TConnection connection)
        {
            Source = source;
            Target = target;
            Connection = connection;
        }

        public TNode Source { get; }
        public TNode Target { get; }
        public TConnection Connection { get; }
    }

    public interface IRetainedGraphCanvasHost<TNode, TConnection>
        where TNode : class
        where TConnection : class
    {
        string RetainedGraphEmptyStateMessage { get; }
        bool RetainedGraphHasGraph { get; }
        Vector2 RetainedGraphNodeSize { get; }
        float RetainedGraphZoom { get; set; }
        Vector2 RetainedGraphPanOffset { get; set; }
        bool RetainedGraphIsSelectingTarget { get; }
        Color RetainedGraphCanvasColor { get; }
        Color RetainedGraphPanelColor { get; }
        Color RetainedGraphMinorGridColor { get; }
        Color RetainedGraphMajorGridColor { get; }
        Color RetainedGraphTargetBorderColor { get; }

        void PrepareRetainedGraph();
        void ClearRetainedNodeRects();
        IEnumerable<TNode> GetRetainedGraphNodes();
        IEnumerable<RetainedGraphConnection<TNode, TConnection>> GetRetainedGraphConnections();
        Vector2 GetRetainedNodePosition(TNode node);
        void SetRetainedNodePosition(TNode node, Vector2 position);
        void SetRetainedNodeRect(TNode node, Rect rect);
        string GetRetainedNodeTitle(TNode node);
        Color GetRetainedNodeTint(TNode node);
        bool IsRetainedNodeTargetable(TNode node);
        void DrawRetainedNode(TNode node);
        void DeleteRetainedNode(TNode node);
        void SelectRetainedNode(TNode node);
        void ClearRetainedNodeSelection();
        bool TrySelectRetainedTarget(TNode node);
        void MarkRetainedGraphDirty();
        void ClampRetainedGraphPan(float workspaceWidth, float workspaceHeight);
        void DrawRetainedConnection(
            Painter2D painter,
            TConnection connection,
            TNode source,
            TNode target,
            Rect sourceRect,
            Rect targetRect,
            bool isDragging);
    }

    /// <summary>
    /// Retained-mode canvas for graph editor windows. Node inspectors remain IMGUI so the
    /// existing serialized editing logic is preserved, while panning, drag and connection
    /// redraw affect only the visual elements that actually changed.
    /// </summary>
    public sealed class RetainedGraphCanvas<TNode, TConnection> : VisualElement
        where TNode : class
        where TConnection : class
    {
        private const float WorkspaceWidth = 10000f;
        private const float WorkspaceHeight = 10000f;
        private const float ZoomMin = 0.25f;
        private const float ZoomMax = 2f;
        private const float NodeHeaderHeight = 24f;

        private readonly IRetainedGraphCanvasHost<TNode, TConnection> host;
        private readonly VisualElement graphContent;
        private readonly Label emptyState;
        private readonly Dictionary<TNode, NodeElement> nodeElements = new();
        private readonly List<ConnectionElement> connectionElements = new();
        private TNode draggedNode;
        private bool rebuildScheduled;
        private bool isPanning;
        private int panPointerId = -1;
        private Vector2 panStartPointer;
        private Vector2 panStartOffset;

        public RetainedGraphCanvas(IRetainedGraphCanvasHost<TNode, TConnection> host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            name = "retained-graph-canvas";
            style.flexGrow = 1f;
            style.overflow = Overflow.Hidden;

            graphContent = new VisualElement { name = "retained-graph-content" };
            graphContent.style.position = Position.Absolute;
            graphContent.style.width = WorkspaceWidth;
            graphContent.style.height = WorkspaceHeight;
            graphContent.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            hierarchy.Add(graphContent);

            emptyState = new Label { name = "retained-graph-empty-state" };
            emptyState.style.position = Position.Absolute;
            emptyState.style.left = 18f;
            emptyState.style.top = 18f;
            emptyState.style.paddingLeft = 10f;
            emptyState.style.paddingRight = 10f;
            emptyState.style.paddingTop = 8f;
            emptyState.style.paddingBottom = 8f;
            emptyState.style.borderTopWidth = 1f;
            emptyState.style.borderBottomWidth = 1f;
            emptyState.style.borderLeftWidth = 1f;
            emptyState.style.borderRightWidth = 1f;
            hierarchy.Add(emptyState);

            RegisterCallback<PointerDownEvent>(HandlePointerDown);
            RegisterCallback<PointerMoveEvent>(HandlePointerMove);
            RegisterCallback<PointerUpEvent>(HandlePointerUp);
            RegisterCallback<PointerCaptureOutEvent>(HandlePointerCaptureOut);
            RegisterCallback<WheelEvent>(HandleWheel);
            ApplyViewTransform();
        }

        public bool IsDraggingNode(TNode node)
        {
            return ReferenceEquals(draggedNode, node);
        }

        public void RebuildNow()
        {
            rebuildScheduled = false;
            graphContent.Clear();
            nodeElements.Clear();
            connectionElements.Clear();
            host.ClearRetainedNodeRects();

            if (!host.RetainedGraphHasGraph)
            {
                emptyState.style.display = DisplayStyle.Flex;
                RefreshGraphAppearance();
                return;
            }

            host.PrepareRetainedGraph();
            emptyState.style.display = DisplayStyle.None;
            graphContent.Add(new GridElement(host));

            foreach (TNode node in host.GetRetainedGraphNodes())
            {
                if (node == null)
                {
                    continue;
                }

                var nodeElement = new NodeElement(this, node);
                nodeElements[node] = nodeElement;
                graphContent.Add(nodeElement);
                host.SetRetainedNodeRect(node, nodeElement.GetGraphRect());
            }

            foreach (RetainedGraphConnection<TNode, TConnection> connection in host.GetRetainedGraphConnections())
            {
                if (connection.Source == null || connection.Target == null || connection.Connection == null ||
                    !nodeElements.ContainsKey(connection.Source) || !nodeElements.ContainsKey(connection.Target))
                {
                    continue;
                }

                var connectionElement = new ConnectionElement(this, connection);
                connectionElements.Add(connectionElement);
                graphContent.Insert(1, connectionElement);
            }

            ApplyViewTransform();
            RefreshGraphAppearance();
        }

        public void RequestRebuild()
        {
            if (rebuildScheduled)
            {
                return;
            }

            rebuildScheduled = true;
            schedule.Execute(RebuildNow).ExecuteLater(0);
        }

        public void RefreshGraphAppearance()
        {
            style.backgroundColor = host.RetainedGraphCanvasColor;
            emptyState.text = host.RetainedGraphEmptyStateMessage;
            emptyState.style.backgroundColor = host.RetainedGraphPanelColor;
            emptyState.style.color = host.RetainedGraphPanelColor.grayscale < 0.5f ? Color.white : Color.black;
            emptyState.style.borderTopColor = host.RetainedGraphMinorGridColor;
            emptyState.style.borderBottomColor = host.RetainedGraphMinorGridColor;
            emptyState.style.borderLeftColor = host.RetainedGraphMinorGridColor;
            emptyState.style.borderRightColor = host.RetainedGraphMinorGridColor;

            foreach (NodeElement nodeElement in nodeElements.Values)
            {
                nodeElement.RefreshAppearance();
            }

            RefreshConnections();
            RefreshTargetSelection();
        }

        public void RefreshTargetSelection()
        {
            foreach (NodeElement nodeElement in nodeElements.Values)
            {
                nodeElement.RefreshTargetSelection();
            }
        }

        private void NotifyNodeGeometryChanged(NodeElement nodeElement)
        {
            if (!nodeElements.ContainsKey(nodeElement.Node))
            {
                return;
            }

            host.SetRetainedNodeRect(nodeElement.Node, nodeElement.GetGraphRect());
            RefreshConnectionsFor(nodeElement.Node);
        }

        private void BeginNodeDrag(NodeElement nodeElement, PointerDownEvent evt)
        {
            if (host.RetainedGraphIsSelectingTarget)
            {
                if (host.TrySelectRetainedTarget(nodeElement.Node))
                {
                    RefreshTargetSelection();
                    RefreshConnections();
                }

                return;
            }

            draggedNode = nodeElement.Node;
            host.SelectRetainedNode(nodeElement.Node);
            RefreshConnections();
            nodeElement.BeginDrag(evt);
        }

        private void MoveNode(NodeElement nodeElement, Vector2 graphPosition)
        {
            if (!ReferenceEquals(draggedNode, nodeElement.Node))
            {
                return;
            }

            Vector2 nodeSize = host.RetainedGraphNodeSize;
            Vector2 clampedPosition = new(
                Mathf.Clamp(graphPosition.x, 0f, WorkspaceWidth - nodeSize.x),
                Mathf.Clamp(graphPosition.y, 0f, WorkspaceHeight - NodeHeaderHeight));
            host.SetRetainedNodePosition(nodeElement.Node, clampedPosition);
            nodeElement.SetGraphPosition(clampedPosition);
            host.SetRetainedNodeRect(nodeElement.Node, nodeElement.GetGraphRect());
            RefreshConnectionsFor(nodeElement.Node);
        }

        private void EndNodeDrag(NodeElement nodeElement)
        {
            if (!ReferenceEquals(draggedNode, nodeElement.Node))
            {
                return;
            }

            draggedNode = null;
            host.SetRetainedNodeRect(nodeElement.Node, nodeElement.GetGraphRect());
            host.MarkRetainedGraphDirty();
            RefreshConnectionsFor(nodeElement.Node);
        }

        private void RefreshConnectionsFor(TNode node)
        {
            foreach (ConnectionElement connectionElement in connectionElements)
            {
                if (connectionElement.IsConnectedTo(node))
                {
                    connectionElement.MarkDirtyRepaint();
                }
            }
        }

        private void RefreshConnections()
        {
            foreach (ConnectionElement connectionElement in connectionElements)
            {
                connectionElement.MarkDirtyRepaint();
            }
        }

        private void HandlePointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0 && (evt.target == this || evt.target == graphContent))
            {
                host.ClearRetainedNodeSelection();
                RefreshConnections();
                return;
            }

            if (evt.button != 1 || isPanning)
            {
                return;
            }

            isPanning = true;
            panPointerId = evt.pointerId;
            panStartPointer = new Vector2(evt.position.x, evt.position.y);
            panStartOffset = host.RetainedGraphPanOffset;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void HandlePointerMove(PointerMoveEvent evt)
        {
            if (!isPanning || evt.pointerId != panPointerId || !this.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            Vector2 pointerPosition = new(evt.position.x, evt.position.y);
            host.RetainedGraphPanOffset = panStartOffset + (pointerPosition - panStartPointer);
            host.ClampRetainedGraphPan(WorkspaceWidth, WorkspaceHeight);
            ApplyViewTransform();
            evt.StopPropagation();
        }

        private void HandlePointerUp(PointerUpEvent evt)
        {
            EndPan(evt.pointerId);
        }

        private void HandlePointerCaptureOut(PointerCaptureOutEvent evt)
        {
            EndPan(evt.pointerId);
        }

        private void EndPan(int pointerId)
        {
            if (!isPanning || pointerId != panPointerId)
            {
                return;
            }

            if (this.HasPointerCapture(pointerId))
            {
                this.ReleasePointer(pointerId);
            }

            isPanning = false;
            panPointerId = -1;
        }

        private void HandleWheel(WheelEvent evt)
        {
            float oldZoom = host.RetainedGraphZoom;
            float newZoom = Mathf.Clamp(oldZoom - evt.delta.y * 0.05f, ZoomMin, ZoomMax);
            if (Mathf.Approximately(oldZoom, newZoom))
            {
                return;
            }

            Vector2 mousePosition = new(evt.mousePosition.x, evt.mousePosition.y);
            Vector2 graphPoint = (mousePosition - host.RetainedGraphPanOffset) / oldZoom;
            host.RetainedGraphZoom = newZoom;
            host.RetainedGraphPanOffset = mousePosition - graphPoint * newZoom;
            host.ClampRetainedGraphPan(WorkspaceWidth, WorkspaceHeight);
            ApplyViewTransform();
            evt.StopPropagation();
        }

        private void ApplyViewTransform()
        {
            graphContent.style.left = host.RetainedGraphPanOffset.x;
            graphContent.style.top = host.RetainedGraphPanOffset.y;
            graphContent.style.scale = new Scale(new Vector2(host.RetainedGraphZoom, host.RetainedGraphZoom));
        }

        private sealed class NodeElement : VisualElement
        {
            private readonly RetainedGraphCanvas<TNode, TConnection> canvas;
            private readonly VisualElement header;
            private readonly Label title;
            private readonly Button removeButton;
            private readonly IMGUIContainer content;
            private int dragPointerId = -1;
            private Vector2 dragStartPointer;
            private Vector2 dragStartPosition;

            public NodeElement(RetainedGraphCanvas<TNode, TConnection> canvas, TNode node)
            {
                this.canvas = canvas;
                Node = node;
                name = "retained-graph-node";
                Vector2 nodePosition = canvas.host.GetRetainedNodePosition(node);
                Vector2 nodeSize = canvas.host.RetainedGraphNodeSize;
                style.position = Position.Absolute;
                style.left = nodePosition.x;
                style.top = nodePosition.y;
                style.width = nodeSize.x;
                style.minHeight = 80f;
                style.flexDirection = FlexDirection.Column;
                style.borderTopWidth = 1f;
                style.borderBottomWidth = 1f;
                style.borderLeftWidth = 1f;
                style.borderRightWidth = 1f;
                style.borderTopLeftRadius = 4f;
                style.borderTopRightRadius = 4f;
                style.borderBottomLeftRadius = 4f;
                style.borderBottomRightRadius = 4f;

                header = new VisualElement { name = "retained-graph-node-header" };
                header.style.height = NodeHeaderHeight;
                header.style.flexDirection = FlexDirection.Row;
                header.style.alignItems = Align.Center;
                header.style.paddingLeft = 7f;
                header.style.paddingRight = 3f;
                header.style.borderTopLeftRadius = 3f;
                header.style.borderTopRightRadius = 3f;
                hierarchy.Add(header);

                title = new Label { name = "retained-graph-node-title" };
                title.style.flexGrow = 1f;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.whiteSpace = WhiteSpace.NoWrap;
                title.style.overflow = Overflow.Hidden;
                title.style.textOverflow = TextOverflow.Ellipsis;
                header.Add(title);

                removeButton = new Button(() => canvas.host.DeleteRetainedNode(Node))
                {
                    text = "×",
                    name = "retained-graph-node-remove"
                };
                removeButton.style.width = 20f;
                removeButton.style.height = 18f;
                removeButton.style.paddingLeft = 0f;
                removeButton.style.paddingRight = 0f;
                header.Add(removeButton);

                content = new IMGUIContainer(() => canvas.host.DrawRetainedNode(Node))
                {
                    name = "retained-graph-node-content"
                };
                content.style.flexGrow = 1f;
                content.style.paddingLeft = 5f;
                content.style.paddingRight = 5f;
                content.style.paddingBottom = 5f;
                hierarchy.Add(content);

                header.RegisterCallback<PointerDownEvent>(HandleHeaderPointerDown);
                header.RegisterCallback<PointerMoveEvent>(HandleHeaderPointerMove);
                header.RegisterCallback<PointerUpEvent>(HandleHeaderPointerUp);
                header.RegisterCallback<PointerCaptureOutEvent>(HandleHeaderPointerCaptureOut);
                RegisterCallback<GeometryChangedEvent>(_ => canvas.NotifyNodeGeometryChanged(this));
                RefreshAppearance();
            }

            public TNode Node { get; }

            public Rect GetGraphRect()
            {
                Vector2 nodeSize = canvas.host.RetainedGraphNodeSize;
                float width = layout.width > 0f ? layout.width : nodeSize.x;
                float height = layout.height > 0f ? layout.height : nodeSize.y;
                return new Rect(canvas.host.GetRetainedNodePosition(Node), new Vector2(width, height));
            }

            public void SetGraphPosition(Vector2 position)
            {
                style.left = position.x;
                style.top = position.y;
            }

            public void RefreshAppearance()
            {
                title.text = canvas.host.GetRetainedNodeTitle(Node);
                header.style.backgroundColor = canvas.host.GetRetainedNodeTint(Node);
                header.style.color = Color.black;
                style.backgroundColor = canvas.host.RetainedGraphPanelColor;
                style.borderTopColor = canvas.host.RetainedGraphMinorGridColor;
                style.borderBottomColor = canvas.host.RetainedGraphMinorGridColor;
                style.borderLeftColor = canvas.host.RetainedGraphMinorGridColor;
                style.borderRightColor = canvas.host.RetainedGraphMinorGridColor;
                content.MarkDirtyRepaint();
            }

            public void RefreshTargetSelection()
            {
                if (!canvas.host.RetainedGraphIsSelectingTarget)
                {
                    style.opacity = 1f;
                    style.borderTopColor = canvas.host.RetainedGraphMinorGridColor;
                    style.borderBottomColor = canvas.host.RetainedGraphMinorGridColor;
                    style.borderLeftColor = canvas.host.RetainedGraphMinorGridColor;
                    style.borderRightColor = canvas.host.RetainedGraphMinorGridColor;
                    return;
                }

                style.opacity = canvas.host.IsRetainedNodeTargetable(Node) ? 1f : 0.45f;
                style.borderTopColor = canvas.host.RetainedGraphTargetBorderColor;
                style.borderBottomColor = canvas.host.RetainedGraphTargetBorderColor;
                style.borderLeftColor = canvas.host.RetainedGraphTargetBorderColor;
                style.borderRightColor = canvas.host.RetainedGraphTargetBorderColor;
            }

            public void BeginDrag(PointerDownEvent evt)
            {
                dragPointerId = evt.pointerId;
                dragStartPointer = new Vector2(evt.position.x, evt.position.y);
                dragStartPosition = canvas.host.GetRetainedNodePosition(Node);
                header.CapturePointer(evt.pointerId);
            }

            private void HandleHeaderPointerDown(PointerDownEvent evt)
            {
                if (evt.button != 0 || removeButton.worldBound.Contains(evt.position))
                {
                    return;
                }

                canvas.BeginNodeDrag(this, evt);
                evt.StopPropagation();
            }

            private void HandleHeaderPointerMove(PointerMoveEvent evt)
            {
                if (evt.pointerId != dragPointerId || !header.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                Vector2 pointerPosition = new(evt.position.x, evt.position.y);
                Vector2 graphPosition = dragStartPosition + (pointerPosition - dragStartPointer) / canvas.host.RetainedGraphZoom;
                canvas.MoveNode(this, graphPosition);
                evt.StopPropagation();
            }

            private void HandleHeaderPointerUp(PointerUpEvent evt)
            {
                EndDrag(evt.pointerId);
                evt.StopPropagation();
            }

            private void HandleHeaderPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                EndDrag(evt.pointerId);
            }

            private void EndDrag(int pointerId)
            {
                if (pointerId != dragPointerId)
                {
                    return;
                }

                if (header.HasPointerCapture(pointerId))
                {
                    header.ReleasePointer(pointerId);
                }

                dragPointerId = -1;
                canvas.EndNodeDrag(this);
            }
        }

        private sealed class ConnectionElement : VisualElement
        {
            private readonly RetainedGraphCanvas<TNode, TConnection> canvas;
            private readonly RetainedGraphConnection<TNode, TConnection> connection;

            public ConnectionElement(
                RetainedGraphCanvas<TNode, TConnection> canvas,
                RetainedGraphConnection<TNode, TConnection> connection)
            {
                this.canvas = canvas;
                this.connection = connection;
                name = "retained-graph-connection";
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.top = 0f;
                style.width = WorkspaceWidth;
                style.height = WorkspaceHeight;
                generateVisualContent += DrawConnection;
            }

            public bool IsConnectedTo(TNode node)
            {
                return ReferenceEquals(connection.Source, node) || ReferenceEquals(connection.Target, node);
            }

            private void DrawConnection(MeshGenerationContext context)
            {
                if (!canvas.nodeElements.TryGetValue(connection.Source, out NodeElement sourceElement) ||
                    !canvas.nodeElements.TryGetValue(connection.Target, out NodeElement targetElement))
                {
                    return;
                }

                canvas.host.DrawRetainedConnection(
                    context.painter2D,
                    connection.Connection,
                    connection.Source,
                    connection.Target,
                    sourceElement.GetGraphRect(),
                    targetElement.GetGraphRect(),
                    canvas.IsDraggingNode(connection.Source) || canvas.IsDraggingNode(connection.Target));
            }
        }

        private sealed class GridElement : VisualElement
        {
            private readonly IRetainedGraphCanvasHost<TNode, TConnection> host;

            public GridElement(IRetainedGraphCanvasHost<TNode, TConnection> host)
            {
                this.host = host;
                name = "retained-graph-grid";
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.top = 0f;
                style.width = WorkspaceWidth;
                style.height = WorkspaceHeight;
                generateVisualContent += DrawGrid;
            }

            private void DrawGrid(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                DrawGridLines(painter, 40f, host.RetainedGraphMinorGridColor, 1f);
                DrawGridLines(painter, 200f, host.RetainedGraphMajorGridColor, 1.4f);
            }

            private static void DrawGridLines(Painter2D painter, float step, Color color, float width)
            {
                painter.strokeColor = color;
                painter.lineWidth = width;

                for (float x = 0f; x <= WorkspaceWidth; x += step)
                {
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, 0f));
                    painter.LineTo(new Vector2(x, WorkspaceHeight));
                    painter.Stroke();
                }

                for (float y = 0f; y <= WorkspaceHeight; y += step)
                {
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(0f, y));
                    painter.LineTo(new Vector2(WorkspaceWidth, y));
                    painter.Stroke();
                }
            }
        }
    }
}
