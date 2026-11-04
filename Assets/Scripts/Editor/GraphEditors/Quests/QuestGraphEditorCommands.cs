using EditorTools;
using Quests.Editor;
using Quests.Graph.Model;
using UnityEngine;

namespace Quests.Graph.Editor
{
    /// <summary>Creates quest graph assets and their authored node/transition assets.</summary>
    internal static class QuestGraphEditorCommands
    {
        public static QuestGraph CreateGraph()
        {
            var graph = ScriptableObject.CreateInstance<QuestGraph>();
            GraphEditorAssetService.CreateProjectAsset(graph, "NewQuestGraph.asset", "Create quest graph");
            return graph;
        }

        public static bool TryCreateNode(QuestGraph graph, string folderPath, Vector2 position, out QuestNode node)
        {
            node = null;
            if (graph == null || !GraphEditorAssetService.EnsureFolderExists(folderPath, "Please specify the folder for saving quest nodes."))
            {
                return false;
            }

            QuestGraphStructureOperations.EnsureNodes(graph);
            string fileName = $"QuestNode_{graph.Nodes.Count}.asset";
            var nodeData = ScriptableObject.CreateInstance<QuestNodeData>();
            string defaultTitle = System.IO.Path.GetFileNameWithoutExtension(fileName);
            nodeData.name = defaultTitle;
            nodeData.SetEditorTitle(defaultTitle);
            nodeData.SetOwnerGraph(graph);
            string path = GraphEditorAssetService.CreateAsset(nodeData, folderPath, fileName, "Create quest node");
            nodeData.SetEditorTitle(System.IO.Path.GetFileNameWithoutExtension(path));

            node = new QuestNode(nodeData) { Position = position };
            GraphEditorAssetService.MarkDirty(graph, "Add quest node");
            graph.Nodes.Add(node);
            GraphEditorAssetService.MarkDirty(nodeData, "Create quest node");
            return true;
        }

        public static bool TryCreateTransition(QuestNodeData nodeData, string folderPath)
        {
            if (nodeData == null || !GraphEditorAssetService.EnsureFolderExists(folderPath, "Please specify the folder for saving quest transitions."))
            {
                return false;
            }

            nodeData.Transitions ??= new System.Collections.Generic.List<QuestTransition>();
            var transition = ScriptableObject.CreateInstance<QuestTransition>();
            GraphEditorAssetService.CreateAsset(
                transition,
                folderPath,
                $"{nodeData.name}_Transition_{nodeData.Transitions.Count}.asset",
                "Create quest transition");
            GraphEditorAssetService.MarkDirty(nodeData, "Add quest transition");
            nodeData.Transitions.Add(transition);
            return true;
        }
    }
}
