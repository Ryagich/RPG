using UnityEngine;

namespace EditorTools
{
    /// <summary>Low-level, domain-free building blocks for graph-editor IMGUI skins.</summary>
    internal static class GraphEditorGuiStyleUtility
    {
        public static void ApplyState(GUIStyleState state, Texture2D background, Color textColor)
        {
            state.background = background;
            state.scaledBackgrounds = new[] { background };
            state.textColor = textColor;
        }

        public static GUIStyle[] AppendOrReplace(GUIStyle[] styles, GUIStyle style)
        {
            if (styles == null || styles.Length == 0)
            {
                return new[] { style };
            }

            for (int index = 0; index < styles.Length; index++)
            {
                if (styles[index] != null && styles[index].name == style.name)
                {
                    styles[index] = style;
                    return styles;
                }
            }

            GUIStyle[] result = new GUIStyle[styles.Length + 1];
            styles.CopyTo(result, 0);
            result[styles.Length] = style;
            return result;
        }

        public static Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
