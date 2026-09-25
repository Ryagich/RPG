using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools
{
    /// <summary>Common canvas operations used by graph-editor window chrome.</summary>
    internal interface IGraphEditorCanvasView
    {
        void RebuildNow();
        void RequestRebuild();
        void RefreshGraphAppearance();
    }

    /// <summary>Builds and refreshes domain-neutral UI Toolkit chrome for graph editors.</summary>
    internal static class GraphEditorWindowUi
    {
        public static IMGUIContainer BuildRoot(
            VisualElement root,
            VisualElement canvas,
            string controlsName,
            float controlsWidth,
            Color backgroundColor,
            Action drawControls)
        {
            root.Clear();
            root.style.flexGrow = 1f;
            root.style.backgroundColor = backgroundColor;
            root.Add(canvas);

            var controls = new IMGUIContainer(drawControls)
            {
                name = controlsName
            };
            controls.style.position = Position.Absolute;
            controls.style.left = 0f;
            controls.style.top = 0f;
            controls.style.width = controlsWidth;
            controls.style.bottom = 0f;
            root.Add(controls);
            return controls;
        }

        public static void Refresh(
            VisualElement root,
            IGraphEditorCanvasView canvas,
            IMGUIContainer controls,
            Color backgroundColor,
            bool rebuild)
        {
            if (canvas == null)
            {
                return;
            }

            if (rebuild)
            {
                canvas.RequestRebuild();
            }
            else
            {
                canvas.RefreshGraphAppearance();
            }

            controls?.MarkDirtyRepaint();
            root.style.backgroundColor = backgroundColor;
        }
    }

    public abstract partial class GraphEditorWindowBase
    {
        protected readonly struct ThemedGuiScope : IDisposable
        {
            private readonly GraphEditorWindowBase owner;
            private readonly Color backgroundColor;
            private readonly Color contentColor;
            private readonly GUISkin skin;

            internal ThemedGuiScope(GraphEditorWindowBase owner)
            {
                this.owner = owner;
                backgroundColor = GUI.backgroundColor;
                contentColor = GUI.contentColor;
                skin = GUI.skin;
                owner.ApplyThemedGuiState();
            }

            public void Dispose()
            {
                owner.RestoreThemedGuiState();
                GUI.skin = skin;
                GUI.backgroundColor = backgroundColor;
                GUI.contentColor = contentColor;
            }
        }

        protected ThemedGuiScope BeginThemedGuiScope()
        {
            return new ThemedGuiScope(this);
        }

        protected abstract void ApplyThemedGuiState();
        protected abstract void RestoreThemedGuiState();
    }
}
