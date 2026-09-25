using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Editor-only presentation state shared by graph workspaces. It intentionally contains
    /// no graph, asset, or domain knowledge.
    /// </summary>
    internal sealed class GraphEditorWorkspaceState
    {
        public float Zoom { get; set; } = 1f;
        public Vector2 PanOffset { get; set; } = Vector2.zero;
        public bool UseLightTheme { get; set; }
    }
}
