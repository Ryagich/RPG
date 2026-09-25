using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools
{
    /// <summary>
    /// Presentation adapter for graph editors that keeps the reusable canvas independent
    /// from a graph's domain model. It delegates graph-specific reads and mutations to
    /// the editor that owns the serialized assets and Undo lifecycle.
    /// </summary>
    internal sealed class DelegatingRetainedGraphCanvasAdapter<TNode, TConnection> :
        IRetainedGraphCanvasViewport,
        IRetainedGraphCanvasAppearance,
        IRetainedGraphCanvasSource<TNode, TConnection>,
        IRetainedGraphCanvasNodePresenter<TNode>,
        IRetainedGraphCanvasInteraction<TNode>,
        IRetainedGraphCanvasConnectionRenderer<TNode, TConnection>
        where TNode : class
        where TConnection : class
    {
        private readonly Func<bool> hasGraph;
        private readonly Action prepareGraph;
        private readonly Action clearNodeRects;
        private readonly Func<IEnumerable<TNode>> getNodes;
        private readonly Func<IEnumerable<RetainedGraphConnection<TNode, TConnection>>> getConnections;
        private readonly Func<TNode, Vector2> getNodePosition;
        private readonly Action<TNode, Vector2> setNodePosition;
        private readonly Action<TNode, Rect> setNodeRect;
        private readonly Func<TNode, string> getNodeTitle;
        private readonly Func<TNode, Color> getNodeTint;
        private readonly Func<TNode, bool> isNodeTargetable;
        private readonly Action<TNode> drawNode;
        private readonly Action<TNode> deleteNode;
        private readonly Action<TNode> selectNode;
        private readonly Action clearSelection;
        private readonly Func<TNode, bool> trySelectTarget;
        private readonly Action markNodePositionDirty;
        private readonly Action<float, float> clampPan;
        private readonly Action<Painter2D, TConnection, TNode, TNode, Rect, Rect, bool> drawConnection;
        private readonly Func<float> getZoom;
        private readonly Action<float> setZoom;
        private readonly Func<Vector2> getPanOffset;
        private readonly Action<Vector2> setPanOffset;
        private readonly Func<bool> isSelectingTarget;
        private readonly Func<Color> getCanvasColor;
        private readonly Func<Color> getPanelColor;
        private readonly Func<Color> getMinorGridColor;
        private readonly Func<Color> getMajorGridColor;
        private readonly Func<Color> getTargetBorderColor;

        public DelegatingRetainedGraphCanvasAdapter(
            string emptyStateMessage, Vector2 nodeSize, Func<bool> hasGraph, Action prepareGraph,
            Action clearNodeRects, Func<IEnumerable<TNode>> getNodes,
            Func<IEnumerable<RetainedGraphConnection<TNode, TConnection>>> getConnections,
            Func<TNode, Vector2> getNodePosition, Action<TNode, Vector2> setNodePosition,
            Action<TNode, Rect> setNodeRect, Func<TNode, string> getNodeTitle,
            Func<TNode, Color> getNodeTint, Func<TNode, bool> isNodeTargetable,
            Action<TNode> drawNode, Action<TNode> deleteNode, Action<TNode> selectNode,
            Action clearSelection, Func<TNode, bool> trySelectTarget, Action markNodePositionDirty,
            Action<float, float> clampPan,
            Action<Painter2D, TConnection, TNode, TNode, Rect, Rect, bool> drawConnection,
            Func<float> getZoom, Action<float> setZoom, Func<Vector2> getPanOffset,
            Action<Vector2> setPanOffset, Func<bool> isSelectingTarget,
            Func<Color> getCanvasColor, Func<Color> getPanelColor, Func<Color> getMinorGridColor,
            Func<Color> getMajorGridColor, Func<Color> getTargetBorderColor)
        {
            RetainedGraphEmptyStateMessage = emptyStateMessage ?? throw new ArgumentNullException(nameof(emptyStateMessage));
            RetainedGraphNodeSize = nodeSize;
            this.hasGraph = hasGraph ?? throw new ArgumentNullException(nameof(hasGraph));
            this.prepareGraph = prepareGraph ?? throw new ArgumentNullException(nameof(prepareGraph));
            this.clearNodeRects = clearNodeRects ?? throw new ArgumentNullException(nameof(clearNodeRects));
            this.getNodes = getNodes ?? throw new ArgumentNullException(nameof(getNodes));
            this.getConnections = getConnections ?? throw new ArgumentNullException(nameof(getConnections));
            this.getNodePosition = getNodePosition ?? throw new ArgumentNullException(nameof(getNodePosition));
            this.setNodePosition = setNodePosition ?? throw new ArgumentNullException(nameof(setNodePosition));
            this.setNodeRect = setNodeRect ?? throw new ArgumentNullException(nameof(setNodeRect));
            this.getNodeTitle = getNodeTitle ?? throw new ArgumentNullException(nameof(getNodeTitle));
            this.getNodeTint = getNodeTint ?? throw new ArgumentNullException(nameof(getNodeTint));
            this.isNodeTargetable = isNodeTargetable ?? throw new ArgumentNullException(nameof(isNodeTargetable));
            this.drawNode = drawNode ?? throw new ArgumentNullException(nameof(drawNode));
            this.deleteNode = deleteNode ?? throw new ArgumentNullException(nameof(deleteNode));
            this.selectNode = selectNode ?? throw new ArgumentNullException(nameof(selectNode));
            this.clearSelection = clearSelection ?? throw new ArgumentNullException(nameof(clearSelection));
            this.trySelectTarget = trySelectTarget ?? throw new ArgumentNullException(nameof(trySelectTarget));
            this.markNodePositionDirty = markNodePositionDirty ?? throw new ArgumentNullException(nameof(markNodePositionDirty));
            this.clampPan = clampPan ?? throw new ArgumentNullException(nameof(clampPan));
            this.drawConnection = drawConnection ?? throw new ArgumentNullException(nameof(drawConnection));
            this.getZoom = getZoom ?? throw new ArgumentNullException(nameof(getZoom));
            this.setZoom = setZoom ?? throw new ArgumentNullException(nameof(setZoom));
            this.getPanOffset = getPanOffset ?? throw new ArgumentNullException(nameof(getPanOffset));
            this.setPanOffset = setPanOffset ?? throw new ArgumentNullException(nameof(setPanOffset));
            this.isSelectingTarget = isSelectingTarget ?? throw new ArgumentNullException(nameof(isSelectingTarget));
            this.getCanvasColor = getCanvasColor ?? throw new ArgumentNullException(nameof(getCanvasColor));
            this.getPanelColor = getPanelColor ?? throw new ArgumentNullException(nameof(getPanelColor));
            this.getMinorGridColor = getMinorGridColor ?? throw new ArgumentNullException(nameof(getMinorGridColor));
            this.getMajorGridColor = getMajorGridColor ?? throw new ArgumentNullException(nameof(getMajorGridColor));
            this.getTargetBorderColor = getTargetBorderColor ?? throw new ArgumentNullException(nameof(getTargetBorderColor));
        }

        public string RetainedGraphEmptyStateMessage { get; }
        public bool RetainedGraphHasGraph => hasGraph();
        public Vector2 RetainedGraphNodeSize { get; }
        public float RetainedGraphZoom { get => getZoom(); set => setZoom(value); }
        public Vector2 RetainedGraphPanOffset { get => getPanOffset(); set => setPanOffset(value); }
        public bool RetainedGraphIsSelectingTarget => isSelectingTarget();
        public Color RetainedGraphCanvasColor => getCanvasColor();
        public Color RetainedGraphPanelColor => getPanelColor();
        public Color RetainedGraphMinorGridColor => getMinorGridColor();
        public Color RetainedGraphMajorGridColor => getMajorGridColor();
        public Color RetainedGraphTargetBorderColor => getTargetBorderColor();
        public void PrepareRetainedGraph() => prepareGraph();
        public void ClearRetainedNodeRects() => clearNodeRects();
        public IEnumerable<TNode> GetRetainedGraphNodes() => getNodes();
        public IEnumerable<RetainedGraphConnection<TNode, TConnection>> GetRetainedGraphConnections() => getConnections();
        public Vector2 GetRetainedNodePosition(TNode node) => getNodePosition(node);
        public void SetRetainedNodePosition(TNode node, Vector2 position) => setNodePosition(node, position);
        public void SetRetainedNodeRect(TNode node, Rect rect) => setNodeRect(node, rect);
        public string GetRetainedNodeTitle(TNode node) => getNodeTitle(node);
        public Color GetRetainedNodeTint(TNode node) => getNodeTint(node);
        public bool IsRetainedNodeTargetable(TNode node) => isNodeTargetable(node);
        public void DrawRetainedNode(TNode node) => drawNode(node);
        public void DeleteRetainedNode(TNode node) => deleteNode(node);
        public void SelectRetainedNode(TNode node) => selectNode(node);
        public void ClearRetainedNodeSelection() => clearSelection();
        public bool TrySelectRetainedTarget(TNode node) => trySelectTarget(node);
        public void MarkRetainedNodePositionDirty() => markNodePositionDirty();
        public void ClampRetainedGraphPan(float workspaceWidth, float workspaceHeight) => clampPan(workspaceWidth, workspaceHeight);
        public void DrawRetainedConnection(Painter2D painter, TConnection connection, TNode source, TNode target, Rect sourceRect, Rect targetRect, bool isDragging) =>
            drawConnection(painter, connection, source, target, sourceRect, targetRect, isDragging);
    }
}
