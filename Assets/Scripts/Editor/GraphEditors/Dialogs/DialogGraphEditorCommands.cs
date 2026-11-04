using Dialogs.Graph.Model;
using EditorTools;
using UnityEngine;

namespace Dialogs.Graph.Editor
{
    /// <summary>Creates dialogue graph and phrase assets without owning editor-window state.</summary>
    internal static class DialogGraphEditorCommands
    {
        public static bool TryCreateGraph(string folderPath, out DialogGraph graph)
        {
            graph = null;
            if (!GraphEditorAssetService.EnsureFolderExists(folderPath, "Please specify the folder for saving dialogs."))
            {
                return false;
            }

            graph = ScriptableObject.CreateInstance<DialogGraph>();
            GraphEditorAssetService.CreateAsset(graph, folderPath, "DialogGraph.asset", "Create dialog graph");
            return true;
        }

        public static bool TryCreatePhrase(DialogGraph graph, string folderPath, Vector2 position, out DialogNode node)
        {
            node = null;
            if (graph == null || !GraphEditorAssetService.EnsureFolderExists(folderPath, "Please specify the folder for saving phrases."))
            {
                return false;
            }

            DialogGraphStructureOperations.EnsureNodes(graph);
            var phrase = ScriptableObject.CreateInstance<DialogPhrase>();
            GraphEditorAssetService.CreateAsset(
                phrase,
                folderPath,
                $"DialogPhrase_{graph.Nodes.Count}.asset",
                "Create dialog phrase");
            node = new DialogNode(phrase) { Position = position };
            GraphEditorAssetService.MarkDirty(graph, "Add dialog phrase");
            graph.Nodes.Add(node);
            return true;
        }
    }
}
