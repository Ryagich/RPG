using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Owns the mutable camera state of a graph workspace.
    /// </summary>
    public interface IRetainedGraphCanvasViewport
    {
        float RetainedGraphZoom { get; set; }
        Vector2 RetainedGraphPanOffset { get; set; }
        bool RetainedGraphIsSelectingTarget { get; }
        void ClampRetainedGraphPan(float workspaceWidth, float workspaceHeight);
    }

    /// <summary>
    /// Supplies presentation values shared by the canvas background, grid and node overlays.
    /// </summary>
    public interface IRetainedGraphCanvasAppearance
    {
        string RetainedGraphEmptyStateMessage { get; }
        Color RetainedGraphCanvasColor { get; }
        Color RetainedGraphPanelColor { get; }
        Color RetainedGraphMinorGridColor { get; }
        Color RetainedGraphMajorGridColor { get; }
        Color RetainedGraphTargetBorderColor { get; }
    }

    /// <summary>
    /// Owns graph structure and the mapping from domain connections to visual connections.
    /// </summary>
    public interface IRetainedGraphCanvasSource<TNode, TConnection>
        where TNode : class
        where TConnection : class
    {
        bool RetainedGraphHasGraph { get; }
        void PrepareRetainedGraph();
        void ClearRetainedNodeRects();
        IEnumerable<TNode> GetRetainedGraphNodes();
        IEnumerable<RetainedGraphConnection<TNode, TConnection>> GetRetainedGraphConnections();
    }

    /// <summary>
    /// Owns domain-specific node geometry and IMGUI node contents.
    /// </summary>
    public interface IRetainedGraphCanvasNodePresenter<TNode>
        where TNode : class
    {
        Vector2 RetainedGraphNodeSize { get; }
        Vector2 GetRetainedNodePosition(TNode node);
        void SetRetainedNodePosition(TNode node, Vector2 position);
        void SetRetainedNodeRect(TNode node, Rect rect);
        string GetRetainedNodeTitle(TNode node);
        Color GetRetainedNodeTint(TNode node);
        bool IsRetainedNodeTargetable(TNode node);
        void DrawRetainedNode(TNode node);
    }

    /// <summary>
    /// Owns edits initiated by the canvas: selection, target assignment, deletion and persistence.
    /// </summary>
    public interface IRetainedGraphCanvasInteraction<TNode>
        where TNode : class
    {
        void DeleteRetainedNode(TNode node);
        void SelectRetainedNode(TNode node);
        void ClearRetainedNodeSelection();
        bool TrySelectRetainedTarget(TNode node);
        void MarkRetainedNodePositionDirty();
    }

    /// <summary>
    /// Renders a connection after the canvas has resolved the participating node rectangles.
    /// </summary>
    public interface IRetainedGraphCanvasConnectionRenderer<TNode, TConnection>
        where TNode : class
        where TConnection : class
    {
        void DrawRetainedConnection(
            Painter2D painter,
            TConnection connection,
            TNode source,
            TNode target,
            Rect sourceRect,
            Rect targetRect,
            bool isDragging);
    }

    public sealed class RetainedGraphCanvas<TNode, TConnection> : VisualElement, IGraphEditorCanvasView
        where TNode : class
        where TConnection : class
    {
        private const float WorkspaceWidth = 10000f;
        private const float WorkspaceHeight = 10000f;
        private const float ZoomMin = 0.25f;
        private const float ZoomMax = 2f;
        private const float NodeHeaderHeight = 24f;
        // Mirrors the dialog editor's retained canvas: only presenters close to the
        // viewport exist, while domain node geometry remains available for edges.
        private const float VirtualizationMargin = 240f;
        private const int VirtualizedNodeCreationBudget = 1;

        private readonly IRetainedGraphCanvasViewport viewport;
        private readonly IRetainedGraphCanvasAppearance appearance;
        private readonly IRetainedGraphCanvasSource<TNode, TConnection> source;
        private readonly IRetainedGraphCanvasNodePresenter<TNode> nodePresenter;
        private readonly IRetainedGraphCanvasInteraction<TNode> interaction;
        private readonly IRetainedGraphCanvasConnectionRenderer<TNode, TConnection> connectionRenderer;
        private VisualElement graphContent;
        private Label emptyState;
        private readonly Dictionary<TNode, NodeElement> nodeElements = new();
        private readonly Dictionary<TNode, Rect> nodeRects = new();
        private readonly HashSet<TNode> nodesWithPendingGeometry = new();
        private readonly HashSet<TNode> visibleNodes = new();
        private readonly HashSet<TNode> pendingVisualNodes = new();
        private readonly List<TNode> nodesToRemove = new();
        private ConnectionLayer connectionLayer;
        private ConnectionLayer dragConnectionLayer;
        private GridElement gridElement;
        private TNode draggedNode;
        private bool rebuildScheduled;
        private bool geometryRefreshScheduled;
        private bool visibleNodeCreationScheduled;
        private bool connectionsNeedRefreshAfterVirtualizedCreation;
        private bool isPanning;
        private int panPointerId = -1;
        private Vector2 panStartPointer;
        private Vector2 panStartOffset;

        /// <summary>
        /// Creates a canvas from independently-owned roles. Domain editors should use this
        /// overload rather than making their window implement the compatibility host.
        /// </summary>
        public RetainedGraphCanvas(
            IRetainedGraphCanvasViewport viewport,
            IRetainedGraphCanvasAppearance appearance,
            IRetainedGraphCanvasSource<TNode, TConnection> source,
            IRetainedGraphCanvasNodePresenter<TNode> nodePresenter,
            IRetainedGraphCanvasInteraction<TNode> interaction,
            IRetainedGraphCanvasConnectionRenderer<TNode, TConnection> connectionRenderer)
        {
            this.viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.nodePresenter = nodePresenter ?? throw new ArgumentNullException(nameof(nodePresenter));
            this.interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            this.connectionRenderer = connectionRenderer ?? throw new ArgumentNullException(nameof(connectionRenderer));
            InitializeVisualTree();
        }

        private void InitializeVisualTree()
        {
            name = "retained-graph-canvas";
            style.flexGrow = 1f;
            style.overflow = Overflow.Hidden;

            graphContent = new VisualElement { name = "retained-graph-content" };
            // Panning and zooming move the entire graph on every pointer event. Keep this
            // subtree out of the layout path and let UI Toolkit transform its cached mesh on
            // the GPU instead.
            graphContent.usageHints = UsageHints.DynamicTransform;
            graphContent.style.position = Position.Absolute;
            graphContent.style.left = 0f;
            graphContent.style.top = 0f;
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
            nodeRects.Clear();
            nodesWithPendingGeometry.Clear();
            visibleNodes.Clear();
            pendingVisualNodes.Clear();
            nodesToRemove.Clear();
            connectionLayer = null;
            dragConnectionLayer = null;
            gridElement = null;
            source.ClearRetainedNodeRects();

            if (!source.RetainedGraphHasGraph)
            {
                emptyState.style.display = DisplayStyle.Flex;
                RefreshGraphAppearance();
                return;
            }

            source.PrepareRetainedGraph();
            emptyState.style.display = DisplayStyle.None;
            gridElement = new GridElement(this);
            graphContent.Add(gridElement);

            foreach (TNode node in source.GetRetainedGraphNodes())
            {
                if (node == null)
                {
                    continue;
                }

                Vector2 position = nodePresenter.GetRetainedNodePosition(node);
                nodeRects[node] = new Rect(position, nodePresenter.RetainedGraphNodeSize);
                nodePresenter.SetRetainedNodeRect(node, nodeRects[node]);
            }

            var connections = new List<RetainedGraphConnection<TNode, TConnection>>();
            foreach (RetainedGraphConnection<TNode, TConnection> connection in source.GetRetainedGraphConnections())
            {
                if (connection.Source == null || connection.Target == null || connection.Connection == null ||
                    !nodeRects.ContainsKey(connection.Source) || !nodeRects.ContainsKey(connection.Target))
                {
                    continue;
                }

                connections.Add(connection);
            }

            connectionLayer = new ConnectionLayer(this, connections);
            graphContent.Insert(1, connectionLayer);
            dragConnectionLayer = new ConnectionLayer(this, connections);
            dragConnectionLayer.HideAll();
            graphContent.Insert(2, dragConnectionLayer);

            ApplyViewTransform();
            RefreshVisibleNodeElements();
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
            style.backgroundColor = appearance.RetainedGraphCanvasColor;
            emptyState.text = appearance.RetainedGraphEmptyStateMessage;
            emptyState.style.backgroundColor = appearance.RetainedGraphPanelColor;
            emptyState.style.color = appearance.RetainedGraphPanelColor.grayscale < 0.5f ? Color.white : Color.black;
            emptyState.style.borderTopColor = appearance.RetainedGraphMinorGridColor;
            emptyState.style.borderBottomColor = appearance.RetainedGraphMinorGridColor;
            emptyState.style.borderLeftColor = appearance.RetainedGraphMinorGridColor;
            emptyState.style.borderRightColor = appearance.RetainedGraphMinorGridColor;

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

            // Drag updates the graph rect directly. Ignore incidental layout notifications
            // while the node is represented by a visual transform.
            if (ReferenceEquals(draggedNode, nodeElement.Node))
            {
                return;
            }

            nodePresenter.SetRetainedNodeRect(nodeElement.Node, nodeElement.GetGraphRect());
            nodeRects[nodeElement.Node] = nodeElement.GetGraphRect();
            if (pendingVisualNodes.Count > 0 || visibleNodeCreationScheduled)
            {
                connectionsNeedRefreshAfterVirtualizedCreation = true;
                return;
            }
            nodesWithPendingGeometry.Add(nodeElement.Node);
            if (!geometryRefreshScheduled)
            {
                geometryRefreshScheduled = true;
                schedule.Execute(FlushPendingGeometryChanges).ExecuteLater(0);
            }
        }

        private void BeginNodeDrag(NodeElement nodeElement, PointerDownEvent evt)
        {
            if (viewport.RetainedGraphIsSelectingTarget)
            {
                if (interaction.TrySelectRetainedTarget(nodeElement.Node))
                {
                    RefreshTargetSelection();
                    RefreshConnections();
                }

                return;
            }

            interaction.SelectRetainedNode(nodeElement.Node);
            draggedNode = nodeElement.Node;
            connectionLayer?.ExcludeNode(nodeElement.Node);
            dragConnectionLayer?.IncludeNode(nodeElement.Node);
            connectionLayer?.MarkDirtyRepaint();
            dragConnectionLayer?.MarkDirtyRepaint();
            nodeElement.BeginDrag(evt);
        }

        private void MoveNode(NodeElement nodeElement, Vector2 graphPosition)
        {
            if (!ReferenceEquals(draggedNode, nodeElement.Node))
            {
                return;
            }

            Vector2 nodeSize = nodePresenter.RetainedGraphNodeSize;
            Vector2 clampedPosition = new(
                Mathf.Clamp(graphPosition.x, 0f, WorkspaceWidth - nodeSize.x),
                Mathf.Clamp(graphPosition.y, 0f, WorkspaceHeight - NodeHeaderHeight));
            nodePresenter.SetRetainedNodePosition(nodeElement.Node, clampedPosition);
            nodeElement.SetDragPosition(clampedPosition);
            nodePresenter.SetRetainedNodeRect(nodeElement.Node, nodeElement.GetGraphRect());
            nodeRects[nodeElement.Node] = nodeElement.GetGraphRect();
            dragConnectionLayer?.MarkDirtyRepaint();
        }

        private void EndNodeDrag(NodeElement nodeElement)
        {
            if (!ReferenceEquals(draggedNode, nodeElement.Node))
            {
                return;
            }

            nodeElement.CommitDragPosition(nodePresenter.GetRetainedNodePosition(nodeElement.Node));
            nodePresenter.SetRetainedNodeRect(nodeElement.Node, nodeElement.GetGraphRect());
            nodeRects[nodeElement.Node] = nodeElement.GetGraphRect();
            interaction.MarkRetainedNodePositionDirty();
            draggedNode = null;
            connectionLayer?.ClearNodeFilter();
            dragConnectionLayer?.HideAll();
            connectionLayer?.MarkDirtyRepaint();
            dragConnectionLayer?.MarkDirtyRepaint();
        }

        private void RefreshConnectionsFor(TNode node)
        {
            if (draggedNode != null)
            {
                if (ReferenceEquals(draggedNode, node))
                {
                    dragConnectionLayer?.MarkDirtyRepaint();
                }

                return;
            }

            if (connectionLayer != null && connectionLayer.IsConnectedTo(node))
            {
                connectionLayer.MarkDirtyRepaint();
            }
        }

        private void RefreshConnections()
        {
            connectionLayer?.MarkDirtyRepaint();
            dragConnectionLayer?.MarkDirtyRepaint();
        }

        private void FlushPendingGeometryChanges()
        {
            geometryRefreshScheduled = false;
            if (nodesWithPendingGeometry.Count == 0)
            {
                return;
            }

            nodesWithPendingGeometry.Clear();
            connectionLayer?.MarkDirtyRepaint();
            dragConnectionLayer?.MarkDirtyRepaint();
        }

        private void HandlePointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0 && (evt.target == this || evt.target == graphContent))
            {
                interaction.ClearRetainedNodeSelection();
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
            panStartOffset = viewport.RetainedGraphPanOffset;
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
            viewport.RetainedGraphPanOffset = panStartOffset + (pointerPosition - panStartPointer);
            viewport.ClampRetainedGraphPan(WorkspaceWidth, WorkspaceHeight);
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
            float oldZoom = viewport.RetainedGraphZoom;
            float newZoom = Mathf.Clamp(oldZoom - evt.delta.y * 0.05f, ZoomMin, ZoomMax);
            if (Mathf.Approximately(oldZoom, newZoom))
            {
                return;
            }

            Vector2 mousePosition = new(evt.mousePosition.x, evt.mousePosition.y);
            Vector2 graphPoint = (mousePosition - viewport.RetainedGraphPanOffset) / oldZoom;
            viewport.RetainedGraphZoom = newZoom;
            viewport.RetainedGraphPanOffset = mousePosition - graphPoint * newZoom;
            viewport.ClampRetainedGraphPan(WorkspaceWidth, WorkspaceHeight);
            ApplyViewTransform();
            evt.StopPropagation();
        }

        private void ApplyViewTransform()
        {
            graphContent.style.translate = new Translate(
                viewport.RetainedGraphPanOffset.x,
                viewport.RetainedGraphPanOffset.y);
            graphContent.style.scale = new Scale(new Vector2(viewport.RetainedGraphZoom, viewport.RetainedGraphZoom));
            gridElement?.MarkDirtyRepaint();
            RefreshVisibleNodeElements();
        }

        private Rect GetVisibleGraphRect()
        {
            float zoom = Mathf.Max(viewport.RetainedGraphZoom, 0.0001f);
            Vector2 pan = viewport.RetainedGraphPanOffset;
            Rect viewportRect = contentRect;
            return new Rect(-pan.x / zoom, -pan.y / zoom, viewportRect.width / zoom, viewportRect.height / zoom);
        }

        private void RefreshVisibleNodeElements()
        {
            if (!source.RetainedGraphHasGraph)
            {
                return;
            }

            Rect visibleRect = GetVisibleGraphRect();
            visibleRect.xMin -= VirtualizationMargin;
            visibleRect.yMin -= VirtualizationMargin;
            visibleRect.xMax += VirtualizationMargin;
            visibleRect.yMax += VirtualizationMargin;

            visibleNodes.Clear();
            foreach (KeyValuePair<TNode, Rect> pair in nodeRects)
            {
                if (ReferenceEquals(pair.Key, draggedNode) || pair.Value.Overlaps(visibleRect))
                {
                    visibleNodes.Add(pair.Key);
                }
            }

            nodesToRemove.Clear();
            foreach (TNode node in nodeElements.Keys)
            {
                if (!visibleNodes.Contains(node))
                {
                    nodesToRemove.Add(node);
                }
            }

            foreach (TNode node in nodesToRemove)
            {
                nodeElements[node].RemoveFromHierarchy();
                nodeElements.Remove(node);
            }

            pendingVisualNodes.RemoveWhere(node => !visibleNodes.Contains(node));
            foreach (TNode node in visibleNodes)
            {
                if (!nodeElements.ContainsKey(node))
                {
                    pendingVisualNodes.Add(node);
                }
            }

            ScheduleVisibleNodeCreation();
        }

        private void ScheduleVisibleNodeCreation()
        {
            if (visibleNodeCreationScheduled || pendingVisualNodes.Count == 0)
            {
                return;
            }

            visibleNodeCreationScheduled = true;
            schedule.Execute(CreatePendingVisibleNodes).ExecuteLater(0);
        }

        private void CreatePendingVisibleNodes()
        {
            visibleNodeCreationScheduled = false;
            int created = 0;
            while (created < VirtualizedNodeCreationBudget && pendingVisualNodes.Count > 0)
            {
                TNode node = pendingVisualNodes.First();
                pendingVisualNodes.Remove(node);
                created++;
                if (!visibleNodes.Contains(node) || nodeElements.ContainsKey(node))
                {
                    continue;
                }

                var nodeElement = new NodeElement(this, node);
                nodeElements[node] = nodeElement;
                graphContent.Add(nodeElement);
            }

            ScheduleVisibleNodeCreation();
            if (pendingVisualNodes.Count == 0 && connectionsNeedRefreshAfterVirtualizedCreation)
            {
                connectionsNeedRefreshAfterVirtualizedCreation = false;
                RefreshConnections();
            }
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
            private Vector2 layoutPosition;

            public NodeElement(RetainedGraphCanvas<TNode, TConnection> canvas, TNode node)
            {
                this.canvas = canvas;
                Node = node;
                name = "retained-graph-node";
                usageHints = UsageHints.DynamicTransform;
                Vector2 nodePosition = canvas.nodePresenter.GetRetainedNodePosition(node);
                layoutPosition = nodePosition;
                Vector2 nodeSize = canvas.nodePresenter.RetainedGraphNodeSize;
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

                removeButton = new Button(() => canvas.interaction.DeleteRetainedNode(Node))
                {
                    text = "×",
                    name = "retained-graph-node-remove"
                };
                removeButton.style.width = 20f;
                removeButton.style.height = 18f;
                removeButton.style.paddingLeft = 0f;
                removeButton.style.paddingRight = 0f;
                header.Add(removeButton);

                content = new IMGUIContainer(() => canvas.nodePresenter.DrawRetainedNode(Node))
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
                Vector2 nodeSize = canvas.nodePresenter.RetainedGraphNodeSize;
                float width = layout.width > 0f ? layout.width : nodeSize.x;
                float height = layout.height > 0f ? layout.height : nodeSize.y;
                return new Rect(canvas.nodePresenter.GetRetainedNodePosition(Node), new Vector2(width, height));
            }

            public void SetGraphPosition(Vector2 position)
            {
                layoutPosition = position;
                style.left = position.x;
                style.top = position.y;
                style.translate = new Translate(0f, 0f);
            }

            public void SetDragPosition(Vector2 position)
            {
                style.translate = new Translate(position.x - layoutPosition.x, position.y - layoutPosition.y);
            }

            public void CommitDragPosition(Vector2 position)
            {
                SetGraphPosition(position);
            }

            public void RefreshAppearance()
            {
                title.text = canvas.nodePresenter.GetRetainedNodeTitle(Node);
                header.style.backgroundColor = canvas.nodePresenter.GetRetainedNodeTint(Node);
                header.style.color = Color.black;
                style.backgroundColor = canvas.appearance.RetainedGraphPanelColor;
                style.borderTopColor = canvas.appearance.RetainedGraphMinorGridColor;
                style.borderBottomColor = canvas.appearance.RetainedGraphMinorGridColor;
                style.borderLeftColor = canvas.appearance.RetainedGraphMinorGridColor;
                style.borderRightColor = canvas.appearance.RetainedGraphMinorGridColor;
                content.MarkDirtyRepaint();
            }

            public void RefreshTargetSelection()
            {
                if (!canvas.viewport.RetainedGraphIsSelectingTarget)
                {
                    style.opacity = 1f;
                    style.borderTopColor = canvas.appearance.RetainedGraphMinorGridColor;
                    style.borderBottomColor = canvas.appearance.RetainedGraphMinorGridColor;
                    style.borderLeftColor = canvas.appearance.RetainedGraphMinorGridColor;
                    style.borderRightColor = canvas.appearance.RetainedGraphMinorGridColor;
                    return;
                }

                style.opacity = canvas.nodePresenter.IsRetainedNodeTargetable(Node) ? 1f : 0.45f;
                style.borderTopColor = canvas.appearance.RetainedGraphTargetBorderColor;
                style.borderBottomColor = canvas.appearance.RetainedGraphTargetBorderColor;
                style.borderLeftColor = canvas.appearance.RetainedGraphTargetBorderColor;
                style.borderRightColor = canvas.appearance.RetainedGraphTargetBorderColor;
            }

            public void BeginDrag(PointerDownEvent evt)
            {
                dragPointerId = evt.pointerId;
                dragStartPointer = new Vector2(evt.position.x, evt.position.y);
                dragStartPosition = canvas.nodePresenter.GetRetainedNodePosition(Node);
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
                Vector2 graphPosition = dragStartPosition + (pointerPosition - dragStartPointer) / canvas.viewport.RetainedGraphZoom;
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

        private sealed class ConnectionLayer : VisualElement
        {
            private readonly RetainedGraphCanvas<TNode, TConnection> canvas;
            private readonly IReadOnlyList<RetainedGraphConnection<TNode, TConnection>> connections;
            private TNode excludedNode;
            private TNode includedNode;
            private bool includesOnlyNode;

            public ConnectionLayer(
                RetainedGraphCanvas<TNode, TConnection> canvas,
                IReadOnlyList<RetainedGraphConnection<TNode, TConnection>> connections)
            {
                this.canvas = canvas;
                this.connections = connections;
                name = "retained-graph-connections";
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
                foreach (RetainedGraphConnection<TNode, TConnection> connection in connections)
                {
                    if (ReferenceEquals(connection.Source, node) || ReferenceEquals(connection.Target, node))
                    {
                        return true;
                    }
                }

                return false;
            }

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

            private void DrawConnection(MeshGenerationContext context)
            {
                foreach (RetainedGraphConnection<TNode, TConnection> connection in connections)
                {
                    bool isConnectedToExcludedNode = excludedNode != null &&
                        (ReferenceEquals(connection.Source, excludedNode) || ReferenceEquals(connection.Target, excludedNode));
                    bool isConnectedToIncludedNode = includedNode != null &&
                        (ReferenceEquals(connection.Source, includedNode) || ReferenceEquals(connection.Target, includedNode));
                    if (isConnectedToExcludedNode ||
                        (includesOnlyNode && (includedNode == null || !isConnectedToIncludedNode)))
                    {
                        continue;
                    }

                    if (!canvas.nodeRects.TryGetValue(connection.Source, out Rect sourceRect) ||
                        !canvas.nodeRects.TryGetValue(connection.Target, out Rect targetRect))
                    {
                        continue;
                    }

                    const float routeMargin = 100f;
                    Rect connectionBounds = Rect.MinMaxRect(
                        Mathf.Min(sourceRect.xMin, targetRect.xMin) - routeMargin,
                        Mathf.Min(sourceRect.yMin, targetRect.yMin) - routeMargin,
                        Mathf.Max(sourceRect.xMax, targetRect.xMax) + routeMargin,
                        Mathf.Max(sourceRect.yMax, targetRect.yMax) + routeMargin);
                    if (!connectionBounds.Overlaps(canvas.GetVisibleGraphRect()))
                    {
                        continue;
                    }

                    canvas.connectionRenderer.DrawRetainedConnection(
                        context.painter2D,
                        connection.Connection,
                        connection.Source,
                        connection.Target,
                        sourceRect,
                        targetRect,
                        canvas.IsDraggingNode(connection.Source) || canvas.IsDraggingNode(connection.Target));
                }
            }
        }

        private sealed class GridElement : VisualElement
        {
            private readonly RetainedGraphCanvas<TNode, TConnection> canvas;

            public GridElement(RetainedGraphCanvas<TNode, TConnection> canvas)
            {
                this.canvas = canvas;
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
                Rect visibleGraphRect = GetVisibleGraphRect();
                DrawGridLines(painter, visibleGraphRect, 40f, canvas.appearance.RetainedGraphMinorGridColor, 1f);
                DrawGridLines(painter, visibleGraphRect, 200f, canvas.appearance.RetainedGraphMajorGridColor, 1.4f);
            }

            private Rect GetVisibleGraphRect()
            {
                float zoom = Mathf.Max(canvas.viewport.RetainedGraphZoom, 0.0001f);
                Vector2 pan = canvas.viewport.RetainedGraphPanOffset;
                Rect viewport = canvas.contentRect;
                return new Rect(
                    -pan.x / zoom,
                    -pan.y / zoom,
                    viewport.width / zoom,
                    viewport.height / zoom);
            }

            private static void DrawGridLines(Painter2D painter, Rect visibleGraphRect, float step, Color color, float width)
            {
                painter.strokeColor = color;
                painter.lineWidth = width;

                float firstX = Mathf.Floor(visibleGraphRect.xMin / step) * step;
                float firstY = Mathf.Floor(visibleGraphRect.yMin / step) * step;
                for (float x = firstX; x <= visibleGraphRect.xMax + step; x += step)
                {
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, visibleGraphRect.yMin - step));
                    painter.LineTo(new Vector2(x, visibleGraphRect.yMax + step));
                    painter.Stroke();
                }

                for (float y = firstY; y <= visibleGraphRect.yMax + step; y += step)
                {
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(visibleGraphRect.xMin - step, y));
                    painter.LineTo(new Vector2(visibleGraphRect.xMax + step, y));
                    painter.Stroke();
                }
            }
        }
    }
}
