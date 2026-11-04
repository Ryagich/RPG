using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools
{
    /// <summary>Shared transient layout state for the collapsible graph-editor controls panel.</summary>
    internal sealed class GraphEditorOverlayPanelState
    {
        public const float CollapsedInputWidth = 30f;
        public bool IsExpanded { get; private set; } = true;

        public void SetExpanded(bool isExpanded, IMGUIContainer controls, float expandedWidth)
        {
            if (IsExpanded == isExpanded)
            {
                return;
            }

            IsExpanded = isExpanded;
            ApplyInputWidth(controls, expandedWidth);
            controls?.MarkDirtyRepaint();
        }

        public void ApplyInputWidth(IMGUIContainer controls, float expandedWidth)
        {
            if (controls != null)
            {
                controls.style.width = IsExpanded ? expandedWidth : CollapsedInputWidth;
            }
        }

        public Rect GetToggleRect(Rect panelRect, float toggleWidth, float toggleHeight, float collapsedLeftOffset)
        {
            float x = IsExpanded ? panelRect.xMax - toggleWidth * 0.5f : collapsedLeftOffset;
            float y = panelRect.y + panelRect.height * 0.5f - toggleHeight * 0.5f;
            return new Rect(x, y, toggleWidth, toggleHeight);
        }
    }
}
