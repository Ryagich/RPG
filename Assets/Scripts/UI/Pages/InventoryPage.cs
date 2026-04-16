using System.Collections.Generic;
using Colors;
using Inventory.Grid;
using Inventory.Inventories;
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
using Money;
using Stats;
using UI.UIElements;
using CharacterInfo = Character.CharacterInfo;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class InventoryPage : BasePage, ITickable, IInventoryInteractionPage
    {
        public override PageType Type { get; } = PageType.Inventory;
        public static InventoryPage Current { get; private set; }
        public static IInventoryInteractionPage CurrentInteractionPage => Current;
        
        private readonly UIConfig uiConfig;
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly StatFiller hpFiller;
        private readonly LocalizationConfig localizationConfig;
        private readonly ColorsConfig colorsConfig;
        private readonly PlayerInventory playerInventory;
        private readonly MoneyStorage playerMoneyStorage;
        private readonly CharacterInfo characterInfo;
        private readonly Canvas canvas;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;

        private RectTransform contentRect = null!;
        private StatsHolder statsHolder = null!;
        private RectTransform rightRect = null!;
        private InfoAboutPlayer infoAboutPlayer = null!;
        private InfoAboutInventory infoAboutInventory = null!;
        private SlotsViewContainer slotsViewContainer = null!;
        private Inventory.InventoryView inventoryView = null!;
        private RectTransform handSlotRect = null!;
        private readonly CompositeDisposable redrawDisposables = new();
        private ScrollRect inventoryScrollRect = null!;
        private readonly List<RectTransform> itemRects = new();
        private readonly List<RectTransform> itemGrabRects = new();
        private Vector2 handGrabOffset;
        private BeatingHeart beatingHeart;

        private ItemConfig lastHelmItemConfig;
        private ItemConfig lastBodyItemConfig;
        private ItemConfig lastBackpackItemConfig;
        private Vector2Int lastGridSize = new(-1, -1);
        
        public InventoryPage
            (
                UIConfig uiConfig,
                StatsConfig statsConfig,
                StatsController statsController,
                StatFiller hpFiller,
                LocalizationConfig localizationConfig,
                ColorsConfig colorsConfig,
                Canvas canvas,
                PlayerInventory playerInventory,
                MoneyStorage playerMoneyStorage,
                CharacterInfo characterInfo,
                IObjectResolver resolver
            )
        {
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            this.hpFiller = hpFiller;
            this.localizationConfig = localizationConfig;
            this.colorsConfig = colorsConfig;
            this.canvas = canvas;
            this.playerInventory = playerInventory;
            this.playerMoneyStorage = playerMoneyStorage;
            this.characterInfo = characterInfo;
            this.resolver = resolver;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            Current = this;
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";
            statsHolder = resolver.Instantiate(uiConfig.StatsHolder, contentRect);
            statsHolder.name = $"{uiConfig.StatsHolder.name} | {Type}";

            rightRect = resolver.Instantiate(uiConfig.RightSection, contentRect);
            slotsViewContainer = resolver.Instantiate(uiConfig.CenterSection, contentRect);
            
            infoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            infoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            inventoryView = resolver.Instantiate(uiConfig.InventoryView, rightRect);
            inventoryScrollRect = inventoryView.GetComponent<ScrollRect>();
            PageUiUtilities.FillInfoAboutPlayer(infoAboutPlayer, characterInfo, playerMoneyStorage);
            
            DrawTiles();

            playerInventory.Changed
                           .Subscribe(_ => ReDraw())
                           .AddTo(redrawDisposables);
            playerInventory.HandSlot
                           .Subscribe(_ => ReDraw())
                           .AddTo(redrawDisposables);

            hpFiller.Current
                    .Subscribe(current => ApplyHpFill(GetNormalizedHp(current)))
                    .AddTo(redrawDisposables);
            beatingHeart = new BeatingHeart(statsConfig, statsController.Hp, hpFiller, statsHolder.HPHolder);

            ReDraw();
        }

        public void Tick()
        {
            if (handSlotRect)
            {
                UpdateHandSlotPosition();
            }
            if (HaveSlotsChanged())
            {
                ReDraw();
            }
        }

        public void ReDraw()
        {
            if (!inventoryView)
            {
                return;
            }

            EnsureTilesMatchInventorySize();
            UpdateInventoryScrollState();
            itemRects.Clear();
            itemGrabRects.Clear();
            PageUiUtilities.ClearChildren(inventoryView.ContentForItems);
            DrawItems(inventoryView);
            DrawSlotItems();
            DrawHandSlot();
            UpdateInventoryInfo();
            UpdatePlayerInfo();
            CacheSlotItems();
        }

        private void UpdatePlayerInfo()
        {
            PageUiUtilities.FillInfoAboutPlayer(infoAboutPlayer, characterInfo, playerMoneyStorage);
        }

        private void UpdateInventoryInfo()
        {
            var currentWeight = PageUiUtilities.GetItemsWeight(playerInventory)
                              + PageUiUtilities.GetSlotsWeight(playerInventory.HelmSlot, 
                                                               playerInventory.BodySlot, 
                                                               playerInventory.BackpackSlot, 
                                                               playerInventory.HandSlot.Value);
            PageUiUtilities.FillInfoAboutInventory(infoAboutInventory, localizationConfig, colorsConfig, currentWeight, playerInventory.MaxWeight);
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
        
        public bool TryGetPlacementTile(Vector2 screenPoint, IInventory inventory, out Tile tile)
        {
            tile = null;
            return inventory == playerInventory && TryGetPlacementTile(screenPoint, out tile);
        }

        private void UpdateInventoryScrollState()
        {
            if (!inventoryScrollRect)
            {
                return;
            }

            inventoryScrollRect.enabled = playerInventory.HandSlot.Value?.ItemStack == null;
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
            if (handItemStack?.ItemConfig == null || inventoryView == null)
            {
                return null;
            }

            var gridLayoutGroup = inventoryView.ContentForTiles.GetComponent<GridLayoutGroup>();
            return gridLayoutGroup == null
                ? null
                : PageUiUtilities.GetItemGrabSize(gridLayoutGroup, handItemStack.ItemConfig.Size);
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
        public bool TryGetPlacementTile(Vector2 screenPoint, out Tile tile)
        {
            tile = null;
            if (!TryGetPlacementCell(screenPoint, out var placementCell))
            {
                return false;
            }

            return playerInventory.Tiles.TryGetTile(placementCell.x, placementCell.y, out tile);
        }

        private bool TryGetSnappedHandPosition(Vector2 screenPoint, Camera eventCamera, RectTransform dragParentRect, out Vector2 snappedPosition)
        {
            snappedPosition = Vector2.zero;
            if (TryGetSnappedPositionInSlot(screenPoint, eventCamera, dragParentRect, out var slotSnappedPosition))
            {
                snappedPosition = slotSnappedPosition;
                return true;
            }
            if (!TryGetSnappedPositionInGridLocal(screenPoint, out var snappedLocalPosition))
            {
                return false;
            }

            var snappedWorldPosition = inventoryView.ContentForTiles.TransformPoint(snappedLocalPosition);
            var snappedScreenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, snappedWorldPosition);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(dragParentRect, snappedScreenPosition, eventCamera, out snappedPosition);
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

            if (TryGetSlotUnderPointer(slotsViewContainer.BodySlot, playerInventory.BodySlot, screenPoint, handItemType, out slotModel))
            {
                return true;
            }

            return TryGetSlotUnderPointer(slotsViewContainer.BackpackSlot, playerInventory.BackpackSlot, screenPoint, handItemType, out slotModel);
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

        public bool IsInRightOrCenterSection(Vector2 screenPoint)
        {
            return IsInPlayerSections(screenPoint);
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

        private void CacheSlotItems()
        {
            lastHelmItemConfig = playerInventory.HelmSlot.ItemConfig;
            lastBodyItemConfig = playerInventory.BodySlot.ItemConfig;
            lastBackpackItemConfig = playerInventory.BackpackSlot.ItemConfig;
        }
        
        private bool TryGetPlacementCell(Vector2 screenPoint, out Vector2Int placementCell)
        {
            placementCell = default;
            if (!TryGetCursorCellAndLayout(screenPoint, out var cursorCell, out var gridLayoutGroup))
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

        private bool TryGetSnappedPositionInGridLocal(Vector2 screenPoint, out Vector3 snappedLocalPosition)
        {
            snappedLocalPosition = Vector3.zero;
            if (!TryGetPlacementCell(screenPoint, out var placementCell))
            {
                return false;
            }

            var handItemConfig = playerInventory.HandSlot.Value?.ItemConfig;
            var gridLayoutGroup = inventoryView.ContentForTiles.GetComponent<GridLayoutGroup>();
            if (handItemConfig == null || gridLayoutGroup == null)
            {
                return false;
            }
            
            var gridWidth = playerInventory.Tiles.tiles.GetLength(0);
            var gridHeight = playerInventory.Tiles.tiles.GetLength(1);
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

        private bool TryGetCursorCellAndLayout(Vector2 screenPoint, out Vector2Int cursorCell, out GridLayoutGroup gridLayoutGroup)
        {
            cursorCell = default;
            gridLayoutGroup = null;
            if (!inventoryView)
            {
                return false;
            }

            var gridRect = inventoryView.ContentForTiles;
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
            var xFromLeft = localPoint.x + rect.width * gridRect.pivot.x;
            var yFromTop = rect.height * (1f - gridRect.pivot.y) - localPoint.y;
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
        
        private static Vector2 GetItemGrabSize(GridLayoutGroup gridLayoutGroup, Vector2Int itemSize)
        {
            return new Vector2(
                               itemSize.x * gridLayoutGroup.cellSize.x + (itemSize.x - 1) * gridLayoutGroup.spacing.x,
                               itemSize.y * gridLayoutGroup.cellSize.y + (itemSize.y - 1) * gridLayoutGroup.spacing.y);
        }

        private void DrawItems(Inventory.InventoryView inventory)
        {
            var gridLayoutGroup = inventory.ContentForTiles.GetComponent<GridLayoutGroup>();
            if (gridLayoutGroup == null)
            {
                return;
            }

            foreach (var item in playerInventory.Items)
            {
                var itemGrabSize = PageUiUtilities.GetItemGrabSize(gridLayoutGroup, item.ItemConfig.Size);
                var itemImageRect = PageUiUtilities.CreateItemImage(inventory.ContentForItems, item.ItemStack, "Item", itemGrabSize);
                itemRects.Add(itemImageRect);
                
                var itemGrabRectObject = new GameObject($"Item Grab [{item.ItemConfig.Id}]", typeof(RectTransform));
                var itemGrabRect = itemGrabRectObject.GetComponent<RectTransform>();
                itemGrabRect.SetParent(inventory.ContentForItems, false);
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
        
        private void DrawTiles()
        {
            if (!inventoryView)
            {
                return;
            }

            PageUiUtilities.ClearChildren(inventoryView.ContentForTiles);
            var gridWidth = playerInventory.Tiles.tiles.GetLength(0);
            var gridHeight = playerInventory.Tiles.tiles.GetLength(1);

            for (var y = 0; y < gridHeight; y++)
            for (var x = 0; x < gridWidth; x++)
            {
                var tile = resolver.Instantiate(uiConfig.Tile, inventoryView.ContentForTiles);
                tile.Initialize(playerInventory, playerInventory.Tiles.GetTile(x, y));
                tile.GetComponentInChildren<TMP_Text>().text = $"{x}:{y}";
            }

            lastGridSize = new Vector2Int(gridWidth, gridHeight);
        }

        private void EnsureTilesMatchInventorySize()
        {
            var currentSize = new Vector2Int(playerInventory.Tiles.tiles.GetLength(0), playerInventory.Tiles.tiles.GetLength(1));
            if (currentSize == lastGridSize)
            {
                return;
            }

            DrawTiles();
        }

        private Camera GetEventCamera() => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        
        public override void Hide()
        {
            beatingHeart?.Dispose();
            beatingHeart = null;

            redrawDisposables.Clear();
            itemRects.Clear();
            itemGrabRects.Clear();
            
            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }

            if (handSlotRect)
            {
                Object.Destroy(handSlotRect.gameObject);
            }

            contentRect = null;
            statsHolder = null;
            rightRect = null;
            infoAboutPlayer = null;
            infoAboutInventory = null;
            slotsViewContainer = null;
            inventoryView = null;
            inventoryScrollRect = null;
            handSlotRect = null;
            Current = null;
        }

        private void ApplyHpFill(float normalizedValue)
        {
            if (statsHolder?.HPHolder?.Fill == null)
            {
                return;
            }

            statsHolder.HPHolder.Fill.fillAmount = normalizedValue;
        }

        private float GetNormalizedHp(float current)
        {
            var maxHp = statsController.Hp.Max;
            return Mathf.Approximately(maxHp, 0f) ? 0f : current / maxHp;
        }
    }
}
