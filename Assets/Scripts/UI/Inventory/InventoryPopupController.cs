using System;
using UI.Configs;
using UI.Pages;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using VContainer;
using Object = UnityEngine.Object;

namespace UI.Inventory
{
    public enum InventoryPopupMode
    {
        None,
        Hover,
        ContextMenu
    }

    /// <summary>
    /// Owns popup lifetime, raycast state and screen placement. Content and commands are supplied
    /// by the page, keeping this component independent from inventory gameplay rules.
    /// </summary>
    public sealed class InventoryPopupController
    {
        private readonly UIConfig uiConfig;
        private readonly IObjectResolver resolver;
        private readonly RectTransform parent;
        private readonly Func<Camera> eventCameraProvider;

        private RectTransform root;
        private RectTransform content;
        private InventoryPopupMode mode;

        public InventoryPopupController(UIConfig uiConfig, IObjectResolver resolver, RectTransform parent, Func<Camera> eventCameraProvider)
        {
            this.uiConfig = uiConfig ?? throw new ArgumentNullException(nameof(uiConfig));
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.eventCameraProvider = eventCameraProvider ?? throw new ArgumentNullException(nameof(eventCameraProvider));
        }

        public bool IsOpen => root != null;
        public InventoryPopupMode Mode => mode;

        public bool Open(Vector2 screenPoint, InventoryPopupMode popupMode, string name, Action<RectTransform, RectTransform> buildContent)
        {
            Close();
            var interactive = popupMode == InventoryPopupMode.ContextMenu;
            root = PageUiUtilities.CreatePopupRoot(parent, uiConfig, resolver, !interactive, name);
            if (root == null)
            {
                return false;
            }

            PageUiUtilities.SetPopupRaycastState(root, interactive);
            content = PageUiUtilities.CreatePopupContent(root, uiConfig, resolver, interactive);
            if (content == null)
            {
                Close();
                return false;
            }

            buildContent?.Invoke(root, content);
            PageUiUtilities.RecalculatePopupLayout(root, content);
            UpdatePosition(screenPoint);
            mode = popupMode;
            return true;
        }

        public void UpdatePosition(Vector2 screenPoint)
        {
            if (root != null)
            {
                PageUiUtilities.UpdatePopupPosition(root, parent, eventCameraProvider(), screenPoint);
            }
        }

        public void CreateButton(LocalizedString label, UnityAction onClick, bool interactable = true)
        {
            PageUiUtilities.CreatePopupButton(content, uiConfig, resolver, label, onClick, interactable);
        }

        public void Close()
        {
            if (root != null)
            {
                Object.Destroy(root.gameObject);
            }

            root = null;
            content = null;
            mode = InventoryPopupMode.None;
        }

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            return root != null && RectTransformUtility.RectangleContainsScreenPoint(root, screenPoint, eventCameraProvider());
        }
    }
}
