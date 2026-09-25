using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Temporarily recolors Unity's shared IMGUI styles and restores every state afterwards.
    /// Editor windows must not retain mutations of <see cref="EditorStyles"/> between draws.
    /// </summary>
    internal sealed class GraphEditorStyleTextOverrides
    {
        private readonly List<StyleSnapshot> snapshots = new();

        public void ApplyForLightTheme()
        {
            Restore();
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Apply(Color.black,
                EditorStyles.label, EditorStyles.boldLabel, EditorStyles.miniLabel,
                EditorStyles.miniBoldLabel, EditorStyles.wordWrappedLabel,
                EditorStyles.wordWrappedMiniLabel, EditorStyles.centeredGreyMiniLabel,
                EditorStyles.foldout, EditorStyles.toggle, EditorStyles.textField,
                EditorStyles.textArea, EditorStyles.popup, EditorStyles.miniButton,
                EditorStyles.miniButtonLeft, EditorStyles.miniButtonMid,
                EditorStyles.miniButtonRight, EditorStyles.objectField,
                EditorStyles.objectFieldThumb, EditorStyles.helpBox);
        }

        public void Restore()
        {
            for (int index = snapshots.Count - 1; index >= 0; index--)
            {
                snapshots[index].Restore();
            }

            snapshots.Clear();
        }

        private void Apply(Color color, params GUIStyle[] styles)
        {
            foreach (GUIStyle style in styles)
            {
                if (style == null)
                {
                    continue;
                }

                snapshots.Add(new StyleSnapshot(style));
                SetTextColor(style, color);
            }
        }

        private static void SetTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private readonly struct StyleSnapshot
        {
            private readonly GUIStyle style;
            private readonly Color normal;
            private readonly Color hover;
            private readonly Color active;
            private readonly Color focused;
            private readonly Color onNormal;
            private readonly Color onHover;
            private readonly Color onActive;
            private readonly Color onFocused;

            public StyleSnapshot(GUIStyle style)
            {
                this.style = style;
                normal = style.normal.textColor;
                hover = style.hover.textColor;
                active = style.active.textColor;
                focused = style.focused.textColor;
                onNormal = style.onNormal.textColor;
                onHover = style.onHover.textColor;
                onActive = style.onActive.textColor;
                onFocused = style.onFocused.textColor;
            }

            public void Restore()
            {
                style.normal.textColor = normal;
                style.hover.textColor = hover;
                style.active.textColor = active;
                style.focused.textColor = focused;
                style.onNormal.textColor = onNormal;
                style.onHover.textColor = onHover;
                style.onActive.textColor = onActive;
                style.onFocused.textColor = onFocused;
            }
        }
    }
}
