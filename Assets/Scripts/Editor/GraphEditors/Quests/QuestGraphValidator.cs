using System.Collections.Generic;
using System.Linq;
using EditorTools;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEditor;

namespace Quests.Editor
{
    /// <summary>Read-only validation of quest graph links and entry reachability.</summary>
    internal static class QuestGraphValidator
    {
        public static IReadOnlyList<GraphEditorValidationIssue> Validate(QuestGraph graph)
        {
            var issues = new List<GraphEditorValidationIssue>();
            if (graph?.Nodes == null || graph.Nodes.Count == 0)
            {
                issues.Add(Error("Graph has no start node."));
                return issues;
            }

            var nodes = new HashSet<QuestNodeData>();
            foreach (QuestNode node in graph.Nodes)
            {
                if (node?.NodeData == null || !AssetDatabase.Contains(node.NodeData))
                    issues.Add(Error("Graph contains a node with a missing QuestNodeData asset."));
                else if (!nodes.Add(node.NodeData))
                    issues.Add(Error($"Node \"{node.NodeData.name}\" is assigned more than once."));
            }

            QuestNodeData entry = QuestGraphStructureOperations.GetStartNode(graph);
            if (entry == null || !nodes.Contains(entry))
            {
                issues.Add(Error("The first graph node must reference a valid start node."));
                return issues;
            }

            foreach (QuestNodeData node in nodes)
                foreach (QuestTransition transition in node.Transitions ?? Enumerable.Empty<QuestTransition>())
                    if (transition == null || !AssetDatabase.Contains(transition))
                        issues.Add(Error($"Node \"{node.name}\" contains a missing transition asset."));
                    else if (transition.TargetNode == null)
                        issues.Add(Error($"Transition \"{transition.name}\" has no target node."));
                    else if (!nodes.Contains(transition.TargetNode))
                        issues.Add(Error($"Transition \"{transition.name}\" targets a node outside this graph."));

            var reachable = new HashSet<QuestNodeData> { entry };
            var pending = new Queue<QuestNodeData>();
            pending.Enqueue(entry);
            while (pending.Count > 0)
                foreach (QuestTransition transition in pending.Dequeue().Transitions ?? Enumerable.Empty<QuestTransition>())
                    if (transition?.TargetNode != null && nodes.Contains(transition.TargetNode) && reachable.Add(transition.TargetNode))
                        pending.Enqueue(transition.TargetNode);

            foreach (QuestNodeData node in nodes.Where(node => !reachable.Contains(node)))
                issues.Add(new GraphEditorValidationIssue(GraphEditorValidationSeverity.Warning, $"Node \"{node.name}\" is unreachable from the start node."));
            return issues;
        }

        private static GraphEditorValidationIssue Error(string message) => new(GraphEditorValidationSeverity.Error, message);
    }
}
