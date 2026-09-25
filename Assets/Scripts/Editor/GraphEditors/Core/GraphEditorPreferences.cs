using UnityEditor;

namespace EditorTools
{
    /// <summary>Typed access to editor-local graph workspace preferences.</summary>
    internal static class GraphEditorPreferences
    {
        public static string LoadFolder(string key, string defaultPath) => EditorPrefs.GetString(key, defaultPath);
        public static void SaveFolder(string key, string path) => EditorPrefs.SetString(key, path);
        public static bool LoadTheme(string key) => EditorPrefs.GetBool(key, false);
        public static void SaveTheme(string key, bool useLightTheme) => EditorPrefs.SetBool(key, useLightTheme);
    }
}
