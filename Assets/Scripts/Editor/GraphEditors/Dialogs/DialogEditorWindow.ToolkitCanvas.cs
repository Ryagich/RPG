using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dialogs.Graph.Model;
using Dialogue;
using EditorTools;
using Quests.Editor;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Dialogs.Graph.Editor
{
    public partial class DialogEditorWindow
    {
        private sealed class DialogToolkitCanvas : VisualElement, IGraphEditorCanvasView
        {
            private const float NodeHeaderHeight = 24f;
            private const float VirtualizationMargin = 240f;
            private const int VirtualizedNodeCreationBudget = 1;

            private readonly DialogEditorWindow owner;
            private readonly VisualElement graphContent;
            private readonly Label emptyState;
            private readonly Dictionary<DialogNode, DialogToolkitNodeElement> nodeElements = new();
            private readonly HashSet<DialogNode> nodesWithPendingGeometry = new();
            private readonly HashSet<DialogNode> visibleNodes = new();
            private readonly List<DialogNode> nodesToRemove = new();
            private readonly HashSet<DialogNode> pendingVisualNodes = new();
            private DialogToolkitConnectionLayer connectionLayer;
            private DialogToolkitConnectionLayer selectionConnectionLayer;
            private DialogToolkitConnectionLayer dragConnectionLayer;
            private DialogToolkitConnectionCatalog connectionCatalog;
            private DialogToolkitGridElement gridElement;
            private DialogNode draggedNode;
            private DialogNode nodeWithSuppressedCommitGeometry;
            private Vector2 draggedNodePreviewPosition;
            private bool isNodeDragVisualActive;
            private bool visibleNodeCreationScheduled;
            private bool connectionsNeedRefreshAfterVirtualizedCreation;
            private bool rebuildScheduled;
            private bool geometryRefreshScheduled;
            private bool isPanning;
            private int panPointerId = -1;
            private Vector2 panStartPointer;
            private Vector2 panStartOffset;

            public DialogToolkitCanvas(DialogEditorWindow owner)
            {
                this.owner = owner;
                name = "dialog-toolkit-canvas";
                style.flexGrow = 1f;
                style.overflow = Overflow.Hidden;
                style.backgroundColor = owner.CanvasBackgroundColor;

                graphContent = new VisualElement
                {
                    name = "dialog-toolkit-graph-content"
                };
                // Camera motion is a high-frequency transform. DynamicTransform keeps it out
                // of UI Toolkit's layout pass, so moving the viewport does not relayout every
                // node IMGUI container and connection element.
                graphContent.usageHints = UsageHints.DynamicTransform;
                graphContent.style.position = Position.Absolute;
                graphContent.style.left = 0f;
                graphContent.style.top = 0f;
                graphContent.style.width = WorkspaceWidth;
                graphContent.style.height = WorkspaceHeight;
                graphContent.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
                hierarchy.Add(graphContent);

                emptyState = new Label("Create or load a dialog graph.")
                {
                    name = "dialog-toolkit-empty-state"
                };
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

            public bool IsDraggingNode(DialogNode node)
            {
                return draggedNode == node;
            }

            public float OwnerZoom => owner.zoom;
            public DialogEditorWindow Owner => owner;
            public Rect VisibleGraphRect => GetVisibleGraphRect();
            public bool OwnerIsSelectingTarget => owner.targetSelection.IsActive;
            public Color OwnerPanelBackgroundColor => owner.PanelBackgroundColor;
            public Color OwnerMinorGridColor => owner.MinorGridColor;
            public Color OwnerSelectionBorderColor => owner.GetSelectionOverlayColor(false);

            public void OwnerDrawNode(DialogNode node)
            {
                owner.DrawToolkitNode(node);
            }

            public void OwnerDeleteNode(DialogNode node)
            {
                owner.DeleteNode(node, false);
            }

            public string OwnerGetNodeTitle(DialogNode node)
            {
                return owner.GetNodeTitle(node);
            }

            public Color OwnerGetNodeTint(DialogNode node)
            {
                return owner.GetNodeTint(node);
            }

            public bool OwnerTryGetNodeRect(DialogNode node, out Rect rect)
            {
                if (!owner.nodeRects.TryGetValue(node, out rect))
                {
                    return false;
                }

                if (node == draggedNode && isNodeDragVisualActive)
                {
                    rect.position = draggedNodePreviewPosition;
                }

                return true;
            }

            public Color OwnerGetConnectionColor(
                DialogNode sourceNode,
                DialogNode targetNode,
                bool useSelectionHighlight)
            {
                return useSelectionHighlight
                    ? owner.GetConnectionColor(sourceNode, targetNode)
                    : owner.PrimaryConnectionColor;
            }

            public (Vector2 StartTangent, Vector2 EndTangent) OwnerGetConnectionTangents(
                DialogAnswer answer,
                Vector2 startPos,
                Vector2 endPos,
                Rect sourceRect,
                Rect targetRect,
                DialogNode sourceNode,
                DialogNode targetNode)
            {
                return owner.GetOrBuildConnectionTangents(
                    answer,
                    startPos,
                    endPos,
                    sourceRect,
                    targetRect,
                    sourceNode,
                    targetNode);
            }

            public void RebuildNow()
            {
                rebuildScheduled = false;
                graphContent.Clear();
                nodeElements.Clear();
                nodesWithPendingGeometry.Clear();
                visibleNodes.Clear();
                nodesToRemove.Clear();
                pendingVisualNodes.Clear();
                visibleNodeCreationScheduled = false;
                connectionsNeedRefreshAfterVirtualizedCreation = false;
                connectionLayer = null;
                selectionConnectionLayer = null;
                dragConnectionLayer = null;
                connectionCatalog = null;
                draggedNode = null;
                nodeWithSuppressedCommitGeometry = null;
                isNodeDragVisualActive = false;
                gridElement = null;
                owner.nodeRects.Clear();

                if (owner.currentGraph == null)
                {
                    emptyState.style.display = DisplayStyle.Flex;
                    RefreshGraphAppearance();
                    return;
                }

                if (owner.graphStructureDirty)
                {
                    owner.CleanupGraph();
                    owner.graphStructureDirty = false;
                }

                if (owner.graphIndex.IsDirty)
                {
                    owner.RebuildGraphCaches();
                }

                emptyState.style.display = DisplayStyle.None;
                gridElement = new DialogToolkitGridElement(this);
                graphContent.Add(gridElement);

                foreach (DialogNode node in owner.currentGraph.Nodes)
                {
                    if (node == null)
                    {
                        continue;
                    }

                    owner.nodeRects[node] = new Rect(node.Position, new Vector2(DialogNodeWidth, 220f));
                }

                connectionCatalog = new DialogToolkitConnectionCatalog(this);
                connectionLayer = new DialogToolkitConnectionLayer(this, false, connectionCatalog);
                graphContent.Insert(1, connectionLayer);
                selectionConnectionLayer = new DialogToolkitConnectionLayer(this, true, connectionCatalog);
                selectionConnectionLayer.HideAll();
                graphContent.Insert(2, selectionConnectionLayer);
                dragConnectionLayer = new DialogToolkitConnectionLayer(this, true, connectionCatalog);
                dragConnectionLayer.HideAll();
                graphContent.Insert(3, dragConnectionLayer);

                foreach (DialogNode sourceNode in owner.currentGraph.Nodes)
                {
                    if (sourceNode?.Phrase == null)
                    {
                        continue;
                    }

                    foreach (DialogAnswer answer in GetConnectionAnswers(sourceNode.Phrase))
                    {
                        if (answer?.NextPhrase == null ||
                            !owner.graphIndex.TryGetNode(answer.NextPhrase, out DialogNode targetNode))
                        {
                            continue;
                        }

                        AddConnection(sourceNode, targetNode, answer, false);
                    }

                    foreach (DialogPhrase returnAction in owner.GetImplicitConversationReturnActions(sourceNode.Phrase))
                    {
                        if (owner.graphIndex.TryGetNode(returnAction, out DialogNode targetNode))
                        {
                            AddConnection(sourceNode, targetNode, null, true);
                        }
                    }
                }

                ApplyViewTransform();
                RefreshVisibleNodeElements();
                RefreshGraphAppearance();
            }

            private void AddConnection(
                DialogNode sourceNode,
                DialogNode targetNode,
                DialogAnswer answer,
                bool isImplicit)
            {
                connectionCatalog.Add(new DialogToolkitConnectionElement(
                    this,
                    sourceNode,
                    targetNode,
                    answer,
                    isImplicit));
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
                style.backgroundColor = owner.CanvasBackgroundColor;
                emptyState.style.backgroundColor = owner.PanelBackgroundColor;
                emptyState.style.color = owner.ControlContentColor;
                emptyState.style.borderTopColor = owner.MinorGridColor;
                emptyState.style.borderBottomColor = owner.MinorGridColor;
                emptyState.style.borderLeftColor = owner.MinorGridColor;
                emptyState.style.borderRightColor = owner.MinorGridColor;

                foreach (DialogToolkitNodeElement nodeElement in nodeElements.Values)
                {
                    nodeElement.RefreshAppearance();
                }

                RefreshConnections();
                RefreshConnectionHighlights();
                RefreshTargetSelection();
            }

            public void RefreshConnectionHighlights()
            {
                if (selectionConnectionLayer == null)
                {
                    return;
                }

                if (draggedNode == null && !isNodeDragVisualActive && owner.activeConnectionNode != null)
                {
                    selectionConnectionLayer.IncludeNode(owner.activeConnectionNode);
                }
                else
                {
                    selectionConnectionLayer.HideAll();
                }

                selectionConnectionLayer.RequestRepaint();
            }

            public void InvalidateNodeLayout(DialogPhrase phrase)
            {
                if (phrase == null || !owner.graphIndex.TryGetNode(phrase, out DialogNode node) ||
                    !nodeElements.TryGetValue(node, out DialogToolkitNodeElement nodeElement))
                {
                    return;
                }

                // A foldout changes an IMGUI container's desired height. A repaint alone does
                // not notify its UI Toolkit host, leaving later answers clipped until another
                // unrelated layout pass occurs.
                nodeElement.MarkContentLayoutDirty();
            }

            public void RefreshTargetSelection()
            {
                foreach (DialogToolkitNodeElement nodeElement in nodeElements.Values)
                {
                    nodeElement.RefreshTargetSelection();
                }
            }

            public void NotifyNodeGeometryChanged(DialogToolkitNodeElement nodeElement)
            {
                if (!nodeElements.ContainsKey(nodeElement.Node))
                {
                    return;
                }

                // Moving a node is committed by transferring its preview transform to left/top.
                // That transfer produces one GeometryChangedEvent; its connection refresh is
                // already requested explicitly by EndNodeDrag, so accepting it would create
                // a layout-to-repaint feedback cycle.
                if (draggedNode == nodeElement.Node)
                {
                    return;
                }

                if (nodeWithSuppressedCommitGeometry == nodeElement.Node)
                {
                    nodeWithSuppressedCommitGeometry = null;
                    return;
                }

                owner.nodeRects[nodeElement.Node] = nodeElement.GetGraphRect();
                connectionCatalog?.UpdateConnectionsFor(nodeElement.Node);
                if (pendingVisualNodes.Count > 0 || visibleNodeCreationScheduled)
                {
                    // Node presenters are intentionally created over multiple editor ticks.
                    // Repainting all edges after every presenter layout would reintroduce the
                    // same frame spike that virtualization is meant to avoid.
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

            public void BeginNodeDrag(DialogToolkitNodeElement nodeElement, PointerDownEvent evt)
            {
                if (owner.targetSelection.IsActive)
                {
                    return;
                }

                draggedNode = nodeElement.Node;
                draggedNodePreviewPosition = nodeElement.Node.Position;
                SelectNode(nodeElement.Node);
                // Selecting a node normally renders its highlighted connections. On pointer
                // down for a drag that work is immediately obsolete and may synchronously
                // route every connection of the next selected node before the drag layers are
                // hidden. Mark the drag active first so the selection layer stays empty.
                RefreshConnectionHighlights();
                nodeElement.BeginDrag(evt);
            }

            public void MoveNode(DialogToolkitNodeElement nodeElement, Vector2 graphPosition)
            {
                if (draggedNode != nodeElement.Node)
                {
                    return;
                }

                Vector2 newPosition = ClampNodePosition(graphPosition);
                if (!isNodeDragVisualActive)
                {
                    if (DialogEditorWindow.ApproximatelyEqual(draggedNodePreviewPosition, newPosition))
                    {
                        return;
                    }

                    isNodeDragVisualActive = true;
                    connectionLayer?.ExcludeNode(nodeElement.Node);
                    if (connectionLayer != null)
                    {
                        connectionLayer.style.display = DisplayStyle.None;
                    }

                    selectionConnectionLayer?.HideAll();
                    dragConnectionLayer?.HideAll();
                }

                // The drag preview is presentation-only. Do not write the graph model or
                // change left/top until PointerUp, otherwise each pointer event can trigger
                // a full UI Toolkit layout pass through GeometryChangedEvent.
                draggedNodePreviewPosition = newPosition;
                nodeElement.SetDragPosition(draggedNodePreviewPosition);
            }

            public void EndNodeDrag(DialogToolkitNodeElement nodeElement)
            {
                if (draggedNode != nodeElement.Node)
                {
                    return;
                }

                if (!isNodeDragVisualActive)
                {
                    draggedNode = null;
                    RefreshConnectionHighlights();
                    return;
                }

                nodeWithSuppressedCommitGeometry = nodeElement.Node;
                nodeElement.Node.Position = draggedNodePreviewPosition;
                nodeElement.CommitDragPosition(draggedNodePreviewPosition);
                owner.nodeRects[nodeElement.Node] = nodeElement.GetGraphRect();
                connectionCatalog?.UpdateConnectionsFor(nodeElement.Node);
                owner.MarkNodePositionDirty();
                draggedNode = null;
                isNodeDragVisualActive = false;
                connectionLayer?.ClearNodeFilter();
                if (connectionLayer != null)
                {
                    connectionLayer.style.display = DisplayStyle.Flex;
                }

                dragConnectionLayer?.HideAll();
                connectionLayer?.RequestRepaint();
                RefreshConnectionHighlights();
                schedule.Execute(ClearSuppressedCommitGeometry).ExecuteLater(50);
            }

            private void ClearSuppressedCommitGeometry()
            {
                nodeWithSuppressedCommitGeometry = null;
            }

            private void SelectNode(DialogNode node)
            {
                if (owner.activeConnectionNode == node)
                {
                    return;
                }

                owner.activeConnectionNode = node;
            }

            private void RefreshConnectionsFor(DialogNode node)
            {
                if (draggedNode != null)
                {
                    if (draggedNode == node)
                    {
                        dragConnectionLayer?.RequestRepaint();
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
                connectionLayer?.RequestRepaint();
                if (draggedNode != null)
                {
                    dragConnectionLayer?.RequestRepaint();
                }
            }

            private void FlushPendingGeometryChanges()
            {
                geometryRefreshScheduled = false;
                if (nodesWithPendingGeometry.Count == 0)
                {
                    return;
                }

                nodesWithPendingGeometry.Clear();
                connectionLayer?.RequestRepaint();
                if (draggedNode != null)
                {
                    dragConnectionLayer?.RequestRepaint();
                }
            }

            private void HandlePointerDown(PointerDownEvent evt)
            {
                if (evt.button == 0 && (evt.target == this || evt.target == graphContent))
                {
                    if (owner.activeConnectionNode != null)
                    {
                        owner.activeConnectionNode = null;
                        RefreshConnectionHighlights();
                    }

                    return;
                }

                if (evt.button != 1 || isPanning)
                {
                    return;
                }

                isPanning = true;
                panPointerId = evt.pointerId;
                panStartPointer = evt.position;
                panStartOffset = owner.panOffset;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }

            private void HandlePointerMove(PointerMoveEvent evt)
            {
                if (!isPanning || evt.pointerId != panPointerId || !this.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
                owner.panOffset = panStartOffset + (pointerPosition - panStartPointer);
                owner.ClampPanToWorkspace(WorkspaceWidth, WorkspaceHeight);
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
                float zoomDelta = -evt.delta.y * 0.05f;
                float oldZoom = owner.zoom;
                float newZoom = Mathf.Clamp(owner.zoom + zoomDelta, ZoomMin, ZoomMax);
                if (Mathf.Approximately(oldZoom, newZoom))
                {
                    return;
                }

                Vector2 graphPoint = (evt.mousePosition - owner.panOffset) / oldZoom;
                owner.zoom = newZoom;
                owner.panOffset = evt.mousePosition - graphPoint * newZoom;
                owner.ClampPanToWorkspace(WorkspaceWidth, WorkspaceHeight);
                ApplyViewTransform();
                evt.StopPropagation();
            }

            private void ApplyViewTransform()
            {
                graphContent.style.translate = new Translate(owner.panOffset.x, owner.panOffset.y);
                graphContent.style.scale = new Scale(new Vector2(owner.zoom, owner.zoom));
                gridElement?.MarkDirtyRepaint();
                RefreshVisibleNodeElements();
            }

            private void RefreshVisibleNodeElements()
            {
                if (owner.currentGraph?.Nodes == null)
                {
                    return;
                }

                Rect viewport = GetVisibleGraphRect();
                viewport.xMin -= VirtualizationMargin;
                viewport.yMin -= VirtualizationMargin;
                viewport.xMax += VirtualizationMargin;
                viewport.yMax += VirtualizationMargin;

                visibleNodes.Clear();
                foreach (DialogNode node in owner.currentGraph.Nodes)
                {
                    if (node == null || !owner.nodeRects.TryGetValue(node, out Rect rect))
                    {
                        continue;
                    }

                    if (node == draggedNode || node == owner.activeConnectionNode || rect.Overlaps(viewport))
                    {
                        visibleNodes.Add(node);
                    }
                }

                nodesToRemove.Clear();
                foreach (DialogNode node in nodeElements.Keys)
                {
                    if (!visibleNodes.Contains(node))
                    {
                        nodesToRemove.Add(node);
                    }
                }

                foreach (DialogNode node in nodesToRemove)
                {
                    nodeElements[node].RemoveFromHierarchy();
                    nodeElements.Remove(node);
                }

                pendingVisualNodes.RemoveWhere(node => !visibleNodes.Contains(node));
                foreach (DialogNode node in visibleNodes)
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
                int processedNodes = 0;
                while (processedNodes < VirtualizedNodeCreationBudget && pendingVisualNodes.Count > 0)
                {
                    DialogNode node = null;
                    foreach (DialogNode pendingNode in pendingVisualNodes)
                    {
                        node = pendingNode;
                        break;
                    }

                    pendingVisualNodes.Remove(node);
                    processedNodes++;
                    if (node == null || !visibleNodes.Contains(node) || nodeElements.ContainsKey(node))
                    {
                        continue;
                    }

                    var nodeElement = new DialogToolkitNodeElement(this, node);
                    nodeElements[node] = nodeElement;
                    graphContent.Add(nodeElement);
                }

                ScheduleVisibleNodeCreation();
                if (pendingVisualNodes.Count == 0 && connectionsNeedRefreshAfterVirtualizedCreation)
                {
                    connectionsNeedRefreshAfterVirtualizedCreation = false;
                    connectionLayer?.RequestRepaint();
                    RefreshConnectionHighlights();
                }
            }

            private Rect GetVisibleGraphRect()
            {
                float zoom = Mathf.Max(owner.zoom, 0.0001f);
                Rect viewport = contentRect;
                return new Rect(
                    -owner.panOffset.x / zoom,
                    -owner.panOffset.y / zoom,
                    viewport.width / zoom,
                    viewport.height / zoom);
            }

            private static Vector2 ClampNodePosition(Vector2 position)
            {
                return new Vector2(
                    Mathf.Clamp(position.x, 0f, WorkspaceWidth - DialogNodeWidth),
                    Mathf.Clamp(position.y, 0f, WorkspaceHeight - NodeHeaderHeight));
            }
        }

        private sealed class DialogToolkitNodeElement : VisualElement
        {
            private readonly DialogToolkitCanvas canvas;
            private readonly VisualElement header;
            private readonly Label title;
            private readonly IMGUIContainer content;
            private int dragPointerId = -1;
            private Vector2 dragStartPointer;
            private Vector2 dragStartPosition;
            private Vector2 layoutPosition;

            public DialogToolkitNodeElement(DialogToolkitCanvas canvas, DialogNode node)
            {
                this.canvas = canvas;
                Node = node;
                name = "dialog-toolkit-node";
                usageHints = UsageHints.DynamicTransform;
                layoutPosition = node.Position;
                style.position = Position.Absolute;
                style.left = node.Position.x;
                style.top = node.Position.y;
                style.width = DialogNodeWidth;
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

                header = new VisualElement
                {
                    name = "dialog-toolkit-node-header"
                };
                header.style.height = 24f;
                header.style.flexDirection = FlexDirection.Row;
                header.style.alignItems = Align.Center;
                header.style.paddingLeft = 7f;
                header.style.paddingRight = 3f;
                header.style.borderTopLeftRadius = 3f;
                header.style.borderTopRightRadius = 3f;
                hierarchy.Add(header);

                title = new Label
                {
                    name = "dialog-toolkit-node-title"
                };
                title.style.flexGrow = 1f;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.whiteSpace = WhiteSpace.NoWrap;
                title.style.overflow = Overflow.Hidden;
                title.style.textOverflow = TextOverflow.Ellipsis;
                header.Add(title);

                var removeButton = new Button(() => canvas.OwnerDeleteNode(Node))
                {
                    text = "×",
                    name = "dialog-toolkit-node-remove"
                };
                removeButton.style.width = 20f;
                removeButton.style.height = 18f;
                removeButton.style.paddingLeft = 0f;
                removeButton.style.paddingRight = 0f;
                header.Add(removeButton);

                content = new IMGUIContainer(() => canvas.OwnerDrawNode(Node))
                {
                    name = "dialog-toolkit-node-content"
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

            public DialogNode Node { get; }

            public Rect GetGraphRect()
            {
                float width = layout.width > 0f ? layout.width : DialogNodeWidth;
                float height = layout.height > 0f ? layout.height : 220f;
                return new Rect(Node.Position, new Vector2(width, height));
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

            public void MarkContentLayoutDirty()
            {
                content.MarkDirtyLayout();
                content.MarkDirtyRepaint();
            }

            public void RefreshAppearance()
            {
                title.text = canvas.OwnerGetNodeTitle(Node);
                Color tint = canvas.OwnerGetNodeTint(Node);
                header.style.backgroundColor = tint;
                header.style.color = Color.black;
                style.backgroundColor = canvas.OwnerPanelBackgroundColor;
                style.borderTopColor = canvas.OwnerMinorGridColor;
                style.borderBottomColor = canvas.OwnerMinorGridColor;
                style.borderLeftColor = canvas.OwnerMinorGridColor;
                style.borderRightColor = canvas.OwnerMinorGridColor;
                content.MarkDirtyRepaint();
            }

            public void RefreshTargetSelection()
            {
                if (!canvas.OwnerIsSelectingTarget)
                {
                    style.opacity = 1f;
                    Color defaultBorderColor = canvas.OwnerMinorGridColor;
                    style.borderTopColor = defaultBorderColor;
                    style.borderBottomColor = defaultBorderColor;
                    style.borderLeftColor = defaultBorderColor;
                    style.borderRightColor = defaultBorderColor;
                    return;
                }

                style.opacity = Node.Phrase == null ? 0.45f : 1f;
                Color selectionBorderColor = canvas.OwnerSelectionBorderColor;
                style.borderTopColor = selectionBorderColor;
                style.borderBottomColor = selectionBorderColor;
                style.borderLeftColor = selectionBorderColor;
                style.borderRightColor = selectionBorderColor;
            }

            public void BeginDrag(PointerDownEvent evt)
            {
                dragPointerId = evt.pointerId;
                dragStartPointer = evt.position;
                dragStartPosition = Node.Position;
                header.CapturePointer(evt.pointerId);
            }

            private void HandleHeaderPointerDown(PointerDownEvent evt)
            {
                if (evt.button != 0 || evt.target is Button)
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

                Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
                Vector2 graphPosition = dragStartPosition + (pointerPosition - dragStartPointer) / canvas.OwnerZoom;
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

        private sealed class DialogToolkitConnectionElement : VisualElement
        {
            private readonly DialogToolkitCanvas canvas;
            private readonly DialogNode sourceNode;
            private readonly DialogNode targetNode;
            private readonly DialogAnswer answer;
            private readonly bool isImplicit;

            public DialogToolkitConnectionElement(
                DialogToolkitCanvas canvas,
                DialogNode sourceNode,
                DialogNode targetNode,
                DialogAnswer answer,
                bool isImplicit)
            {
                this.canvas = canvas;
                this.sourceNode = sourceNode;
                this.targetNode = targetNode;
                this.answer = answer;
                this.isImplicit = isImplicit;
                name = "dialog-toolkit-connection";
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.top = 0f;
                style.width = WorkspaceWidth;
                style.height = WorkspaceHeight;
                generateVisualContent += DrawConnection;
            }

            public bool IsConnectedTo(DialogNode node)
            {
                return sourceNode == node || targetNode == node;
            }

            public DialogNode SourceNode => sourceNode;
            public DialogNode TargetNode => targetNode;

            private void DrawConnection(MeshGenerationContext context)
            {
                Draw(context.painter2D, true);
            }

            public void Draw(Painter2D painter, bool useSelectionHighlight)
            {
                if (!canvas.OwnerTryGetNodeRect(sourceNode, out Rect sourceRect) ||
                    !canvas.OwnerTryGetNodeRect(targetNode, out Rect targetRect))
                {
                    return;
                }

                Vector2 startPos = new Vector2(sourceRect.xMax - 12f, sourceRect.center.y);
                Vector2 endPos = DialogConnectionRouter.GetNearestSideCenter(targetRect, startPos);
                Vector2 endDirection = DialogConnectionRouter.GetConnectionDirectionForRectPoint(targetRect, endPos);
                Vector2 startTangent = startPos + Vector2.right * 60f;
                Vector2 endTangent = endPos + endDirection * 60f;

                if (!canvas.IsDraggingNode(sourceNode) && !canvas.IsDraggingNode(targetNode))
                {
                    (startTangent, endTangent) = canvas.OwnerGetConnectionTangents(
                        answer,
                        startPos,
                        endPos,
                        sourceRect,
                        targetRect,
                        sourceNode,
                        targetNode);
                }

                Color color = canvas.OwnerGetConnectionColor(sourceNode, targetNode, useSelectionHighlight);
                if (isImplicit)
                {
                    color = Color.Lerp(color, new Color(0.76f, 0.82f, 0.94f), 0.6f);
                }
                painter.strokeColor = color;
                painter.lineWidth = 3f;
                painter.BeginPath();
                painter.MoveTo(startPos);
                painter.BezierCurveTo(startTangent, endTangent, endPos);
                painter.Stroke();

                Vector2 direction = endPos - endTangent;
                Vector2 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
                Vector2 right = new Vector2(-normalizedDirection.y, normalizedDirection.x);
                Vector2 arrowBase = endPos - normalizedDirection * 18f;
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(endPos);
                painter.LineTo(arrowBase + right * 7.5f);
                painter.LineTo(arrowBase - right * 7.5f);
                painter.ClosePath();
                painter.Fill();
            }

            public bool IntersectsViewport(Rect viewport)
            {
                return TryGetViewportBounds(out Rect bounds) && bounds.Overlaps(viewport);
            }

            public bool TryGetViewportBounds(out Rect bounds)
            {
                if (!canvas.OwnerTryGetNodeRect(sourceNode, out Rect sourceRect) ||
                    !canvas.OwnerTryGetNodeRect(targetNode, out Rect targetRect))
                {
                    bounds = default;
                    return false;
                }

                const float RouteMargin = 100f;
                bounds = Rect.MinMaxRect(
                    Mathf.Min(sourceRect.xMin, targetRect.xMin) - RouteMargin,
                    Mathf.Min(sourceRect.yMin, targetRect.yMin) - RouteMargin,
                    Mathf.Max(sourceRect.xMax, targetRect.xMax) + RouteMargin,
                    Mathf.Max(sourceRect.yMax, targetRect.yMax) + RouteMargin);
                return true;
            }
        }

        /// <summary>Indexes visual connections by endpoint and workspace cells for repaint culling.</summary>
        private sealed class DialogToolkitConnectionCatalog
        {
            private const float CellSize = 800f;

            private readonly DialogToolkitCanvas canvas;
            private readonly List<DialogToolkitConnectionElement> connections = new();
            private readonly Dictionary<DialogNode, List<DialogToolkitConnectionElement>> connectionsByNode = new();
            private readonly Dictionary<long, HashSet<DialogToolkitConnectionElement>> connectionsByCell = new();
            private readonly Dictionary<DialogToolkitConnectionElement, List<long>> cellsByConnection = new();
            private readonly HashSet<DialogToolkitConnectionElement> querySet = new();

            public DialogToolkitConnectionCatalog(DialogToolkitCanvas canvas)
            {
                this.canvas = canvas;
            }

            public void Add(DialogToolkitConnectionElement connection)
            {
                connections.Add(connection);
                AddNodeConnection(connection.SourceNode, connection);
                if (connection.TargetNode != connection.SourceNode)
                {
                    AddNodeConnection(connection.TargetNode, connection);
                }

                UpdateConnection(connection);
            }

            public bool IsConnectedTo(DialogNode node) =>
                node != null && connectionsByNode.TryGetValue(node, out List<DialogToolkitConnectionElement> connected) && connected.Count > 0;

            public void UpdateConnectionsFor(DialogNode node)
            {
                if (node == null || !connectionsByNode.TryGetValue(node, out List<DialogToolkitConnectionElement> connected))
                {
                    return;
                }

                foreach (DialogToolkitConnectionElement connection in connected)
                {
                    UpdateConnection(connection);
                }
            }

            public void FillVisible(Rect viewport, List<DialogToolkitConnectionElement> results)
            {
                results.Clear();
                querySet.Clear();
                int minX = Mathf.FloorToInt(viewport.xMin / CellSize);
                int maxX = Mathf.FloorToInt(viewport.xMax / CellSize);
                int minY = Mathf.FloorToInt(viewport.yMin / CellSize);
                int maxY = Mathf.FloorToInt(viewport.yMax / CellSize);
                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        if (!connectionsByCell.TryGetValue(GetCellKey(x, y), out HashSet<DialogToolkitConnectionElement> cell))
                        {
                            continue;
                        }

                        foreach (DialogToolkitConnectionElement connection in cell)
                        {
                            querySet.Add(connection);
                        }
                    }
                }

                foreach (DialogToolkitConnectionElement connection in querySet)
                {
                    if (connection.IntersectsViewport(viewport))
                    {
                        results.Add(connection);
                    }
                }
            }

            private void AddNodeConnection(DialogNode node, DialogToolkitConnectionElement connection)
            {
                if (!connectionsByNode.TryGetValue(node, out List<DialogToolkitConnectionElement> connected))
                {
                    connected = new List<DialogToolkitConnectionElement>();
                    connectionsByNode[node] = connected;
                }

                connected.Add(connection);
            }

            private void UpdateConnection(DialogToolkitConnectionElement connection)
            {
                RemoveFromCells(connection);
                if (!connection.TryGetViewportBounds(out Rect bounds))
                {
                    return;
                }

                var membership = new List<long>();
                int minX = Mathf.FloorToInt(bounds.xMin / CellSize);
                int maxX = Mathf.FloorToInt(bounds.xMax / CellSize);
                int minY = Mathf.FloorToInt(bounds.yMin / CellSize);
                int maxY = Mathf.FloorToInt(bounds.yMax / CellSize);
                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        long key = GetCellKey(x, y);
                        if (!connectionsByCell.TryGetValue(key, out HashSet<DialogToolkitConnectionElement> cell))
                        {
                            cell = new HashSet<DialogToolkitConnectionElement>();
                            connectionsByCell[key] = cell;
                        }

                        cell.Add(connection);
                        membership.Add(key);
                    }
                }

                cellsByConnection[connection] = membership;
            }

            private void RemoveFromCells(DialogToolkitConnectionElement connection)
            {
                if (!cellsByConnection.TryGetValue(connection, out List<long> membership))
                {
                    return;
                }

                foreach (long key in membership)
                {
                    if (!connectionsByCell.TryGetValue(key, out HashSet<DialogToolkitConnectionElement> cell))
                    {
                        continue;
                    }

                    cell.Remove(connection);
                    if (cell.Count == 0)
                    {
                        connectionsByCell.Remove(key);
                    }
                }

                cellsByConnection.Remove(connection);
            }

            private static long GetCellKey(int x, int y) => ((long)x << 32) ^ (uint)y;
        }

        private sealed class DialogToolkitConnectionLayer : VisualElement
        {
            private const int RepaintIntervalMilliseconds = 16;
            private readonly DialogToolkitCanvas canvas;
            private readonly DialogToolkitConnectionCatalog connectionCatalog;
            private readonly List<DialogToolkitConnectionElement> visibleConnections = new();
            private readonly bool useSelectionHighlight;
            private DialogNode excludedNode;
            private DialogNode includedNode;
            private bool includesOnlyNode;
            private bool repaintScheduled;

            public DialogToolkitConnectionLayer(
                DialogToolkitCanvas canvas,
                bool useSelectionHighlight,
                DialogToolkitConnectionCatalog connectionCatalog)
            {
                this.canvas = canvas;
                this.useSelectionHighlight = useSelectionHighlight;
                this.connectionCatalog = connectionCatalog;
                name = "dialog-toolkit-connections";
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.top = 0f;
                style.width = WorkspaceWidth;
                style.height = WorkspaceHeight;
                generateVisualContent += DrawConnections;
            }

            public void RequestRepaint()
            {
                if (repaintScheduled)
                {
                    return;
                }

                repaintScheduled = true;
                schedule.Execute(() =>
                {
                    repaintScheduled = false;
                    MarkDirtyRepaint();
                }).ExecuteLater(RepaintIntervalMilliseconds);
            }

            public bool IsConnectedTo(DialogNode node)
            {
                return connectionCatalog.IsConnectedTo(node);
            }

            public void ExcludeNode(DialogNode node)
            {
                excludedNode = node;
                includedNode = null;
                includesOnlyNode = false;
            }

            public void IncludeNode(DialogNode node)
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
                Rect viewport = canvas.VisibleGraphRect;
                connectionCatalog.FillVisible(viewport, visibleConnections);
                foreach (DialogToolkitConnectionElement connection in visibleConnections)
                {
                    bool isConnectedToExcludedNode = excludedNode != null && connection.IsConnectedTo(excludedNode);
                    bool isConnectedToIncludedNode = includedNode != null && connection.IsConnectedTo(includedNode);
                    if (isConnectedToExcludedNode ||
                        (includesOnlyNode && (includedNode == null || !isConnectedToIncludedNode)))
                    {
                        continue;
                    }

                    if (!connection.IntersectsViewport(viewport))
                    {
                        continue;
                    }

                    connection.Draw(context.painter2D, useSelectionHighlight);
                }
            }
        }

        private sealed class DialogToolkitGridElement : VisualElement
        {
            private readonly DialogToolkitCanvas canvas;

            public DialogToolkitGridElement(DialogToolkitCanvas canvas)
            {
                this.canvas = canvas;
                name = "dialog-toolkit-grid";
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
                DrawGridLines(painter, visibleGraphRect, 40f, canvas.Owner.MinorGridColor, 1f);
                DrawGridLines(painter, visibleGraphRect, 200f, canvas.Owner.MajorGridColor, 1.4f);
            }

            private Rect GetVisibleGraphRect()
            {
                float zoom = Mathf.Max(canvas.Owner.zoom, 0.0001f);
                Vector2 pan = canvas.Owner.panOffset;
                Rect viewport = canvas.contentRect;
                return new Rect(-pan.x / zoom, -pan.y / zoom, viewport.width / zoom, viewport.height / zoom);
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
