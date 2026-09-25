using System.Collections.Generic;
using System.Linq;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEditor;

namespace Quests.Editor
{
    /// <summary>Queries and repairs graph-container invariants without owning editor UI state.</summary>
    internal static class QuestGraphStructureOperations
    {
        public static bool EnsureNodes(QuestGraph graph)
        {
            if (graph == null || graph.Nodes != null) return false;
            graph.Nodes = new List<QuestNode>();
            return true;
        }

        public static bool RemoveMissingNodes(QuestGraph graph)
        {
            if (graph?.Nodes == null) return false;
            bool changed = false;
            for (int index = graph.Nodes.Count - 1; index >= 0; index--)
            {
                QuestNode node = graph.Nodes[index];
                if (node == null || node.NodeData == null || !AssetDatabase.Contains(node.NodeData))
                {
                    graph.Nodes.RemoveAt(index);
                    changed = true;
                }
            }

            return changed;
        }

        public static QuestNodeData GetStartNode(QuestGraph graph) =>
            graph?.Nodes != null && graph.Nodes.Count > 0 ? graph.Nodes[0]?.NodeData : null;

        public static bool ContainsNode(QuestGraph graph, QuestNodeData nodeData) =>
            graph?.Nodes != null && graph.Nodes.Any(node => node?.NodeData == nodeData);

        public static bool IsStartNode(QuestGraph graph, QuestNode node) =>
            graph?.Nodes != null && graph.Nodes.Count > 0 && graph.Nodes[0] == node && node?.NodeData != null;

        public static bool IsOrphanNode(QuestGraph graph, QuestNodeData nodeData)
        {
            if (graph?.Nodes == null || nodeData == null || GetStartNode(graph) == nodeData) return false;
            return !graph.Nodes.Where(node => node?.NodeData != null)
                .SelectMany(node => node.NodeData.Transitions ?? new List<QuestTransition>())
                .Any(transition => transition != null && transition.TargetNode == nodeData);
        }
    }
}
