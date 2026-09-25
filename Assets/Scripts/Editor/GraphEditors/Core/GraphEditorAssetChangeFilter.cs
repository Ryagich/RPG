using System.Collections.Generic;

namespace EditorTools
{
    /// <summary>
    /// Determines whether a batch of AssetDatabase changes can affect one graph editor.
    /// The filter is intentionally domain-agnostic: callers provide the graph asset and
    /// the folders that own its child assets.
    /// </summary>
    internal static class GraphEditorAssetChangeFilter
    {
        internal static bool HasRelevantChange(
            IReadOnlyCollection<string> changedAssetPaths,
            string graphAssetPath,
            params string[] ownedAssetFolderPaths)
        {
            if (changedAssetPaths == null || changedAssetPaths.Count == 0)
            {
                return false;
            }

            foreach (string assetPath in changedAssetPaths)
            {
                if (IsGraphAsset(assetPath, graphAssetPath) || IsInsideOwnedFolder(assetPath, ownedAssetFolderPaths))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGraphAsset(string assetPath, string graphAssetPath)
        {
            return !string.IsNullOrWhiteSpace(graphAssetPath) &&
                   string.Equals(assetPath, graphAssetPath, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInsideOwnedFolder(string assetPath, IEnumerable<string> ownedAssetFolderPaths)
        {
            if (ownedAssetFolderPaths == null)
            {
                return false;
            }

            foreach (string folderPath in ownedAssetFolderPaths)
            {
                if (GraphEditorAssetChangeTracker.IsPathInsideFolder(assetPath, folderPath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
