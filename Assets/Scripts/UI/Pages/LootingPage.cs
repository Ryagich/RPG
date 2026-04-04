using System.Collections.Generic;
using System.Linq;
using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Looting;
using Inventory.Slot;
using TMPro;
using UI.Configs;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Inventory.Item;
using Localization;
using UI.Inventory;
using UI.UIElements;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class LootingPage : BasePage, ITickable, IInventoryInteractionPage
    {
        public override PageType Type { get; } = PageType.Looting;
        public static LootingPage Current { get; private set; }
        public static IInventoryInteractionPage CurrentInteractionPage => Current;

        private readonly UIConfig uiConfig;
        private readonly LocalizationConfig localizationConfig;
        private readonly PlayerInventory playerInventory;
        private readonly Character.CharacterInfo playerCharacterInfo;
        private readonly LootingContext lootingContext;
        private readonly Canvas canvas;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;

        private RectTransform contentRect = null!;
        private RectTransform rightRect = null!;
        private RectTransform leftRect = null!;
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
        private readonly Dictionary<IInventory, InventoryView> inventoryViews = new();
        private readonly Dictionary<IInventory, Vector2Int> lastGridSizes = new();
        private Vector2 handGrabOffset;

        private ItemConfig lastHelmItemConfig;
        private ItemConfig lastBodyItemConfig;
        private ItemConfig lastBackpackItemConfig;

        public LootingPage
            (
                UIConfig uiConfig,
                LocalizationConfig localizationConfig,
                Canvas canvas,
                PlayerInventory playerInventory,
                Character.CharacterInfo playerCharacterInfo,
                LootingContext lootingContext,
                IObjectResolver resolver
            )
        {
            this.uiConfig = uiConfig;
            this.localizationConfig = localizationConfig;
            this.canvas = canvas;
            this.playerInventory = playerInventory;
            this.playerCharacterInfo = playerCharacterInfo;
            this.lootingContext = lootingContext;
            this.resolver = resolver;

            canvasRect = canvas.GetComponent<RectTransform>();
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

            leftRect = resolver.Instantiate(uiConfig.LeftSection, contentRect);
            slotsViewContainer = resolver.Instantiate(uiConfig.CenterSection, contentRect);
            rightRect = resolver.Instantiate(uiConfig.RightSection, contentRect);
            
            var rightInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            rightInfoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            playerInventoryView = resolver.Instantiate(uiConfig.InventoryView, rightRect);
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo);

            var leftInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, leftRect);
            leftInfoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, leftRect);
            targetInventoryView = resolver.Instantiate(uiConfig.InventoryView, leftRect);
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, lootingContext.CurrentTargetCharacterInfo);
            
            inventoryViews.Clear();
            inventoryViews[playerInventory] = playerInventoryView;
            inventoryViews[targetInventory] = targetInventoryView;
            CacheInventoryScrollRects();

            DrawTiles(playerInventory);
            DrawTiles(targetInventory);

            playerInventory.Items.ObserveCountChanged().Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            targetInventory.Items.ObserveCountChanged().Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            playerInventory.HandSlot.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);

            ReDraw();
        }
        
        public void Tick()
        {
            if (handSlotRect)
            {
                UpdateHandSlotPosition();
            }

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

            EnsureTilesMatchInventorySize();
            UpdateInventoryScrollState();
            itemRects.Clear();
            itemGrabRects.Clear();

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
            CacheSlotItems();
        }

        private void UpdateInventoryInfo()
        {
            var playerWeight = PageUiUtilities.GetItemsWeight(playerInventory)
                             + PageUiUtilities.GetSlotsWeight(playerInventory.HelmSlot, playerInventory.BodySlot, playerInventory.BackpackSlot);
            PageUiUtilities.FillInfoAboutInventory(rightInfoAboutInventory, localizationConfig, playerWeight, playerInventory.MaxWeight);

            var targetInventory = lootingContext.CurrentTargetInventory;
            var targetWeight = PageUiUtilities.GetItemsWeight(targetInventory);
            PageUiUtilities.FillInfoAboutInventory(leftInfoAboutInventory, localizationConfig, targetWeight, targetInventory?.MaxWeight);
        }

        public bool TryCaptureGrabOffset(Vector2 screenPoint)
        {
            var eventCamera = GetEventCamera();
            return PageUiUtilities.TryCaptureGrabOffset(itemRects, itemGrabRects, screenPoint, eventCamera, out handGrabOffset);
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

            // ReSharper disable once Unity.NoNullPropagation
            var handItemType = playerInventory.HandSlot.Value?.ItemConfig?.ItemType;
            if (TryGetSlotUnderPointer(slotsViewContainer.HeadSlot, playerInventory.HelmSlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            if (TryGetSlotUnderPointer(slotsViewContainer.BodySlot, playerInventory.BodySlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            return TryGetSlotUnderPointer(slotsViewContainer.BackpackSlot, playerInventory.BackpackSlot, screenPoint, handItemType, out slotModel);
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
            var isDraggingItem = playerInventory.HandSlot.Value?.ItemConfig != null;
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
            handSlotRect = PageUiUtilities.DrawHandSlot(handSlotRect, canvasRect, playerInventory.HandSlot.Value?.ItemConfig);
            if (handSlotRect)
            {
                UpdateHandSlotPosition();
            }
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
                   || lastBodyItemConfig != playerInventory.BodySlot.ItemConfig
                   || lastBackpackItemConfig != playerInventory.BackpackSlot.ItemConfig;
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
            lastBodyItemConfig = playerInventory.BodySlot.ItemConfig;
            lastBackpackItemConfig = playerInventory.BackpackSlot.ItemConfig;
        }

        private bool TryGetPlacementCell(Vector2 screenPoint, IInventory inventory, out Vector2Int placementCell)
        {
            placementCell = default;
            if (!TryGetCursorCellAndLayout(screenPoint, inventory, out var cursorCell, out var gridLayoutGroup))
            {
                return false;
            }

            var handItemConfig = playerInventory.HandSlot.Value?.ItemConfig;
            if (handItemConfig == null)
            {
                return false;
            }

            var itemGrabSize = PageUiUtilities.GetItemGrabSize(gridLayoutGroup, handItemConfig.Size);
            var stepX = gridLayoutGroup.cellSize.x + gridLayoutGroup.spacing.x;
            var stepY = gridLayoutGroup.cellSize.y + gridLayoutGroup.spacing.y;
            if (stepX <= 0 || stepY <= 0)
            {
                return false;
            }

            var grabFromLeft = handGrabOffset.x + itemGrabSize.x * 0.5f;
            var grabFromTop = itemGrabSize.y * 0.5f - handGrabOffset.y;
            var grabOffsetX = Mathf.Clamp(Mathf.FloorToInt(grabFromLeft / stepX), 0, handItemConfig.Size.x - 1);
            var grabOffsetY = Mathf.Clamp(Mathf.FloorToInt(grabFromTop / stepY), 0, handItemConfig.Size.y - 1);

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

            var handItemConfig = playerInventory.HandSlot.Value?.ItemConfig;
            var gridLayoutGroup = view.ContentForTiles.GetComponent<GridLayoutGroup>();
            var tiles = GetTiles(inventory);
            if (handItemConfig == null || gridLayoutGroup == null || tiles == null)
            {
                return false;
            }

            var gridWidth = tiles.tiles.GetLength(0);
            var gridHeight = tiles.tiles.GetLength(1);
            var isFullyInsideGrid =
                placementCell.x >= 0
                && placementCell.y >= 0
                && placementCell.x + handItemConfig.Size.x <= gridWidth
                && placementCell.y + handItemConfig.Size.y <= gridHeight;
            if (!isFullyInsideGrid)
            {
                return false;
            }

            var snappedAnchoredPosition = new Vector2(
                gridLayoutGroup.padding.left
                + (placementCell.x + handItemConfig.Size.x * 0.5f) * gridLayoutGroup.cellSize.x
                + (placementCell.x + (handItemConfig.Size.x - 1) * 0.5f) * gridLayoutGroup.spacing.x,
                -(gridLayoutGroup.padding.top
                  + (placementCell.y + handItemConfig.Size.y * 0.5f) * gridLayoutGroup.cellSize.y
                  + (placementCell.y + (handItemConfig.Size.y - 1) * 0.5f) * gridLayoutGroup.spacing.y));

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
            DrawSlotItem(slotsViewContainer.BodySlot, playerInventory.BodySlot);
            DrawSlotItem(slotsViewContainer.BackpackSlot, playerInventory.BackpackSlot);
        }

        private void DrawSlotItem(SlotView slotView, SlotModel slotModel)
        {
            PageUiUtilities.DrawSlotItem(slotView, slotModel, itemRects, itemGrabRects);
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
                var itemImageRect = PageUiUtilities.CreateItemImage(inventoryView.ContentForItems, item.ItemConfig, "Item");
                itemRects.Add(itemImageRect);

                var itemGrabRectObject = new GameObject($"Item Grab [{item.ItemConfig.Id}]", typeof(RectTransform));
                var itemGrabRect = itemGrabRectObject.GetComponent<RectTransform>();
                itemGrabRect.SetParent(inventoryView.ContentForItems, false);
                itemGrabRect.anchorMin = new Vector2(0, 1);
                itemGrabRect.anchorMax = new Vector2(0, 1);
                itemGrabRect.pivot = new Vector2(0.5f, 0.5f);
                itemGrabRect.sizeDelta = PageUiUtilities.GetItemGrabSize(gridLayoutGroup, item.ItemConfig.Size);
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

        private static Tiles GetTiles(IInventory inventory)
        {
            return (inventory as ITiledInventory)?.Tiles;
        }

        private Camera GetEventCamera() => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        public override void Hide()
        {
            redrawDisposables.Clear();
            itemRects.Clear();
            itemGrabRects.Clear();
            inventoryViews.Clear();
            lastGridSizes.Clear();
            inventoryScrollRects.Clear();

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }

            if (handSlotRect)
            {
                Object.Destroy(handSlotRect.gameObject);
            }

            contentRect = null;
            rightRect = null;
            leftRect = null;
            rightInfoAboutInventory = null;
            leftInfoAboutInventory = null;
            slotsViewContainer = null;
            playerInventoryView = null;
            targetInventoryView = null;
            handSlotRect = null;
            Current = null;
        }
    }
}