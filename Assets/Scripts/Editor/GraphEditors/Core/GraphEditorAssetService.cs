using System.IO;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Owns the editor-side AssetDatabase protocol shared by graph editors.
    /// Domain editors decide what an asset means; this service only validates paths,
    /// creates/loads assets and keeps Unity's selection and persistence lifecycle coherent.
    /// </summary>
    internal static class GraphEditorAssetService
    {
        public static bool TryPickAssetsFolder(string title, string currentPath, out string assetPath)
        {
            string selectedPath = EditorUtility.OpenFolderPanel(title, Application.dataPath, currentPath ?? string.Empty);
            if (string.IsNullOrEmpty(selectedPath))
            {
                assetPath = currentPath;
                return false;
            }

            selectedPath = selectedPath.Replace('\\', '/');
            string normalizedAssetsPath = Application.dataPath.Replace('\\', '/');
            if (!selectedPath.StartsWith(normalizedAssetsPath))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder inside your Assets directory.", "OK");
                assetPath = currentPath;
                return false;
            }

            assetPath = "Assets" + selectedPath.Substring(normalizedAssetsPath.Length);
            return true;
        }

        public static bool EnsureFolderExists(string folderPath, string emptyPathMessage)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                EditorUtility.DisplayDialog("Path not set", emptyPathMessage, "OK");
                return false;
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            return true;
        }

        public static bool TryLoadAsset<TAsset>(string title, out TAsset asset)
            where TAsset : Object
        {
            string path = EditorUtility.OpenFilePanel(title, Application.dataPath, "asset");
            if (string.IsNullOrEmpty(path))
            {
                asset = null;
                return false;
            }

            string normalizedAssetsPath = Application.dataPath.Replace('\\', '/');
            path = path.Replace('\\', '/');
            if (!path.StartsWith(normalizedAssetsPath))
            {
                asset = null;
                return false;
            }

            asset = AssetDatabase.LoadAssetAtPath<TAsset>("Assets" + path.Substring(normalizedAssetsPath.Length));
            if (asset != null)
            {
                return true;
            }

            EditorUtility.DisplayDialog("Invalid Asset", $"Selected asset is not a {typeof(TAsset).Name}.", "OK");
            return false;
        }

        public static string CreateAsset<TAsset>(TAsset asset, string folderPath, string fileName, string undoLabel)
            where TAsset : Object
        {
            if (asset == null)
            {
                throw new System.ArgumentNullException(nameof(asset));
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, fileName));
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            Undo.RegisterCreatedObjectUndo(asset, undoLabel);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
            return assetPath;
        }

        public static void CreateProjectAsset<TAsset>(TAsset asset, string defaultFileName, string undoLabel)
            where TAsset : Object
        {
            if (asset == null)
            {
                throw new System.ArgumentNullException(nameof(asset));
            }

            Undo.RegisterCreatedObjectUndo(asset, undoLabel);
            ProjectWindowUtil.CreateAsset(asset, defaultFileName);
        }

        public static void MarkDirty(Object asset, string undoLabel)
        {
            if (asset == null)
            {
                return;
            }

            Undo.RecordObject(asset, undoLabel);
            EditorUtility.SetDirty(asset);
        }

        public static void DeleteAsset(Object asset, string undoLabel)
        {
            if (asset == null || !AssetDatabase.Contains(asset))
            {
                return;
            }

            Undo.DestroyObjectImmediate(asset);
            AssetDatabase.SaveAssets();
        }

        public static void FlushChanges()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
