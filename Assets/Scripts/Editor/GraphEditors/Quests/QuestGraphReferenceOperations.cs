using System.Collections.Generic;
using System.Linq;
using EditorTools;
using Quests.Graph;
using Quests.Graph.Model;

namespace Quests.Editor
{
    /// <summary>Maintains reference and ownership invariants of quest graph assets.</summary>
    internal static class QuestGraphReferenceOperations
    {
        public static void RemoveIncomingReferences(QuestGraph graph, QuestNodeData target) =>
            UpdateIncomingReferences(graph, target, null);

        public static void ReplaceIncomingReferences(QuestGraph graph, QuestNodeData oldNode, QuestNodeData newNode) =>
            UpdateIncomingReferences(graph, oldNode, newNode);

        public static void DeleteOwnedTransitions(QuestNodeData nodeData)
        {
            if (nodeData == null) return;
            EnsureTransitions(nodeData);
            foreach (QuestTransition transition in nodeData.Transitions.ToList())
                if (transition != null) GraphEditorAssetService.DeleteAsset(transition, "Delete quest transition");
            GraphEditorAssetService.MarkDirty(nodeData, "Delete owned quest transitions");
            nodeData.Transitions.Clear();
        }

        public static void ReleaseOwnershipIfUnused(QuestGraph graph, QuestNodeData nodeData)
        {
            if (graph == null || nodeData == null || graph.Nodes.Any(node => node?.NodeData == nodeData)) return;
            nodeData.ClearOwnerGraph(graph);
            GraphEditorAssetService.MarkDirty(nodeData, "Release quest node ownership");
        }

        public static void ClaimUnownedNodes(QuestGraph graph)
        {
            if (graph?.Nodes == null) return;
            foreach (QuestNode node in graph.Nodes)
                if (node?.NodeData != null && node.NodeData.OwnerGraph == null)
                {
                    node.NodeData.SetOwnerGraph(graph);
                    GraphEditorAssetService.MarkDirty(node.NodeData, "Claim quest node ownership");
                }
        }

        private static void UpdateIncomingReferences(QuestGraph graph, QuestNodeData oldNode, QuestNodeData newNode)
        {
            if (graph?.Nodes == null || oldNode == null || oldNode == newNode) return;
            foreach (QuestNode node in graph.Nodes)
            {
                if (node?.NodeData == null) continue;
                EnsureTransitions(node.NodeData);
                foreach (QuestTransition transition in node.NodeData.Transitions)
                    if (transition != null && transition.TargetNode == oldNode)
                    {
                        transition.SetTargetNode(newNode);
                        GraphEditorAssetService.MarkDirty(transition, "Update quest node reference");
                    }
            }
        }

        private static void EnsureTransitions(QuestNodeData nodeData) => nodeData.Transitions ??= new List<QuestTransition>();
    }
}
