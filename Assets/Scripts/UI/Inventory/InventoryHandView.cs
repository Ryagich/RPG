using System;
using Inventory.Inventories;
using Inventory.Item;
using TMPro;
using UI.Pages;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI.Inventory
{
    /// <summary>Owns the presentation-only positioning of the item currently attached to the cursor.</summary>
    public sealed class InventoryHandView
    {
        private readonly RectTransform canvasRect;
        private readonly Func<Camera> eventCameraProvider;
        private readonly TMP_FontAsset stackCountFont;
        private RectTransform handRect;

        public InventoryHandView(RectTransform canvasRect, TMP_FontAsset stackCountFont, Func<Camera> eventCameraProvider)
        {
            this.canvasRect = canvasRect ?? throw new ArgumentNullException(nameof(canvasRect));
            this.stackCountFont = stackCountFont;
            this.eventCameraProvider = eventCameraProvider ?? throw new ArgumentNullException(nameof(eventCameraProvider));
        }

        public bool IsVisible => handRect != null;

        public void Render(ItemStack stack, Vector2? stackAnchorAreaSize)
        {
            handRect = PageUiUtilities.DrawHandSlot(handRect, canvasRect, stack, stackCountFont, stackAnchorAreaSize);
        }

        public void SetVisible(bool visible)
        {
            if (handRect != null && handRect.gameObject.activeSelf != visible)
            {
                handRect.gameObject.SetActive(visible);
            }
        }

        public void UpdatePosition(Vector2 screenPoint, Vector2 grabOffset, Func<RectTransform, Vector2, Vector2?> tryGetSnappedPosition)
        {
            if (handRect == null)
            {
                return;
            }

            var snappedPosition = tryGetSnappedPosition?.Invoke(handRect.parent as RectTransform, screenPoint);
            if (snappedPosition.HasValue)
            {
                handRect.anchoredPosition = snappedPosition.Value;
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCameraProvider(), out var localPoint))
            {
                return;
            }

            handRect.anchoredPosition = localPoint - grabOffset;
        }

        public void SetSize(ItemStack stack, Vector2 fallbackSize)
        {
            var size = stack?.ItemConfig == null
                ? fallbackSize
                : new Vector2(stack.Size.x * fallbackSize.x, stack.Size.y * fallbackSize.y);
            handRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            handRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }
    }

    public delegate bool InventoryHandSnapResolver(
        Vector2 screenPoint,
        Camera eventCamera,
        RectTransform dragParentRect,
        out Vector2 snappedPosition);

    /// <summary>
    /// Coordinates the hand view with the player's current hand stack. It owns pointer lookup and
    /// source-grid sizing; pages provide only their own valid snap zones and movement permissions.
    /// </summary>
    public sealed class InventoryHandPresenter
    {
        private readonly PlayerInventory playerInventory;
        private readonly InventoryHandView handView;
        private readonly Func<IInventory, InventoryView> inventoryViewProvider;
        private readonly Func<Camera> eventCameraProvider;

        public InventoryHandPresenter(
            PlayerInventory playerInventory,
            InventoryHandView handView,
            Func<IInventory, InventoryView> inventoryViewProvider,
            Func<Camera> eventCameraProvider)
        {
            this.playerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            this.handView = handView ?? throw new ArgumentNullException(nameof(handView));
            this.inventoryViewProvider = inventoryViewProvider ?? throw new ArgumentNullException(nameof(inventoryViewProvider));
            this.eventCameraProvider = eventCameraProvider ?? throw new ArgumentNullException(nameof(eventCameraProvider));
        }

        public bool IsVisible => handView.IsVisible;

        public void RenderAndPosition(Vector2 grabOffset, InventoryHandSnapResolver snapResolver)
        {
            var handStack = playerInventory.HandSlot.Value?.ItemStack;
            handView.Render(handStack, GetSourceGridItemSize(handStack));
            UpdatePosition(grabOffset, snapResolver);
        }

        public void UpdatePosition(Vector2 grabOffset, InventoryHandSnapResolver snapResolver)
        {
            if (!handView.IsVisible || Pointer.current == null)
            {
                return;
            }

            var screenPoint = Pointer.current.position.ReadValue();
            var eventCamera = eventCameraProvider();
            handView.UpdatePosition(screenPoint, grabOffset, (dragParentRect, pointerPosition) =>
                snapResolver != null && snapResolver(pointerPosition, eventCamera, dragParentRect, out var snappedPosition)
                    ? snappedPosition
                    : (Vector2?)null);
        }

        private Vector2? GetSourceGridItemSize(ItemStack handStack)
        {
            if (handStack?.ItemConfig == null)
            {
                return null;
            }

            var sourceView = inventoryViewProvider(playerInventory.HandSourceInventory.Value);
            var gridLayout = sourceView != null ? sourceView.ContentForTiles.GetComponent<UnityEngine.UI.GridLayoutGroup>() : null;
            return gridLayout == null ? null : PageUiUtilities.GetItemGrabSize(gridLayout, handStack.Size);
        }
    }
}
