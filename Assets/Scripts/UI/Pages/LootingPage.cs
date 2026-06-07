using System;
using System.Collections.Generic;
using System.Linq;
using Colors;
using Inventory;
using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Looting;
using Inventory.Slot;
using Localization;
using Messages;
using Money;
using Stats;
using TMPro;
using UI.Configs;
using UI.Inventory;
using UI.UIElements;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class LootingPage : BasePage, ITickable, IInventoryInteractionPage
    {
        private sealed class PopupTarget
        {
            public RectTransform Rect;
            public IInventory SourceInventory;
            public ItemInInventory InventoryItem;
            public SlotModel SlotModel;
        }

        private enum PopupOpenMode
        {
            None,
            Hover,
            RightClick
        }

        public override PageType Type { get; } = PageType.Looting;
        public static LootingPage Current { get; private set; }
        public static IInventoryInteractionPage CurrentInteractionPage => Current;

        private readonly UIConfig uiConfig;
        private readonly StatsConfig statsConfig;
        private readonly StatFiller hpFiller;
        private readonly LocalizationConfig localizationConfig;
        private readonly ColorsConfig colorsConfig;
        private readonly StatIconsConfig statIconsConfig;
        private readonly PlayerInventory playerInventory;
        private readonly InventoryHandController inventoryHandController;
        private readonly MoneyStorage playerMoneyStorage;
        private readonly Character.CharacterInfo playerCharacterInfo;
        private readonly LootingContext lootingContext;
        private readonly StatsController statsController;
        private readonly Canvas canvas;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;

        private RectTransform contentRect = null!;
        private RectTransform rightRect = null!;
        private RectTransform leftRect = null!;
        private InfoAboutPlayer rightInfoAboutPlayer = null!;
        private InfoAboutPlayer leftInfoAboutPlayer = null!;
        private InfoAboutInventory rightInfoAboutInventory = null!;
        private InfoAboutInventory leftInfoAboutInventory = null!;
        private SlotsViewContainer slotsViewContainer = null!;
        private InventoryView playerInventoryView = null!;
        private InventoryView targetInventoryView = null!;
        private RectTransform handSlotRect = null!;
        private readonly CompositeDisposable redrawDisposables = new();
        private readonly List<ScrollRect> inventoryScrollRects = new();
        private readonly List<RectTransform> itemRects = new();
        private readonly List<RectTransform> itemGrabRects = new();
        private readonly List<PopupTarget> popupTargets = new();
        private readonly Dictionary<IInventory, InventoryView> inventoryViews = new();
        private readonly Dictionary<IInventory, Vector2Int> lastGridSizes = new();
        private Vector2 handGrabOffset;
        private HeartbeatPulse heartbeatPulse;
        private BloodScreenController bloodScreenController;
        private Image bloodScreen;
        private RectTransform popupRect;
        private RectTransform popupParentRect;
        private PopupTarget hoverPopupTarget;
        private float hoverPopupElapsed;
        private PopupOpenMode popupOpenMode;

        private ItemConfig lastHelmItemConfig;
        private ItemConfig lastFaceItemConfig;
        private ItemConfig lastBodyItemConfig;
        private ItemConfig lastHandsItemConfig;
        private ItemConfig lastLegsItemConfig;
        private ItemConfig lastHipsItemConfig;
        private ItemConfig lastBackpackItemConfig;
        private ItemConfig lastLeftWeaponItemConfig;
        private ItemConfig lastRightWeaponItemConfig;
        private ItemConfig lastFastSlot1ItemConfig;
        private ItemConfig lastFastSlot2ItemConfig;
        private ItemConfig lastFastSlot3ItemConfig;
        private ItemConfig lastFastSlot4ItemConfig;

        public LootingPage(
            UIConfig uiConfig,
            StatsConfig statsConfig,
            StatFillers statFillers,
            LocalizationConfig localizationConfig,
            ColorsConfig colorsConfig,
            StatIconsConfig statIconsConfig,
            Canvas canvas,
            PlayerInventory playerInventory,
            InventoryHandController inventoryHandController,
            MoneyStorage playerMoneyStorage,
            Character.CharacterInfo playerCharacterInfo,
            LootingContext lootingContext,
            StatsController statsController,
            IObjectResolver resolver)
        {
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            hpFiller = statFillers.Get(StatType.Hp);
            this.localizationConfig = localizationConfig;
            this.colorsConfig = colorsConfig;
            this.statIconsConfig = statIconsConfig;
            this.canvas = canvas;
            this.playerInventory = playerInventory;
            this.inventoryHandController = inventoryHandController;
            this.playerCharacterInfo = playerCharacterInfo;
            this.playerMoneyStorage = playerMoneyStorage;
            this.lootingContext = lootingContext;
            this.statsController = statsController;
            this.resolver = resolver;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public bool TryHandleMouseDown(MouseButtonType button, Vector2 screenPoint)
        {
            if (Current != this || contentRect == null || playerInventory.HandSlot.Value?.ItemStack != null)
            {
                return false;
            }

            if (button == MouseButtonType.Right)
            {
                ClosePopup();
                ResetHoverPopupState();
                TryOpenPopup(screenPoint, PopupOpenMode.RightClick);
                return true;
            }

            if (button != MouseButtonType.Left || popupRect == null)
            {
                return false;
            }

            if (popupOpenMode == PopupOpenMode.RightClick
             && RectTransformUtility.RectangleContainsScreenPoint(popupRect, screenPoint, GetEventCamera()))
            {
                return true;
            }

            if (popupOpenMode == PopupOpenMode.Hover)
            {
                ClosePopup();
                ResetHoverPopupState();
                return false;
            }

            ClosePopup();
            ResetHoverPopupState();
            return false;
        }

        public override void Draw()
        {
            var targetInventory = lootingContext.CurrentTargetInventory;
            if (targetInventory == null)
            {
                return;
            }

            Current = this;
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";
            popupParentRect = contentRect;

            leftRect = resolver.Instantiate(uiConfig.LeftSection, contentRect);
            slotsViewContainer = resolver.Instantiate(uiConfig.CenterSection, contentRect);
            PageUiUtilities.FillSlotsViewContainerStats(slotsViewContainer, statsController);
            rightRect = resolver.Instantiate(uiConfig.RightSection, contentRect);

            rightInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            rightInfoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            playerInventoryView = resolver.Instantiate(uiConfig.InventoryView, rightRect);
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo, playerMoneyStorage);

            leftInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, leftRect);
            leftInfoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, leftRect);
            targetInventoryView = resolver.Instantiate(uiConfig.InventoryView, leftRect);
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, lootingContext.CurrentTargetCharacterInfo, lootingContext.CurrentTargetMoneyStorage);

            inventoryViews.Clear();
            inventoryViews[playerInventory] = playerInventoryView;
            inventoryViews[targetInventory] = targetInventoryView;
            CacheInventoryScrollRects();

            DrawTiles(playerInventory);
            DrawTiles(targetInventory);
            bloodScreen = PageUiUtilities.CreateBloodScreen(uiConfig, resolver, contentRect, Type);
            heartbeatPulse = new HeartbeatPulse(statsConfig, statsController.Hp, hpFiller);
            bloodScreenController = new BloodScreenController(statsConfig, statsController.Hp, hpFiller, heartbeatPulse, bloodScreen);

            playerInventory.Changed.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            targetInventory.Changed.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            playerInventory.HandSlot.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            statsController.Changed
                           .Subscribe(_ => PageUiUtilities.FillSlotsViewContainerStats(slotsViewContainer, statsController))
                           .AddTo(redrawDisposables);

            ReDraw();
        }

        public void Tick()
        {
            if (handSlotRect)
            {
                UpdateHandSlotPosition();
            }

            HandleHoverPopup();

            if (HaveSlotsChanged() || HaveGridChanged())
            {
                ReDraw();
            }
        }

        public void ReDraw()
        {
            if (!playerInventoryView)
            {
                return;
            }

            ClosePopup();
            ResetHoverPopupState();
            EnsureTilesMatchInventorySize();
            UpdateInventoryScrollState();
            itemRects.Clear();
            itemGrabRects.Clear();
            popupTargets.Clear();

            foreach (var view in inventoryViews.Values)
            {
                PageUiUtilities.ClearChildren(view.ContentForItems);
            }

            foreach (var pair in inventoryViews)
            {
                DrawItems(pair.Key, pair.Value);
            }

            DrawSlotItems();
            DrawHandSlot();
            UpdateInventoryInfo();
            UpdatePlayersInfo();
            PageUiUtilities.FillSlotsViewContainerStats(slotsViewContainer, statsController);
            CacheSlotItems();
        }

        private void UpdatePlayersInfo()
        {
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo, playerMoneyStorage);
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, lootingContext.CurrentTargetCharacterInfo, lootingContext.CurrentTargetMoneyStorage);
        }

        private void UpdateInventoryInfo()
        {
            var playerWeight = PageUiUtilities.GetItemsWeight(playerInventory)
                             + PageUiUtilities.GetSlotsWeight(
                                 playerInventory.HelmSlot,
                                 playerInventory.FaceSlot,
                                 playerInventory.BodySlot,
                                 playerInventory.HandsSlot,
                                 playerInventory.LegsSlot,
                                 playerInventory.HipsSlot,
                                 playerInventory.BackpackSlot,
                                 playerInventory.LeftWeaponSlot,
                                 playerInventory.RightWeaponSlot);
            var handWeight = playerInventory.HandSlot.Value?.ItemStack?.TotalWeight ?? 0f;
            var handSourceInventory = playerInventory.HandSourceInventory.Value;
            if (handWeight > 0f && handSourceInventory == playerInventory)
            {
                playerWeight += handWeight;
            }

            PageUiUtilities.FillInfoAboutInventory(rightInfoAboutInventory, localizationConfig, colorsConfig, playerWeight, playerInventory.MaxWeight);

            var targetInventory = lootingContext.CurrentTargetInventory;
            var targetWeight = PageUiUtilities.GetItemsWeight(targetInventory);
            if (handWeight > 0f && handSourceInventory == targetInventory)
            {
                targetWeight += handWeight;
            }

            PageUiUtilities.FillInfoAboutInventory(leftInfoAboutInventory, localizationConfig, colorsConfig, targetWeight, targetInventory?.MaxWeight);
        }

        public bool TryCaptureGrabOffset(Vector2 screenPoint, out Vector2 handGrabOffset)
        {
            var eventCamera = GetEventCamera();
            var captured = PageUiUtilities.TryCaptureGrabOffset(itemRects, itemGrabRects, screenPoint, eventCamera, out handGrabOffset);
            this.handGrabOffset = handGrabOffset;
            return captured;
        }

        public void SetGrabOffset(Vector2 handGrabOffset)
        {
            this.handGrabOffset = handGrabOffset;
        }

        public void ResetGrabOffset()
        {
            handGrabOffset = Vector2.zero;
        }

        public bool TryGetHoveredSlot(Vector2 screenPoint, out SlotModel slotModel)
        {
            slotModel = null;
            if (!slotsViewContainer)
            {
                return false;
            }

            var handItemType = playerInventory.HandSlot.Value?.ItemConfig?.ItemType;
            if (TryGetSlotUnderPointer(slotsViewContainer.HeadSlot, playerInventory.HelmSlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            if (!playerInventory.IsFaceSlotBlocked
             && TryGetSlotUnderPointer(slotsViewContainer.FaceSlot, playerInventory.FaceSlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            if (TryGetSlotUnderPointer(slotsViewContainer.BodySlot, playerInventory.BodySlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            if (TryGetSlotUnderPointer(slotsViewContainer.HandsSlot, playerInventory.HandsSlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            if (TryGetSlotUnderPointer(slotsViewContainer.LegsSlot, playerInventory.LegsSlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            if (TryGetSlotUnderPointer(slotsViewContainer.HipsSlot, playerInventory.HipsSlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            if (TryGetSlotUnderPointer(slotsViewContainer.BackpackSlot, playerInventory.BackpackSlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            if (TryGetSlotUnderPointer(slotsViewContainer.LeftWeaponSlot, playerInventory.LeftWeaponSlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            return TryGetSlotUnderPointer(slotsViewContainer.RightWeaponSlot, playerInventory.RightWeaponSlot, screenPoint, handItemType, out slotModel);
        }

        public bool TryGetHoveredFastSlot(Vector2 screenPoint, out FastSlotModel fastSlotModel)
        {
            fastSlotModel = null;
            if (!slotsViewContainer
             || playerInventory.HandSlot.Value?.ItemConfig?.ItemType != ItemType.Usable
             || playerInventory.HandSourceInventory.Value != playerInventory)
            {
                return false;
            }

            var eventCamera = GetEventCamera();
            return PageUiUtilities.TryGetFastSlotUnderPointer(slotsViewContainer.FastSlot1, playerInventory.FastSlot1, screenPoint, eventCamera, out fastSlotModel)
                   || PageUiUtilities.TryGetFastSlotUnderPointer(slotsViewContainer.FastSlot2, playerInventory.FastSlot2, screenPoint, eventCamera, out fastSlotModel)
                   || PageUiUtilities.TryGetFastSlotUnderPointer(slotsViewContainer.FastSlot3, playerInventory.FastSlot3, screenPoint, eventCamera, out fastSlotModel)
                   || PageUiUtilities.TryGetFastSlotUnderPointer(slotsViewContainer.FastSlot4, playerInventory.FastSlot4, screenPoint, eventCamera, out fastSlotModel);
        }

        public bool TryGetFastSlotRect(FastSlotModel fastSlotModel, out RectTransform slotRect)
        {
            return PageUiUtilities.TryGetFastSlotRect(slotsViewContainer, playerInventory, fastSlotModel, out slotRect);
        }

        public bool TryGetPlacementTile(Vector2 screenPoint, IInventory inventory, out Tile tile)
        {
            tile = null;
            if (inventory == null || !TryGetPlacementCell(screenPoint, inventory, out var placementCell))
            {
                return false;
            }

            var tiles = GetTiles(inventory);
            return tiles != null && tiles.TryGetTile(placementCell.x, placementCell.y, out tile);
        }

        public bool IsInPlayerSections(Vector2 screenPoint)
        {
            var eventCamera = GetEventCamera();
            if (rightRect && RectTransformUtility.RectangleContainsScreenPoint(rightRect, screenPoint, eventCamera))
            {
                return true;
            }

            var centerRect = slotsViewContainer ? slotsViewContainer.GetComponent<RectTransform>() : null;
            return centerRect && RectTransformUtility.RectangleContainsScreenPoint(centerRect, screenPoint, eventCamera);
        }

        public bool IsInTargetSection(Vector2 screenPoint)
        {
            return leftRect && RectTransformUtility.RectangleContainsScreenPoint(leftRect, screenPoint, GetEventCamera());
        }

        public IInventory GetTargetInventory()
        {
            return lootingContext.CurrentTargetInventory;
        }

        private void UpdateInventoryScrollState()
        {
            var isDraggingItem = playerInventory.HandSlot.Value?.ItemStack != null;
            foreach (var scrollRect in inventoryScrollRects)
            {
                if (!scrollRect)
                {
                    continue;
                }

                scrollRect.enabled = !isDraggingItem;
            }
        }

        private void CacheInventoryScrollRects()
        {
            inventoryScrollRects.Clear();

            foreach (var inventoryView in inventoryViews.Values)
            {
                if (!inventoryView)
                {
                    continue;
                }

                var scrollRect = inventoryView.GetComponent<ScrollRect>() ?? inventoryView.GetComponentInParent<ScrollRect>();
                if (scrollRect && !inventoryScrollRects.Contains(scrollRect))
                {
                    inventoryScrollRects.Add(scrollRect);
                }
            }
        }

        private void DrawHandSlot()
        {
            var handItemStack = playerInventory.HandSlot.Value?.ItemStack;
            handSlotRect = PageUiUtilities.DrawHandSlot(handSlotRect, canvasRect, handItemStack, GetHandStackAnchorAreaSize(handItemStack));
            if (handSlotRect)
            {
                UpdateHandSlotPosition();
            }
        }

        private Vector2? GetHandStackAnchorAreaSize(ItemStack handItemStack)
        {
            if (handItemStack?.ItemConfig == null)
            {
                return null;
            }

            var sourceInventory = playerInventory.HandSourceInventory.Value;
            if (sourceInventory != null
             && inventoryViews.TryGetValue(sourceInventory, out var sourceView)
             && sourceView != null)
            {
                var sourceGridLayout = sourceView.ContentForTiles.GetComponent<GridLayoutGroup>();
                if (sourceGridLayout != null)
                {
                    return PageUiUtilities.GetItemGrabSize(sourceGridLayout, handItemStack.Size);
                }
            }

            return null;
        }

        private void UpdateHandSlotPosition()
        {
            var dragParentRect = handSlotRect.parent as RectTransform;
            var eventCamera = GetEventCamera();
            if (!PageUiUtilities.TryGetPointerPositionLocalToRect(dragParentRect, eventCamera, out var pointerPosition, out var localPoint))
            {
                return;
            }

            if (TryGetSnappedHandPosition(pointerPosition, eventCamera, dragParentRect, out var snappedPosition))
            {
                handSlotRect.anchoredPosition = snappedPosition;
                return;
            }

            handSlotRect.anchoredPosition = localPoint - handGrabOffset;
        }

        private bool TryGetSnappedHandPosition(Vector2 screenPoint, Camera eventCamera, RectTransform dragParentRect, out Vector2 snappedPosition)
        {
            snappedPosition = Vector2.zero;
            if (TryGetSnappedPositionInSlot(screenPoint, eventCamera, dragParentRect, out var slotSnappedPosition))
            {
                snappedPosition = slotSnappedPosition;
                return true;
            }

            if (!InventoryTilePointerHandler.TryGetHovered(out var hoveredInventory, out _)
             || !TryGetSnappedPositionInInventoryGridLocal(screenPoint, hoveredInventory, out var snappedLocalPosition))
            {
                return false;
            }

            if (!inventoryViews.TryGetValue(hoveredInventory, out var hoveredView))
            {
                return false;
            }

            var snappedWorldPosition = hoveredView.ContentForTiles.TransformPoint(snappedLocalPosition);
            var snappedScreenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, snappedWorldPosition);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(dragParentRect, snappedScreenPosition, eventCamera, out snappedPosition);
        }

        private bool TryGetSnappedPositionInSlot(Vector2 screenPoint, Camera eventCamera, RectTransform dragParentRect, out Vector2 snappedPosition)
        {
            snappedPosition = Vector2.zero;
            if (TryGetHoveredFastSlot(screenPoint, out var fastSlotModel)
             && TryGetFastSlotRect(fastSlotModel, out var fastSlotRect))
            {
                var fastSlotWorldPosition = fastSlotRect.TransformPoint(fastSlotRect.rect.center);
                var fastSlotScreenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, fastSlotWorldPosition);
                return RectTransformUtility.ScreenPointToLocalPointInRectangle(dragParentRect, fastSlotScreenPosition, eventCamera, out snappedPosition);
            }

            if (!TryGetHoveredSlot(screenPoint, out var slotModel) || slotModel == null)
            {
                return false;
            }

            var handItemConfig = playerInventory.HandSlot.Value?.ItemConfig;
            if (handItemConfig == null || slotModel.ItemType != handItemConfig.ItemType)
            {
                return false;
            }

            if (!PageUiUtilities.TryGetSlotRect(slotsViewContainer, playerInventory, slotModel, out var slotRect))
            {
                return false;
            }

            var slotWorldPosition = slotRect.TransformPoint(slotRect.rect.center);
            var slotScreenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, slotWorldPosition);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(dragParentRect, slotScreenPosition, eventCamera, out snappedPosition);
        }

        private bool TryGetSlotUnderPointer(SlotView slotView, SlotModel slotModel, Vector2 screenPoint, ItemType? requiredType, out SlotModel hoveredSlotModel)
        {
            var eventCamera = GetEventCamera();
            return PageUiUtilities.TryGetSlotUnderPointer(slotView, slotModel, screenPoint, requiredType, eventCamera, out hoveredSlotModel);
        }

        private bool HaveSlotsChanged()
        {
            return lastHelmItemConfig != playerInventory.HelmSlot.ItemConfig
                   || lastFaceItemConfig != playerInventory.FaceSlot.ItemConfig
                   || lastBodyItemConfig != playerInventory.BodySlot.ItemConfig
                   || lastHandsItemConfig != playerInventory.HandsSlot.ItemConfig
                   || lastLegsItemConfig != playerInventory.LegsSlot.ItemConfig
                   || lastHipsItemConfig != playerInventory.HipsSlot.ItemConfig
                   || lastBackpackItemConfig != playerInventory.BackpackSlot.ItemConfig
                   || lastLeftWeaponItemConfig != playerInventory.LeftWeaponSlot.ItemConfig
                   || lastRightWeaponItemConfig != playerInventory.RightWeaponSlot.ItemConfig
                   || lastFastSlot1ItemConfig != playerInventory.FastSlot1.ItemConfig
                   || lastFastSlot2ItemConfig != playerInventory.FastSlot2.ItemConfig
                   || lastFastSlot3ItemConfig != playerInventory.FastSlot3.ItemConfig
                   || lastFastSlot4ItemConfig != playerInventory.FastSlot4.ItemConfig;
        }

        private bool HaveGridChanged()
        {
            foreach (var pair in inventoryViews)
            {
                var tiles = GetTiles(pair.Key);
                if (tiles == null)
                {
                    continue;
                }

                var currentSize = new Vector2Int(tiles.tiles.GetLength(0), tiles.tiles.GetLength(1));
                if (!lastGridSizes.TryGetValue(pair.Key, out var lastSize) || currentSize != lastSize)
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheSlotItems()
        {
            lastHelmItemConfig = playerInventory.HelmSlot.ItemConfig;
            lastFaceItemConfig = playerInventory.FaceSlot.ItemConfig;
            lastBodyItemConfig = playerInventory.BodySlot.ItemConfig;
            lastHandsItemConfig = playerInventory.HandsSlot.ItemConfig;
            lastLegsItemConfig = playerInventory.LegsSlot.ItemConfig;
            lastHipsItemConfig = playerInventory.HipsSlot.ItemConfig;
            lastBackpackItemConfig = playerInventory.BackpackSlot.ItemConfig;
            lastLeftWeaponItemConfig = playerInventory.LeftWeaponSlot.ItemConfig;
            lastRightWeaponItemConfig = playerInventory.RightWeaponSlot.ItemConfig;
            lastFastSlot1ItemConfig = playerInventory.FastSlot1.ItemConfig;
            lastFastSlot2ItemConfig = playerInventory.FastSlot2.ItemConfig;
            lastFastSlot3ItemConfig = playerInventory.FastSlot3.ItemConfig;
            lastFastSlot4ItemConfig = playerInventory.FastSlot4.ItemConfig;
        }

        private bool TryGetPlacementCell(Vector2 screenPoint, IInventory inventory, out Vector2Int placementCell)
        {
            placementCell = default;
            if (!TryGetCursorCellAndLayout(screenPoint, inventory, out var cursorCell, out var gridLayoutGroup))
            {
                return false;
            }

            var handItemStack = playerInventory.HandSlot.Value?.ItemStack;
            if (handItemStack?.ItemConfig == null)
            {
                return false;
            }

            var itemGrabSize = PageUiUtilities.GetItemGrabSize(gridLayoutGroup, handItemStack.Size);
            var stepX = gridLayoutGroup.cellSize.x + gridLayoutGroup.spacing.x;
            var stepY = gridLayoutGroup.cellSize.y + gridLayoutGroup.spacing.y;
            if (stepX <= 0 || stepY <= 0)
            {
                return false;
            }

            var grabFromLeft = handGrabOffset.x + itemGrabSize.x * 0.5f;
            var grabFromTop = itemGrabSize.y * 0.5f - handGrabOffset.y;
            var grabOffsetX = Mathf.Clamp(Mathf.FloorToInt(grabFromLeft / stepX), 0, handItemStack.Size.x - 1);
            var grabOffsetY = Mathf.Clamp(Mathf.FloorToInt(grabFromTop / stepY), 0, handItemStack.Size.y - 1);

            placementCell = new Vector2Int(cursorCell.x - grabOffsetX, cursorCell.y - grabOffsetY);
            return true;
        }

        private bool TryGetSnappedPositionInInventoryGridLocal(Vector2 screenPoint, IInventory inventory, out Vector3 snappedLocalPosition)
        {
            snappedLocalPosition = Vector3.zero;
            if (!TryGetPlacementCell(screenPoint, inventory, out var placementCell) || !inventoryViews.TryGetValue(inventory, out var view))
            {
                return false;
            }

            var handItemStack = playerInventory.HandSlot.Value?.ItemStack;
            var gridLayoutGroup = view.ContentForTiles.GetComponent<GridLayoutGroup>();
            var tiles = GetTiles(inventory);
            if (handItemStack?.ItemConfig == null || gridLayoutGroup == null || tiles == null)
            {
                return false;
            }

            var gridWidth = tiles.tiles.GetLength(0);
            var gridHeight = tiles.tiles.GetLength(1);
            var isFullyInsideGrid =
                placementCell.x >= 0
                && placementCell.y >= 0
                && placementCell.x + handItemStack.Size.x <= gridWidth
                && placementCell.y + handItemStack.Size.y <= gridHeight;
            if (!isFullyInsideGrid)
            {
                return false;
            }

            var snappedAnchoredPosition = new Vector2(
                gridLayoutGroup.padding.left
                + (placementCell.x + handItemStack.Size.x * 0.5f) * gridLayoutGroup.cellSize.x
                + (placementCell.x + (handItemStack.Size.x - 1) * 0.5f) * gridLayoutGroup.spacing.x,
                -(gridLayoutGroup.padding.top
                  + (placementCell.y + handItemStack.Size.y * 0.5f) * gridLayoutGroup.cellSize.y
                  + (placementCell.y + (handItemStack.Size.y - 1) * 0.5f) * gridLayoutGroup.spacing.y));

            snappedLocalPosition = new Vector3(snappedAnchoredPosition.x, snappedAnchoredPosition.y, 0f);
            return true;
        }

        private bool TryGetCursorCellAndLayout(Vector2 screenPoint, IInventory inventory, out Vector2Int cursorCell, out GridLayoutGroup gridLayoutGroup)
        {
            cursorCell = default;
            gridLayoutGroup = null;
            if (!inventoryViews.TryGetValue(inventory, out var view))
            {
                return false;
            }

            var gridRect = view.ContentForTiles;
            if (!gridRect)
            {
                return false;
            }

            var eventCamera = GetEventCamera();
            if (!RectTransformUtility.RectangleContainsScreenPoint(gridRect, screenPoint, eventCamera))
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPoint, eventCamera, out var localPoint))
            {
                return false;
            }

            gridLayoutGroup = gridRect.GetComponent<GridLayoutGroup>();
            if (gridLayoutGroup == null)
            {
                return false;
            }

            var rect = gridRect.rect;
            var pivot = gridRect.pivot;
            var xFromLeft = localPoint.x + rect.width * pivot.x;
            var yFromTop = rect.height * (1f - pivot.y) - localPoint.y;
            var stepX = gridLayoutGroup.cellSize.x + gridLayoutGroup.spacing.x;
            var stepY = gridLayoutGroup.cellSize.y + gridLayoutGroup.spacing.y;
            if (stepX <= 0 || stepY <= 0)
            {
                return false;
            }

            var xInCells = (xFromLeft - gridLayoutGroup.padding.left) / stepX;
            var yInCells = (yFromTop - gridLayoutGroup.padding.top) / stepY;
            cursorCell = new Vector2Int(Mathf.FloorToInt(xInCells), Mathf.FloorToInt(yInCells));
            return true;
        }

        private void DrawSlotItems()
        {
            DrawSlotItem(slotsViewContainer.HeadSlot, playerInventory.HelmSlot);
            DrawSlotItem(slotsViewContainer.FaceSlot, playerInventory.FaceSlot);
            DrawSlotItem(slotsViewContainer.BodySlot, playerInventory.BodySlot);
            DrawSlotItem(slotsViewContainer.HandsSlot, playerInventory.HandsSlot);
            DrawSlotItem(slotsViewContainer.LegsSlot, playerInventory.LegsSlot);
            DrawSlotItem(slotsViewContainer.HipsSlot, playerInventory.HipsSlot);
            DrawSlotItem(slotsViewContainer.BackpackSlot, playerInventory.BackpackSlot);
            DrawSlotItem(slotsViewContainer.LeftWeaponSlot, playerInventory.LeftWeaponSlot);
            DrawSlotItem(slotsViewContainer.RightWeaponSlot, playerInventory.RightWeaponSlot);
            DrawFastSlotItem(slotsViewContainer.FastSlot1, playerInventory.FastSlot1);
            DrawFastSlotItem(slotsViewContainer.FastSlot2, playerInventory.FastSlot2);
            DrawFastSlotItem(slotsViewContainer.FastSlot3, playerInventory.FastSlot3);
            DrawFastSlotItem(slotsViewContainer.FastSlot4, playerInventory.FastSlot4);
        }

        private void DrawSlotItem(SlotView slotView, SlotModel slotModel)
        {
            PageUiUtilities.SetSlotBlockedState(slotView, slotModel == playerInventory.FaceSlot && playerInventory.IsFaceSlotBlocked);
            PageUiUtilities.DrawSlotItem(slotView, slotModel, itemRects, itemGrabRects);
            if (slotView == null || slotModel?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            var slotRect = slotView.GetComponent<RectTransform>();
            if (slotRect == null)
            {
                return;
            }

            popupTargets.Add(new PopupTarget
            {
                Rect = slotRect,
                SourceInventory = playerInventory,
                SlotModel = slotModel
            });
        }

        private void DrawFastSlotItem(SlotView slotView, FastSlotModel fastSlotModel)
        {
            PageUiUtilities.DrawFastSlotItem(slotView, fastSlotModel, playerInventory.HasAnyInventoryItem(fastSlotModel?.ItemConfig));
        }

        private void DrawItems(IInventory inventory, InventoryView inventoryView)
        {
            var gridLayoutGroup = inventoryView.ContentForTiles.GetComponent<GridLayoutGroup>();
            if (gridLayoutGroup == null)
            {
                return;
            }

            foreach (var item in inventory.Items)
            {
                var itemGrabSize = PageUiUtilities.GetItemGrabSize(gridLayoutGroup, item.ItemStack.Size);
                var itemImageRect = PageUiUtilities.CreateItemImage(inventoryView.ContentForItems, item.ItemStack, "Item", itemGrabSize);
                itemRects.Add(itemImageRect);
                popupTargets.Add(new PopupTarget
                {
                    Rect = itemImageRect,
                    SourceInventory = inventory,
                    InventoryItem = item
                });

                var itemGrabRectObject = new GameObject($"Item Grab [{item.ItemConfig.Id}]", typeof(RectTransform));
                var itemGrabRect = itemGrabRectObject.GetComponent<RectTransform>();
                itemGrabRect.SetParent(inventoryView.ContentForItems, false);
                itemGrabRect.anchorMin = new Vector2(0, 1);
                itemGrabRect.anchorMax = new Vector2(0, 1);
                itemGrabRect.pivot = new Vector2(0.5f, 0.5f);
                itemGrabRect.sizeDelta = itemGrabSize;
                itemGrabRects.Add(itemGrabRect);

                var itemCenterPosition = item.Position.GetColumn(3);
                var itemAnchoredPosition = PageUiUtilities.GetItemAnchoredPosition(gridLayoutGroup, itemCenterPosition);

                itemImageRect.anchoredPosition = itemAnchoredPosition;
                itemGrabRect.anchoredPosition = itemAnchoredPosition;
            }
        }

        private void DrawTiles(IInventory inventory)
        {
            if (!inventoryViews.TryGetValue(inventory, out var view))
            {
                return;
            }

            var tiles = GetTiles(inventory);
            if (tiles == null)
            {
                return;
            }

            PageUiUtilities.ClearChildren(view.ContentForTiles);
            var gridWidth = tiles.tiles.GetLength(0);
            var gridHeight = tiles.tiles.GetLength(1);

            for (var y = 0; y < gridHeight; y++)
            for (var x = 0; x < gridWidth; x++)
            {
                var tile = resolver.Instantiate(uiConfig.Tile, view.ContentForTiles);
                tile.Initialize(inventory, tiles.GetTile(x, y));
                tile.GetComponentInChildren<TMP_Text>().text = $"{x}:{y}";
            }

            lastGridSizes[inventory] = new Vector2Int(gridWidth, gridHeight);
        }

        private void EnsureTilesMatchInventorySize()
        {
            foreach (var inventory in inventoryViews.Keys.ToArray())
            {
                var tiles = GetTiles(inventory);
                if (tiles == null)
                {
                    continue;
                }

                var currentSize = new Vector2Int(tiles.tiles.GetLength(0), tiles.tiles.GetLength(1));
                if (!lastGridSizes.TryGetValue(inventory, out var lastSize) || currentSize != lastSize)
                {
                    DrawTiles(inventory);
                }
            }
        }

        private void HandleHoverPopup()
        {
            if (Current != this || contentRect == null)
            {
                return;
            }

            if (playerInventory.HandSlot.Value?.ItemStack != null || Pointer.current == null)
            {
                ResetHoverPopupState();
                if (popupOpenMode == PopupOpenMode.Hover)
                {
                    ClosePopup();
                }

                return;
            }

            var screenPoint = Pointer.current.position.ReadValue();
            if (!TryGetPopupTarget(screenPoint, out var target))
            {
                ResetHoverPopupState();
                if (popupOpenMode == PopupOpenMode.Hover)
                {
                    ClosePopup();
                }

                return;
            }

            var targetChanged = !IsSamePopupTarget(hoverPopupTarget, target);
            if (targetChanged)
            {
                hoverPopupTarget = target;
            }

            hoverPopupElapsed += Time.deltaTime;

            if (popupOpenMode == PopupOpenMode.Hover)
            {
                if (targetChanged)
                {
                    ClosePopup();
                    TryOpenPopup(target, screenPoint, PopupOpenMode.Hover);
                    return;
                }

                PageUiUtilities.UpdatePopupPosition(popupRect, popupParentRect, GetEventCamera(), screenPoint);
                return;
            }

            if (popupOpenMode == PopupOpenMode.RightClick)
            {
                return;
            }

            if (hoverPopupElapsed < uiConfig.PopupHoverOpenDelaySeconds)
            {
                return;
            }

            TryOpenPopup(target, screenPoint, PopupOpenMode.Hover);
        }

        private void ResetHoverPopupState()
        {
            hoverPopupTarget = null;
            hoverPopupElapsed = 0f;
        }

        private bool TryOpenPopup(Vector2 screenPoint, PopupOpenMode openMode)
        {
            return TryGetPopupTarget(screenPoint, out var target) && TryOpenPopup(target, screenPoint, openMode);
        }

        private bool TryOpenPopup(PopupTarget target, Vector2 screenPoint, PopupOpenMode openMode)
        {
            if (target == null || popupParentRect == null)
            {
                return false;
            }

            popupRect = resolver.Instantiate(uiConfig.PopupRect, popupParentRect);
            popupRect.name = $"{uiConfig.PopupRect.name} | Looting Popup";
            PageUiUtilities.SetPopupRaycastState(popupRect, openMode == PopupOpenMode.RightClick);

            var itemConfig = GetPopupItemConfig(target);
            var itemStack = GetPopupItemStack(target);
            if (itemConfig == null)
            {
                ClosePopup();
                return false;
            }

            if (openMode == PopupOpenMode.Hover)
            {
                PageUiUtilities.FillInventoryHoverPopup(
                    popupRect,
                    uiConfig,
                    localizationConfig,
                    statIconsConfig,
                    statsController,
                    playerInventory,
                    resolver,
                    itemConfig,
                    itemStack,
                    target?.SlotModel?.ItemConfig == itemConfig,
                    Color.white,
                    Color.green,
                    Color.red);
            }
            else if (openMode == PopupOpenMode.RightClick)
            {
                CreatePopupButtons(target);
            }

            PageUiUtilities.RecalculatePopupSize(popupRect);
            PageUiUtilities.UpdatePopupPosition(popupRect, popupParentRect, GetEventCamera(), screenPoint);
            popupOpenMode = openMode;
            return true;
        }

        private static bool IsSamePopupTarget(PopupTarget first, PopupTarget second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            if (first == null || second == null)
            {
                return false;
            }

            return ReferenceEquals(first.InventoryItem, second.InventoryItem)
                && ReferenceEquals(first.SlotModel, second.SlotModel)
                && ReferenceEquals(first.SourceInventory, second.SourceInventory);
        }

        private static ItemConfig GetPopupItemConfig(PopupTarget target)
        {
            return target?.SlotModel?.ItemStack?.ItemConfig
                   ?? target?.InventoryItem?.ItemStack?.ItemConfig;
        }

        private static ItemStack GetPopupItemStack(PopupTarget target)
        {
            return target?.SlotModel?.ItemStack
                   ?? target?.InventoryItem?.ItemStack;
        }

        private void CreatePopupButtons(PopupTarget target)
        {
            if (target?.SlotModel?.ItemStack?.ItemConfig != null)
            {
                CreateSlotPopupButtons(target.SlotModel);
                return;
            }

            if (target?.InventoryItem?.ItemStack?.ItemConfig != null)
            {
                CreateInventoryPopupButtons(target);
            }
        }

        private void CreateSlotPopupButtons(SlotModel slotModel)
        {
            if (slotModel?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            CreatePopupButton("Move", () => ExecutePopupAction(() => MoveSlotItem(slotModel, slotModel.ItemStack?.Count ?? 0)), CanMoveSlotItem(slotModel));
        }

        private void CreateInventoryPopupButtons(PopupTarget target)
        {
            var itemStack = target.InventoryItem?.ItemStack;
            var itemCount = itemStack?.Count ?? 0;
            var moveHalfCount = GetDropHalfCount(itemCount);

            CreatePopupButton("Use", () => ExecutePopupAction(() => UseTarget(target)), CanUseInventoryItem(itemStack?.ItemConfig));

            if (moveHalfCount > 0)
            {
                CreatePopupButton("Move Half", () => ExecutePopupAction(() => MoveTarget(target, moveHalfCount)), CanMoveInventoryItem(target, moveHalfCount));
            }

            CreatePopupButton("Move", () => ExecutePopupAction(() => MoveTarget(target, itemCount)), CanMoveInventoryItem(target, itemCount));
        }

        private void ExecutePopupAction(Action action)
        {
            ClosePopup();
            ResetHoverPopupState();
            action?.Invoke();
        }

        private void CreatePopupButton(string label, UnityEngine.Events.UnityAction onClick, bool interactable = true)
        {
            PageUiUtilities.CreatePopupButton(popupRect, uiConfig, resolver, label, onClick, interactable);
        }

        private bool CanUseInventoryItem(ItemConfig itemConfig)
        {
            return itemConfig != null && itemConfig.ItemType is ItemType.Usable
                or ItemType.Helm
                or ItemType.Face
                or ItemType.Body
                or ItemType.Hands
                or ItemType.Legs
                or ItemType.Hips
                or ItemType.Backpack
                or ItemType.Weapon;
        }

        private void UseTarget(PopupTarget target)
        {
            if (target?.InventoryItem?.ItemStack?.ItemConfig == null || target.SourceInventory == null)
            {
                return;
            }

            UseInventoryItem(target.SourceInventory, target.InventoryItem);
        }

        private void UseInventoryItem(IInventory sourceInventory, ItemInInventory itemInInventory)
        {
            if (sourceInventory == null || itemInInventory?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            if (itemInInventory.ItemStack.ItemConfig.ItemType == ItemType.Usable)
            {
                UseUsableInventoryItem(sourceInventory, itemInInventory);
                return;
            }

            TryUseEquippableInventoryItem(sourceInventory, itemInInventory);
        }

        private void UseUsableInventoryItem(IInventory sourceInventory, ItemInInventory itemInInventory)
        {
            var sourceStack = itemInInventory?.ItemStack;
            if (sourceInventory == null || sourceStack?.ItemConfig == null)
            {
                return;
            }

            var itemConfig = sourceStack.ItemConfig;
            var isRotated = sourceStack.IsRotated;
            var originalPosition = itemInInventory.Position;
            var originalCount = sourceStack.Count;
            var usedStack = new ItemStack(itemConfig, 1, isRotated);

            sourceInventory.Remove(itemInInventory);
            if (!inventoryHandController.TryUseFromInventory(usedStack))
            {
                sourceInventory.Add(new ItemStack(itemConfig, originalCount, isRotated), originalPosition);
                return;
            }

            if (originalCount > 1)
            {
                sourceInventory.Add(new ItemStack(itemConfig, originalCount - 1, isRotated), originalPosition);
            }
        }

        private void TryUseEquippableInventoryItem(IInventory sourceInventory, ItemInInventory itemInInventory)
        {
            var itemStack = itemInInventory?.ItemStack?.Clone();
            if (sourceInventory == null || itemStack?.ItemConfig == null)
            {
                return;
            }

            var originalPosition = itemInInventory.Position;
            sourceInventory.Remove(itemInInventory);

            if (!inventoryHandController.TryUseFromInventory(itemStack, TryStoreAcrossLootingInventories))
            {
                sourceInventory.Add(itemStack, originalPosition);
            }
        }

        private ItemStack TryStoreAcrossLootingInventories(ItemStack itemStack)
        {
            if (itemStack?.ItemConfig == null)
            {
                return null;
            }

            var remainder = playerInventory.TryAdd(itemStack);
            var targetInventory = lootingContext.CurrentTargetInventory;
            return remainder != null && targetInventory != null
                ? targetInventory.TryAdd(remainder)
                : remainder;
        }

        private bool CanMoveSlotItem(SlotModel slotModel)
        {
            return slotModel?.ItemStack?.ItemConfig != null
                   && CanInventoryAcceptAny(GetOtherInventory(playerInventory), slotModel.ItemStack);
        }

        private bool CanMoveInventoryItem(PopupTarget target, int count)
        {
            return count > 0
                   && target?.InventoryItem?.ItemStack?.ItemConfig != null
                   && CanInventoryAcceptAny(GetOtherInventory(target.SourceInventory), target.InventoryItem.ItemStack);
        }

        private IInventory GetOtherInventory(IInventory sourceInventory)
        {
            var targetInventory = lootingContext.CurrentTargetInventory;
            if (sourceInventory == playerInventory)
            {
                return targetInventory;
            }

            return sourceInventory == targetInventory ? playerInventory : null;
        }

        private static int GetDropHalfCount(int totalCount)
        {
            return totalCount > 1 ? totalCount / 2 : 0;
        }

        private bool CanInventoryAcceptAny(IInventory inventory, ItemStack itemStack)
        {
            if (inventory == null || itemStack?.ItemConfig == null)
            {
                return false;
            }

            return inventory switch
            {
                PlayerInventory playerDestination => CanPlayerInventoryAcceptAny(playerDestination, itemStack),
                ITiledInventory tiledDestination => CanTiledInventoryAcceptAny(tiledDestination, itemStack),
                _ => false
            };
        }

        private static bool CanPlayerInventoryAcceptAny(PlayerInventory inventory, ItemStack itemStack)
        {
            if (inventory == null || itemStack?.ItemConfig == null)
            {
                return false;
            }

            if (itemStack.ItemConfig.ItemType == ItemType.Helm
             && itemStack.ItemConfig.BlocksFaceSlot
             && inventory.FaceSlot.ItemConfig != null
             && !inventory.CanMoveSlotItemToGrid(inventory.FaceSlot))
            {
                return false;
            }

            foreach (var slot in new[]
                     {
                         inventory.HelmSlot,
                         inventory.FaceSlot,
                         inventory.BodySlot,
                         inventory.HandsSlot,
                         inventory.LegsSlot,
                         inventory.HipsSlot,
                         inventory.BackpackSlot,
                         inventory.LeftWeaponSlot,
                         inventory.RightWeaponSlot
                     })
            {
                if (inventory.IsSlotBlocked(slot) || slot.ItemType != itemStack.ItemConfig.ItemType)
                {
                    continue;
                }

                if (slot.ItemStack == null)
                {
                    return true;
                }

                if (slot.ItemStack.CanStackWith(itemStack) && slot.ItemStack.Count < slot.GetMaxStack(itemStack.ItemConfig))
                {
                    return true;
                }
            }

            foreach (var item in inventory.Items)
            {
                if (item?.ItemStack?.CanStackWith(itemStack) == true && !item.ItemStack.IsFull)
                {
                    return true;
                }
            }

            foreach (var tile in inventory.Tiles.tiles)
            {
                if (CanPlaceAt(tile, inventory.Tiles, itemStack.Size))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanTiledInventoryAcceptAny(ITiledInventory inventory, ItemStack itemStack)
        {
            if (inventory == null || itemStack?.ItemConfig == null)
            {
                return false;
            }

            foreach (var item in inventory.Items)
            {
                if (item?.ItemStack?.CanStackWith(itemStack) == true && !item.ItemStack.IsFull)
                {
                    return true;
                }
            }

            foreach (var tile in inventory.Tiles.tiles)
            {
                if (CanPlaceAt(tile, inventory.Tiles, itemStack.Size))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanPlaceAt(Tile tile, Tiles tiles, Vector2Int size)
        {
            if (tile == null || tiles == null)
            {
                return false;
            }

            var itemTiles = tiles.GetTilesAround(tile.Index, size);
            return itemTiles.Count == size.x * size.y && itemTiles.All(currentTile => currentTile.IsFree);
        }

        private void MoveTarget(PopupTarget target, int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (target?.SlotModel?.ItemStack?.ItemConfig != null)
            {
                MoveSlotItem(target.SlotModel, count);
                return;
            }

            if (target?.InventoryItem?.ItemStack?.ItemConfig != null)
            {
                MoveInventoryItem(target.SourceInventory, target.InventoryItem, count);
            }
        }

        private void MoveSlotItem(SlotModel slotModel, int count)
        {
            var destinationInventory = GetOtherInventory(playerInventory);
            if (destinationInventory == null
             || slotModel?.ItemStack?.ItemConfig == null
             || !playerInventory.TryTakeFromSlot(slotModel, count, out var itemStack))
            {
                return;
            }

            var remainder = destinationInventory.TryAdd(itemStack);
            if (remainder != null)
            {
                RestoreSlotItem(slotModel, remainder);
                return;
            }

            if (slotModel.ItemType == ItemType.Backpack)
            {
                var droppedItems = playerInventory.RebuildInventoryFromCurrentBackpack();
                foreach (var droppedItem in droppedItems)
                {
                    inventoryHandController.Drop(droppedItem);
                }
            }
        }

        private void RestoreSlotItem(SlotModel slotModel, ItemStack itemStack)
        {
            if (slotModel == null || itemStack?.ItemConfig == null)
            {
                return;
            }

            playerInventory.TryPlaceInSlot(slotModel, itemStack, out var remainderStack, out var replacedStack);
            if (replacedStack != null)
            {
                var replacedRemainder = playerInventory.TryAdd(replacedStack);
                if (replacedRemainder != null)
                {
                    inventoryHandController.Drop(replacedRemainder);
                }
            }

            if (remainderStack != null)
            {
                var remainderAfterInventory = playerInventory.TryAdd(remainderStack);
                if (remainderAfterInventory != null)
                {
                    inventoryHandController.Drop(remainderAfterInventory);
                }
            }
        }

        private void MoveInventoryItem(IInventory sourceInventory, ItemInInventory itemInInventory, int count)
        {
            if (sourceInventory == null || itemInInventory?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            var destinationInventory = GetOtherInventory(sourceInventory);
            if (destinationInventory == null)
            {
                return;
            }

            var itemConfig = itemInInventory.ItemConfig;
            var isRotated = itemInInventory.ItemStack.IsRotated;
            var originalPosition = itemInInventory.Position;
            var originalCount = itemInInventory.Count;
            var moveCount = Mathf.Clamp(count, 1, originalCount);
            var movingStack = new ItemStack(itemConfig, moveCount, isRotated);
            var remainder = destinationInventory.TryAdd(movingStack);
            var movedCount = moveCount - (remainder?.Count ?? 0);
            if (movedCount <= 0)
            {
                return;
            }

            sourceInventory.Remove(itemInInventory);
            var remainingSourceCount = originalCount - movedCount;
            if (remainingSourceCount > 0)
            {
                sourceInventory.Add(new ItemStack(itemConfig, remainingSourceCount, isRotated), originalPosition);
            }
        }

        private bool TryGetPopupTarget(Vector2 screenPoint, out PopupTarget target)
        {
            var eventCamera = GetEventCamera();
            for (var i = popupTargets.Count - 1; i >= 0; i--)
            {
                var currentTarget = popupTargets[i];
                if (currentTarget?.Rect == null)
                {
                    continue;
                }

                if (!RectTransformUtility.RectangleContainsScreenPoint(currentTarget.Rect, screenPoint, eventCamera))
                {
                    continue;
                }

                target = currentTarget;
                return true;
            }

            if (TryGetGridPopupTarget(out target))
            {
                return true;
            }

            target = null;
            return false;
        }

        private bool TryGetGridPopupTarget(out PopupTarget target)
        {
            target = null;
            if (!InventoryTilePointerHandler.TryGetHovered(out var hoveredInventory, out var hoveredTile)
             || hoveredTile?.ItemInInventory?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            target = new PopupTarget
            {
                SourceInventory = hoveredInventory,
                InventoryItem = hoveredTile.ItemInInventory
            };
            return true;
        }

        private static Tiles GetTiles(IInventory inventory)
        {
            return (inventory as ITiledInventory)?.Tiles;
        }

        private Camera GetEventCamera() => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        private void ClosePopup()
        {
            if (popupRect == null)
            {
                popupOpenMode = PopupOpenMode.None;
                return;
            }

            UnityEngine.Object.Destroy(popupRect.gameObject);
            popupRect = null;
            popupOpenMode = PopupOpenMode.None;
        }

        public override void Hide()
        {
            bloodScreenController?.Dispose();
            bloodScreenController = null;

            heartbeatPulse?.Dispose();
            heartbeatPulse = null;

            redrawDisposables.Clear();
            itemRects.Clear();
            itemGrabRects.Clear();
            popupTargets.Clear();
            inventoryViews.Clear();
            lastGridSizes.Clear();
            inventoryScrollRects.Clear();
            ClosePopup();
            ResetHoverPopupState();

            if (contentRect)
            {
                UnityEngine.Object.Destroy(contentRect.gameObject);
            }

            if (handSlotRect)
            {
                UnityEngine.Object.Destroy(handSlotRect.gameObject);
            }

            contentRect = null;
            rightRect = null;
            leftRect = null;
            rightInfoAboutPlayer = null;
            leftInfoAboutPlayer = null;
            rightInfoAboutInventory = null;
            leftInfoAboutInventory = null;
            slotsViewContainer = null;
            bloodScreen = null;
            playerInventoryView = null;
            targetInventoryView = null;
            handSlotRect = null;
            popupRect = null;
            popupParentRect = null;
            Current = null;
        }
    }
}
