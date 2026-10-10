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

        private void ClearRetainedNodeRects()
        {
            nodeRects.Clear();
            answerPickerAnchors.Clear();
        }

        private IEnumerable<DialogNode> GetRetainedGraphNodes() =>
            currentGraph?.Nodes ?? Enumerable.Empty<DialogNode>();

        private IEnumerable<RetainedGraphConnection<DialogNode, DialogCanvasConnection>> GetRetainedGraphConnections()
        {
            if (currentGraph == null) yield break;
            foreach (DialogNode sourceNode in currentGraph.Nodes)
            {
                foreach (RetainedGraphConnection<DialogNode, DialogCanvasConnection> connection in GetRetainedGraphConnectionsFrom(sourceNode))
                {
                    yield return connection;
                }
            }
        }

        private IEnumerable<RetainedGraphConnection<DialogNode, DialogCanvasConnection>> GetRetainedGraphConnectionsFrom(DialogNode sourceNode)
        {
            if (sourceNode?.Phrase == null) yield break;
            foreach (DialogAnswer answer in GetConnectionAnswers(sourceNode.Phrase))
                if (answer?.NextPhrase != null && graphIndex.TryGetNode(answer.NextPhrase, out DialogNode targetNode))
                    yield return new RetainedGraphConnection<DialogNode, DialogCanvasConnection>(
                        sourceNode, targetNode, new DialogCanvasConnection(answer, false));

            foreach (DialogPhrase returnAction in GetImplicitConversationReturnActions(sourceNode.Phrase))
                if (graphIndex.TryGetNode(returnAction, out DialogNode targetNode))
                    yield return new RetainedGraphConnection<DialogNode, DialogCanvasConnection>(
                        sourceNode, targetNode, new DialogCanvasConnection(null, true));
        }

        private Vector2 GetRetainedNodePosition(DialogNode node) => node.Position;
        private void SetRetainedNodePosition(DialogNode node, Vector2 position) => node.Position = position;
        private void SetRetainedNodeRect(DialogNode node, Rect rect) => nodeRects[node] = rect;
        private string GetRetainedNodeTitle(DialogNode node) => GetNodeTitle(node);
        private Color GetRetainedNodeTint(DialogNode node) => GetNodeTint(node);
        private bool IsRetainedNodeTargetable(DialogNode node) => IsTargetPhraseSelectable(node?.Phrase);
        private void DrawRetainedNode(DialogNode node) => DrawToolkitNode(node);
        private void DeleteRetainedNode(DialogNode node) => DeleteNode(node, false);
        private void SelectRetainedNode(DialogNode node) => activeConnectionNode = node;
        private void ClearRetainedNodeSelection() => activeConnectionNode = null;
        private void MarkRetainedNodePositionDirty() => MarkNodePositionDirty();
        private void ClampRetainedGraphPan(float workspaceWidth, float workspaceHeight) =>
            ClampPanToWorkspace(workspaceWidth, workspaceHeight);

        private bool TrySelectRetainedTarget(DialogNode node)
        {
            if (!IsTargetPhraseSelectable(node?.Phrase)) return false;
            targetSelection.PendingAnswer.SetNextPhrase(node.Phrase);
            MarkDirty(targetSelection.SourcePhrase);
            toolkitCanvas?.RebuildConnectionsGradually();
            CancelTargetSelection(false);
            return true;
        }

        private void DrawRetainedConnection(
            Painter2D painter, DialogCanvasConnection connection, DialogNode sourceNode, DialogNode targetNode,
            Rect sourceRect, Rect targetRect, bool isDragging)
        {
            (Vector2 defaultStart, Vector2 endPosition) = GraphConnectionGeometry.GetConnectionAnchors(sourceRect, targetRect);
            Vector2 startPosition = answerPickerAnchors.Resolve(connection.Answer, sourceRect, defaultStart);
            Vector2 startTangent = startPosition + GraphConnectionGeometry.GetDirectionForRectPoint(sourceRect, startPosition) * 60f;
            Vector2 endTangent = endPosition + GraphConnectionGeometry.GetDirectionForRectPoint(targetRect, endPosition) * 60f;
            if (!isDragging)
                (startTangent, endTangent) = GetOrBuildConnectionTangents(
                    connection.Answer, startPosition, endPosition, sourceRect, targetRect, sourceNode, targetNode);

            Color color = GetConnectionColor(sourceNode, targetNode);
            if (connection.IsImplicit) color = Color.Lerp(color, new Color(0.76f, 0.82f, 0.94f), 0.6f);
            GraphConnectionDrawing.DrawBezier(
                painter, startPosition, startTangent, endTangent, endPosition, color);
        }
    }
}
