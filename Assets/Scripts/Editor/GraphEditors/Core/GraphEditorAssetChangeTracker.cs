using System;
using System.Collections.Generic;
using UnityEditor;

namespace EditorTools
{
    /// <summary>
    /// Coalesces AssetDatabase changes and publishes them on the next editor tick.
    /// Graph editors use the changed paths to invalidate only the graph they display,
    /// rather than rebuilding on Unity's project-wide change notification.
    /// </summary>
    internal sealed class GraphEditorAssetChangeTracker : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingAssetPaths = new(StringComparer.OrdinalIgnoreCase);
        private static bool publishScheduled;

        internal static event Action<IReadOnlyCollection<string>> AssetsChanged;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            AddPaths(importedAssets);
            AddPaths(deletedAssets);
            AddPaths(movedAssets);
            AddPaths(movedFromAssetPaths);

            if (publishScheduled || PendingAssetPaths.Count == 0)
            {
                return;
            }

            publishScheduled = true;
            EditorApplication.delayCall += PublishPendingChanges;
        }

        internal static bool IsPathInsideFolder(string assetPath, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(folderPath))
            {
                return false;
            }

            string normalizedPath = NormalizePath(assetPath);
            string normalizedFolder = NormalizePath(folderPath).TrimEnd('/');
            return string.Equals(normalizedPath, normalizedFolder, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(normalizedFolder + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddPaths(IEnumerable<string> assetPaths)
        {
            if (assetPaths == null)
            {
                return;
            }

            foreach (string assetPath in assetPaths)
            {
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    PendingAssetPaths.Add(NormalizePath(assetPath));
                }
            }
        }

        private static void PublishPendingChanges()
        {
            publishScheduled = false;
            if (PendingAssetPaths.Count == 0)
            {
                return;
            }

            string[] changedPaths = new string[PendingAssetPaths.Count];
            PendingAssetPaths.CopyTo(changedPaths);
            PendingAssetPaths.Clear();
            AssetsChanged?.Invoke(changedPaths);
        }

        private static string NormalizePath(string assetPath) => assetPath.Replace('\\', '/');
    }
}
