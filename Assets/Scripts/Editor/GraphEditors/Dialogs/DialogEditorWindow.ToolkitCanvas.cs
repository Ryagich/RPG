using System.Collections.Generic;
using System.Linq;
using Dialogs.Graph.Model;
using EditorTools;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dialogs.Graph.Editor
{
    public partial class DialogEditorWindow
    {
        /// <summary>
        /// Presentation descriptor for authored answers and generated conversation-return
        /// links. The common canvas stays independent of dialogue semantics.
        /// </summary>
        private sealed class DialogCanvasConnection
        {
            public DialogCanvasConnection(DialogAnswer answer, bool isImplicit)
            {
                Answer = answer;
                IsImplicit = isImplicit;
            }

            public DialogAnswer Answer { get; }
            public bool IsImplicit { get; }
        }

        private DelegatingRetainedGraphCanvasAdapter<DialogNode, DialogCanvasConnection> CreateCanvasAdapter()
        {
            return new DelegatingRetainedGraphCanvasAdapter<DialogNode, DialogCanvasConnection>(
                "Create or load a dialog graph.", new Vector2(DialogNodeWidth, 220f),
                () => currentGraph != null, PrepareRetainedGraph, ClearRetainedNodeRects,
                GetRetainedGraphNodes, GetRetainedGraphConnections, GetRetainedNodePosition,
                SetRetainedNodePosition, SetRetainedNodeRect, GetRetainedNodeTitle,
                GetRetainedNodeTint, IsRetainedNodeTargetable, DrawRetainedNode,
                DeleteRetainedNode, SelectRetainedNode, ClearRetainedNodeSelection,
                TrySelectRetainedTarget, MarkRetainedNodePositionDirty, ClampRetainedGraphPan,
                DrawRetainedConnection, () => zoom, value => zoom = value, () => panOffset,
                value => panOffset = value, () => targetSelection.IsActive, () => CanvasBackgroundColor,
                () => PanelBackgroundColor, () => MinorGridColor, () => MajorGridColor,
                () => GetSelectionOverlayColor(false));
        }

        private void PrepareRetainedGraph()
        {
            if (currentGraph == null) return;
            if (graphStructureDirty)
            {
                CleanupGraph();
                graphStructureDirty = false;
            }
            if (graphIndex.IsDirty) RebuildGraphCaches();
            SynchronizeNodeRects();
        }

        private void ClearRetainedNodeRects() => nodeRects.Clear();

        private IEnumerable<DialogNode> GetRetainedGraphNodes() =>
            currentGraph?.Nodes ?? Enumerable.Empty<DialogNode>();

        private IEnumerable<RetainedGraphConnection<DialogNode, DialogCanvasConnection>> GetRetainedGraphConnections()
        {
            if (currentGraph == null) yield break;
            foreach (DialogNode sourceNode in currentGraph.Nodes)
            {
                if (sourceNode?.Phrase == null) continue;
                foreach (DialogAnswer answer in GetConnectionAnswers(sourceNode.Phrase))
                    if (answer?.NextPhrase != null && graphIndex.TryGetNode(answer.NextPhrase, out DialogNode targetNode))
                        yield return new RetainedGraphConnection<DialogNode, DialogCanvasConnection>(
                            sourceNode, targetNode, new DialogCanvasConnection(answer, false));

                foreach (DialogPhrase returnAction in GetImplicitConversationReturnActions(sourceNode.Phrase))
                    if (graphIndex.TryGetNode(returnAction, out DialogNode targetNode))
                        yield return new RetainedGraphConnection<DialogNode, DialogCanvasConnection>(
                            sourceNode, targetNode, new DialogCanvasConnection(null, true));
            }
        }

        private Vector2 GetRetainedNodePosition(DialogNode node) => node.Position;
        private void SetRetainedNodePosition(DialogNode node, Vector2 position) => node.Position = position;
        private void SetRetainedNodeRect(DialogNode node, Rect rect) => nodeRects[node] = rect;
        private string GetRetainedNodeTitle(DialogNode node) => GetNodeTitle(node);
        private Color GetRetainedNodeTint(DialogNode node) => GetNodeTint(node);
        private bool IsRetainedNodeTargetable(DialogNode node) => node?.Phrase != null;
        private void DrawRetainedNode(DialogNode node) => DrawToolkitNode(node);
        private void DeleteRetainedNode(DialogNode node) => DeleteNode(node, false);
        private void SelectRetainedNode(DialogNode node) => activeConnectionNode = node;
        private void ClearRetainedNodeSelection() => activeConnectionNode = null;
        private void MarkRetainedNodePositionDirty() => MarkNodePositionDirty();
        private void ClampRetainedGraphPan(float workspaceWidth, float workspaceHeight) =>
            ClampPanToWorkspace(workspaceWidth, workspaceHeight);

        private bool TrySelectRetainedTarget(DialogNode node)
        {
            if (!targetSelection.IsActive || targetSelection.PendingAnswer == null || node?.Phrase == null) return false;
            targetSelection.PendingAnswer.SetNextPhrase(node.Phrase);
            MarkDirty(targetSelection.SourcePhrase);
            CancelTargetSelection(false);
            return true;
        }

        private void DrawRetainedConnection(
            Painter2D painter, DialogCanvasConnection connection, DialogNode sourceNode, DialogNode targetNode,
            Rect sourceRect, Rect targetRect, bool isDragging)
        {
            (Vector2 startPosition, Vector2 endPosition) = GraphConnectionGeometry.GetConnectionAnchors(sourceRect, targetRect);
            Vector2 startTangent = startPosition + GraphConnectionGeometry.GetDirectionForRectPoint(sourceRect, startPosition) * 60f;
            Vector2 endTangent = endPosition + GraphConnectionGeometry.GetDirectionForRectPoint(targetRect, endPosition) * 60f;
            if (!isDragging)
                (startTangent, endTangent) = GetOrBuildConnectionTangents(
                    connection.Answer, startPosition, endPosition, sourceRect, targetRect, sourceNode, targetNode);

            Color color = GetConnectionColor(sourceNode, targetNode);
            if (connection.IsImplicit) color = Color.Lerp(color, new Color(0.76f, 0.82f, 0.94f), 0.6f);
            painter.strokeColor = color;
            painter.lineWidth = 3f;
            painter.BeginPath();
            painter.MoveTo(startPosition);
            painter.BezierCurveTo(startTangent, endTangent, endPosition);
            painter.Stroke();
            painter.fillColor = color;
            GraphConnectionDrawing.DrawArrow(painter, endPosition, endPosition - endTangent);
        }
    }
}
