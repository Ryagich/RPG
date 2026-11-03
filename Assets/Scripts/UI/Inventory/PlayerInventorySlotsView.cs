using System;
using System.Collections.Generic;
using Input;
using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using TMPro;
using UI.Pages;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Inventory
{
    /// <summary>
    /// Renders the player's equipment and quick slots. Pages may observe rendered equipment slots
    /// for page-specific UI affordances, but this view contains no item-transfer policy.
    /// </summary>
    public sealed class PlayerInventorySlotsView
    {
        private readonly SlotsViewContainer slots;
        private readonly PlayerInventory inventory;
        private readonly InputConfig inputConfig;
        private readonly TMP_FontAsset stackCountFont;
        private readonly ICollection<RectTransform> itemRects;
        private readonly ICollection<RectTransform> itemGrabRects;

        public PlayerInventorySlotsView
            (
                SlotsViewContainer slots,
                PlayerInventory inventory,
                InputConfig inputConfig,
                TMP_FontAsset stackCountFont,
                ICollection<RectTransform> itemRects,
                ICollection<RectTransform> itemGrabRects
            )
        {
            this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.inputConfig = inputConfig ?? throw new ArgumentNullException(nameof(inputConfig));
            this.stackCountFont = stackCountFont;
            this.itemRects = itemRects ?? throw new ArgumentNullException(nameof(itemRects));
            this.itemGrabRects = itemGrabRects ?? throw new ArgumentNullException(nameof(itemGrabRects));
        }

        public void Render(Action<RectTransform, SlotModel> onEquipmentSlotRendered = null)
        {
            RenderEquipmentSlot(slots.HeadSlot, inventory.HelmSlot, onEquipmentSlotRendered);
            RenderEquipmentSlot(slots.FaceSlot, inventory.FaceSlot, onEquipmentSlotRendered);
            RenderEquipmentSlot(slots.BodySlot, inventory.BodySlot, onEquipmentSlotRendered);
            RenderEquipmentSlot(slots.HandsSlot, inventory.HandsSlot, onEquipmentSlotRendered);
            RenderEquipmentSlot(slots.ArmsSlot, inventory.ArmsSlot, onEquipmentSlotRendered);
            RenderEquipmentSlot(slots.LegsSlot, inventory.LegsSlot, onEquipmentSlotRendered);
            RenderEquipmentSlot(slots.HipsSlot, inventory.HipsSlot, onEquipmentSlotRendered);
            RenderEquipmentSlot(slots.BackpackSlot, inventory.BackpackSlot, onEquipmentSlotRendered);
            RenderEquipmentSlot(slots.LeftWeaponSlot, inventory.LeftWeaponSlot, onEquipmentSlotRendered);
            RenderEquipmentSlot(slots.RightWeaponSlot, inventory.RightWeaponSlot, onEquipmentSlotRendered);
            RenderFastSlot(slots.FastSlot1, inventory.FastSlot1);
            RenderFastSlot(slots.FastSlot2, inventory.FastSlot2);
            RenderFastSlot(slots.FastSlot3, inventory.FastSlot3);
            RenderFastSlot(slots.FastSlot4, inventory.FastSlot4);
        }

        private void RenderEquipmentSlot(SlotView slotView, SlotModel slotModel, Action<RectTransform, SlotModel> onRendered)
        {
            PageUiUtilities.SetSlotBlockedState(slotView, slotModel == inventory.FaceSlot && inventory.IsFaceSlotBlocked);
            PageUiUtilities.DrawSlotItem(slotView, slotModel, itemRects, itemGrabRects, stackCountFont);
            if (slotView == null || slotModel?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            onRendered?.Invoke(slotView.GetComponent<RectTransform>(), slotModel);
        }

        private void RenderFastSlot(SlotView slotView, FastSlotModel fastSlotModel)
        {
            PageUiUtilities.DrawFastSlotItem
                (
                    slotView,
                    fastSlotModel,
                    inventory.HasAnyInventoryItem(fastSlotModel?.ItemConfig),
                    inputConfig,
                    stackCountFont
                );
        }
    }

    /// <summary>
    /// Renders item images and their grab areas in a tiled inventory. Item commands and popup
    /// semantics are supplied by the owning page through the rendered-item callback.
    /// </summary>
    public sealed class InventoryItemsView
    {
        private readonly TMP_FontAsset stackCountFont;
        private readonly ICollection<RectTransform> itemRects;
        private readonly ICollection<RectTransform> itemGrabRects;

        public InventoryItemsView(
            TMP_FontAsset stackCountFont,
            ICollection<RectTransform> itemRects,
            ICollection<RectTransform> itemGrabRects)
        {
            this.stackCountFont = stackCountFont;
            this.itemRects = itemRects ?? throw new ArgumentNullException(nameof(itemRects));
            this.itemGrabRects = itemGrabRects ?? throw new ArgumentNullException(nameof(itemGrabRects));
        }

        public void Render(IInventory inventory, InventoryView inventoryView, Action<RectTransform, ItemInInventory> onItemRendered = null)
        {
            if (inventory == null || inventoryView == null)
            {
                return;
            }

            var gridLayout = inventoryView.ContentForTiles.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
            {
                return;
            }

            foreach (var item in inventory.Items)
            {
                if (item?.ItemStack?.ItemConfig == null)
                {
                    continue;
                }

                var grabSize = PageUiUtilities.GetItemGrabSize(gridLayout, item.ItemStack.Size);
                var imageRect = PageUiUtilities.CreateItemImage(inventoryView.ContentForItems, item.ItemStack, "Item", stackCountFont, grabSize);
                var grabRect = CreateGrabRect(inventoryView.ContentForItems, item.ItemConfig.Id, grabSize);
                var itemPosition = PageUiUtilities.GetItemAnchoredPosition(gridLayout, item.Position.GetColumn(3));

                imageRect.anchoredPosition = itemPosition;
                grabRect.anchoredPosition = itemPosition;
                itemRects.Add(imageRect);
                itemGrabRects.Add(grabRect);
                onItemRendered?.Invoke(imageRect, item);
            }
        }

        private static RectTransform CreateGrabRect(RectTransform parent, string itemId, Vector2 size)
        {
            var grabObject = new GameObject($"Item Grab [{itemId}]", typeof(RectTransform));
            var grabRect = grabObject.GetComponent<RectTransform>();
            grabRect.SetParent(parent, false);
            grabRect.anchorMin = new Vector2(0f, 1f);
            grabRect.anchorMax = new Vector2(0f, 1f);
            grabRect.pivot = new Vector2(0.5f, 0.5f);
            grabRect.sizeDelta = size;
            return grabRect;
        }
    }

    /// <summary>
    /// Coordinates shared inventory-page hit testing and cursor-relative placement. It deliberately
    /// exposes UI intent only; each page remains responsible for deciding what a successful drop means.
    /// </summary>
    public sealed class InventoryInteractionPresenter
    {
        private readonly PlayerInventory playerInventory;
        private readonly Func<SlotsViewContainer> slotsProvider;
        private readonly Func<IInventory, InventoryGridView> gridViewProvider;
        private readonly Func<Vector2, IInventory> inventoryAtScreenPointProvider;
        private readonly Func<Camera> eventCameraProvider;
        private readonly Func<bool> canUseFastSlots;
        private readonly IReadOnlyList<RectTransform> itemRects;
        private readonly IReadOnlyList<RectTransform> itemGrabRects;
        private readonly List<InventoryItemTarget> inventoryItemTargets = new();

        public InventoryInteractionPresenter(
            PlayerInventory playerInventory,
            Func<SlotsViewContainer> slotsProvider,
            Func<IInventory, InventoryGridView> gridViewProvider,
            Func<Vector2, IInventory> inventoryAtScreenPointProvider,
            Func<Camera> eventCameraProvider,
            Func<bool> canUseFastSlots,
            IReadOnlyList<RectTransform> itemRects,
            IReadOnlyList<RectTransform> itemGrabRects)
        {
            this.playerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            this.slotsProvider = slotsProvider ?? throw new ArgumentNullException(nameof(slotsProvider));
            this.gridViewProvider = gridViewProvider ?? throw new ArgumentNullException(nameof(gridViewProvider));
            this.inventoryAtScreenPointProvider = inventoryAtScreenPointProvider ?? throw new ArgumentNullException(nameof(inventoryAtScreenPointProvider));
            this.eventCameraProvider = eventCameraProvider ?? throw new ArgumentNullException(nameof(eventCameraProvider));
            this.canUseFastSlots = canUseFastSlots ?? throw new ArgumentNullException(nameof(canUseFastSlots));
            this.itemRects = itemRects ?? throw new ArgumentNullException(nameof(itemRects));
            this.itemGrabRects = itemGrabRects ?? throw new ArgumentNullException(nameof(itemGrabRects));
        }

        public Vector2 GrabOffset { get; private set; }

        public bool TryCaptureGrabOffset(Vector2 screenPoint, out Vector2 grabOffset)
        {
            var captured = PageUiUtilities.TryCaptureGrabOffset(itemRects, itemGrabRects, screenPoint, eventCameraProvider(), out grabOffset);
            GrabOffset = grabOffset;
            return captured;
        }

        public void SetGrabOffset(Vector2 grabOffset) => GrabOffset = grabOffset;
        public void ResetGrabOffset() => GrabOffset = Vector2.zero;

        public bool TryGetInventoryAt(Vector2 screenPoint, out IInventory inventory)
        {
            inventory = inventoryAtScreenPointProvider(screenPoint);
            return inventory != null;
        }

        public void ClearInventoryItems() => inventoryItemTargets.Clear();

        public void RegisterInventoryItem(IInventory inventory, RectTransform rect, ItemInInventory itemInInventory)
        {
            if (inventory == null || rect == null || itemInInventory?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            inventoryItemTargets.Add(new InventoryItemTarget(inventory, rect, itemInInventory));
        }

        public bool TryGetInventoryItemAt(Vector2 screenPoint, out IInventory inventory, out ItemInInventory itemInInventory)
        {
            for (var i = inventoryItemTargets.Count - 1; i >= 0; i--)
            {
                var target = inventoryItemTargets[i];
                if (target.Rect == null
                    || !target.Inventory.CanGet(target.Item)
                    || inventoryAtScreenPointProvider(screenPoint) != target.Inventory
                    || !RectTransformUtility.RectangleContainsScreenPoint(target.Rect, screenPoint, eventCameraProvider()))
                {
                    continue;
                }

                inventory = target.Inventory;
                itemInInventory = target.Item;
                return true;
            }

            inventory = null;
            itemInInventory = null;
            return false;
        }

        public bool TryGetHoveredSlot(Vector2 screenPoint, out SlotModel slotModel)
        {
            slotModel = null;
            var slots = slotsProvider();
            if (slots == null)
            {
                return false;
            }

            var requiredType = playerInventory.HandSlot.Value?.ItemConfig?.ItemType;
            return TryGetSlotUnderPointer(slots.HeadSlot, playerInventory.HelmSlot, screenPoint, requiredType, out slotModel)
                   || (!playerInventory.IsFaceSlotBlocked && TryGetSlotUnderPointer(slots.FaceSlot, playerInventory.FaceSlot, screenPoint, requiredType, out slotModel))
                   || TryGetSlotUnderPointer(slots.BodySlot, playerInventory.BodySlot, screenPoint, requiredType, out slotModel)
                   || TryGetSlotUnderPointer(slots.HandsSlot, playerInventory.HandsSlot, screenPoint, requiredType, out slotModel)
                   || TryGetSlotUnderPointer(slots.ArmsSlot, playerInventory.ArmsSlot, screenPoint, requiredType, out slotModel)
                   || TryGetSlotUnderPointer(slots.LegsSlot, playerInventory.LegsSlot, screenPoint, requiredType, out slotModel)
                   || TryGetSlotUnderPointer(slots.HipsSlot, playerInventory.HipsSlot, screenPoint, requiredType, out slotModel)
                   || TryGetSlotUnderPointer(slots.BackpackSlot, playerInventory.BackpackSlot, screenPoint, requiredType, out slotModel)
                   || TryGetSlotUnderPointer(slots.LeftWeaponSlot, playerInventory.LeftWeaponSlot, screenPoint, requiredType, out slotModel)
                   || TryGetSlotUnderPointer(slots.RightWeaponSlot, playerInventory.RightWeaponSlot, screenPoint, requiredType, out slotModel);
        }

        public bool TryGetSlotSize(SlotModel slotModel, out Vector2 slotSize)
        {
            slotSize = Vector2.zero;
            var slots = slotsProvider();
            if (!PageUiUtilities.TryGetSlotRect(slots, playerInventory, slotModel, out var slotRect))
            {
                return false;
            }

            slotSize = slotRect.rect.size;
            return slotSize.x > 0f && slotSize.y > 0f;
        }

        public bool TryGetHoveredFastSlot(Vector2 screenPoint, out FastSlotModel fastSlotModel)
        {
            fastSlotModel = null;
            var slots = slotsProvider();
            if (slots == null || playerInventory.HandSlot.Value?.ItemConfig?.ItemType != ItemType.Usable || !canUseFastSlots())
            {
                return false;
            }

            var eventCamera = eventCameraProvider();
            return PageUiUtilities.TryGetFastSlotUnderPointer(slots.FastSlot1, playerInventory.FastSlot1, screenPoint, eventCamera, out fastSlotModel)
                   || PageUiUtilities.TryGetFastSlotUnderPointer(slots.FastSlot2, playerInventory.FastSlot2, screenPoint, eventCamera, out fastSlotModel)
                   || PageUiUtilities.TryGetFastSlotUnderPointer(slots.FastSlot3, playerInventory.FastSlot3, screenPoint, eventCamera, out fastSlotModel)
                   || PageUiUtilities.TryGetFastSlotUnderPointer(slots.FastSlot4, playerInventory.FastSlot4, screenPoint, eventCamera, out fastSlotModel);
        }

        public bool TryGetFastSlotRect(FastSlotModel fastSlotModel, out RectTransform slotRect)
        {
            return PageUiUtilities.TryGetFastSlotRect(slotsProvider(), playerInventory, fastSlotModel, out slotRect);
        }

        public bool TryGetPlacementTile(Vector2 screenPoint, IInventory inventory, out Tile tile)
        {
            tile = null;
            var gridView = gridViewProvider(inventory);
            return gridView != null && gridView.TryGetPlacementTile(eventCameraProvider(), screenPoint, playerInventory.HandSlot.Value?.ItemStack, GrabOffset, out tile);
        }

        public bool TryGetSnappedGridPosition(Vector2 screenPoint, IInventory inventory, out Vector3 position)
        {
            position = default;
            var gridView = gridViewProvider(inventory);
            return gridView != null && gridView.TryGetSnappedLocalPosition(eventCameraProvider(), screenPoint, playerInventory.HandSlot.Value?.ItemStack, GrabOffset, out position);
        }

        private bool TryGetSlotUnderPointer(SlotView slotView, SlotModel slotModel, Vector2 screenPoint, ItemType? requiredType, out SlotModel hoveredSlot)
        {
            return PageUiUtilities.TryGetSlotUnderPointer(slotView, slotModel, screenPoint, requiredType, eventCameraProvider(), out hoveredSlot);
        }

        private readonly struct InventoryItemTarget
        {
            public InventoryItemTarget(IInventory inventory, RectTransform rect, ItemInInventory item)
            {
                Inventory = inventory;
                Rect = rect;
                Item = item;
            }

            public IInventory Inventory { get; }
            public RectTransform Rect { get; }
            public ItemInInventory Item { get; }
        }
    }

    /// <summary>
    /// Detects changes to the item configurations rendered in player equipment and quick slots.
    /// It stores presentation invalidation state only and has no inventory mutation policy.
    /// </summary>
    public sealed class PlayerInventorySlotStateTracker
    {
        private readonly PlayerInventory inventory;
        private readonly ItemConfig[] cachedItemConfigs = new ItemConfig[14];

        public PlayerInventorySlotStateTracker(PlayerInventory inventory)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public bool HasChanged()
        {
            return cachedItemConfigs[0] != inventory.HelmSlot.ItemConfig
                   || cachedItemConfigs[1] != inventory.FaceSlot.ItemConfig
                   || cachedItemConfigs[2] != inventory.BodySlot.ItemConfig
                   || cachedItemConfigs[3] != inventory.HandsSlot.ItemConfig
                   || cachedItemConfigs[4] != inventory.ArmsSlot.ItemConfig
                   || cachedItemConfigs[5] != inventory.LegsSlot.ItemConfig
                   || cachedItemConfigs[6] != inventory.HipsSlot.ItemConfig
                   || cachedItemConfigs[7] != inventory.BackpackSlot.ItemConfig
                   || cachedItemConfigs[8] != inventory.LeftWeaponSlot.ItemConfig
                   || cachedItemConfigs[9] != inventory.RightWeaponSlot.ItemConfig
                   || cachedItemConfigs[10] != inventory.FastSlot1.ItemConfig
                   || cachedItemConfigs[11] != inventory.FastSlot2.ItemConfig
                   || cachedItemConfigs[12] != inventory.FastSlot3.ItemConfig
                   || cachedItemConfigs[13] != inventory.FastSlot4.ItemConfig;
        }

        public void Capture()
        {
            cachedItemConfigs[0] = inventory.HelmSlot.ItemConfig;
            cachedItemConfigs[1] = inventory.FaceSlot.ItemConfig;
            cachedItemConfigs[2] = inventory.BodySlot.ItemConfig;
            cachedItemConfigs[3] = inventory.HandsSlot.ItemConfig;
            cachedItemConfigs[4] = inventory.ArmsSlot.ItemConfig;
            cachedItemConfigs[5] = inventory.LegsSlot.ItemConfig;
            cachedItemConfigs[6] = inventory.HipsSlot.ItemConfig;
            cachedItemConfigs[7] = inventory.BackpackSlot.ItemConfig;
            cachedItemConfigs[8] = inventory.LeftWeaponSlot.ItemConfig;
            cachedItemConfigs[9] = inventory.RightWeaponSlot.ItemConfig;
            cachedItemConfigs[10] = inventory.FastSlot1.ItemConfig;
            cachedItemConfigs[11] = inventory.FastSlot2.ItemConfig;
            cachedItemConfigs[12] = inventory.FastSlot3.ItemConfig;
            cachedItemConfigs[13] = inventory.FastSlot4.ItemConfig;
        }
    }

    /// <summary>
    /// Owns pointer-hover timing and popup mode transitions. Pages supply only their target lookup
    /// and popup content callbacks, keeping this presenter independent from item actions.
    /// </summary>
    public sealed class InventoryTooltipPresenter<TTarget> where TTarget : class
    {
        private readonly Func<InventoryPopupMode> modeProvider;
        private readonly Action close;
        private readonly Action<Vector2> updatePosition;
        private readonly Func<TTarget, TTarget, bool> isSameTarget;
        private readonly bool resetDelayOnTargetChange;
        private TTarget hoverTarget;
        private float hoverElapsed;

        public InventoryTooltipPresenter(
            Func<InventoryPopupMode> modeProvider,
            Action close,
            Action<Vector2> updatePosition,
            Func<TTarget, TTarget, bool> isSameTarget,
            bool resetDelayOnTargetChange = false)
        {
            this.modeProvider = modeProvider ?? throw new ArgumentNullException(nameof(modeProvider));
            this.close = close ?? throw new ArgumentNullException(nameof(close));
            this.updatePosition = updatePosition ?? throw new ArgumentNullException(nameof(updatePosition));
            this.isSameTarget = isSameTarget ?? throw new ArgumentNullException(nameof(isSameTarget));
            this.resetDelayOnTargetChange = resetDelayOnTargetChange;
        }

        public void Reset()
        {
            hoverTarget = null;
            hoverElapsed = 0f;
        }

        public void Update(
            bool canShow,
            Vector2? screenPoint,
            float deltaTime,
            float hoverDelay,
            Func<Vector2, TTarget> findTarget,
            Func<TTarget, Vector2, InventoryPopupMode, bool> open)
        {
            if (!canShow || !screenPoint.HasValue)
            {
                ResetAndCloseHoverPopup();
                return;
            }

            var pointerPosition = screenPoint.Value;
            var target = findTarget?.Invoke(pointerPosition);
            if (target == null)
            {
                ResetAndCloseHoverPopup();
                return;
            }

            var targetChanged = !isSameTarget(hoverTarget, target);
            if (targetChanged)
            {
                hoverTarget = target;
                if (resetDelayOnTargetChange)
                {
                    hoverElapsed = 0f;
                }
            }

            hoverElapsed += deltaTime;
            var mode = modeProvider();
            if (mode == InventoryPopupMode.Hover)
            {
                if (targetChanged)
                {
                    close();
                    open?.Invoke(target, pointerPosition, InventoryPopupMode.Hover);
                    return;
                }

                updatePosition(pointerPosition);
                return;
            }

            if (mode == InventoryPopupMode.ContextMenu || hoverElapsed < hoverDelay)
            {
                return;
            }

            open?.Invoke(target, pointerPosition, InventoryPopupMode.Hover);
        }

        private void ResetAndCloseHoverPopup()
        {
            Reset();
            if (modeProvider() == InventoryPopupMode.Hover)
            {
                close();
            }
        }
    }
}
