using System.Collections.Generic;
using Inventory;
using Inventory.Grid;
using Inventory.Slot;
using TMPro;
using UI.Configs;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using UnityEngine.InputSystem;
using Inventory.Item;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class InventoryPage : BasePage, ITickable
    {
        public override PageType Type { get; } = PageType.Inventory;
        public static InventoryPage Current { get; private set; }
        
        private readonly UIConfig uiConfig;
        private readonly PlayerInventory playerInventory;
        private readonly Canvas canvas;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;

        private RectTransform contentRect = null!;
        private RectTransform rightRect = null!;
        private SlotsViewContainer slotsViewContainer = null!;
        private Inventory.InventoryView inventoryView = null!;
        private RectTransform handSlotRect = null!;
        private readonly CompositeDisposable redrawDisposables = new();
        private ScrollRect inventoryScrollRect = null!;
        private readonly List<RectTransform> itemRects = new();
        private readonly List<RectTransform> itemGrabRects = new();
        private Vector2 handGrabOffset;

        private ItemConfig lastHelmItemConfig;
        private ItemConfig lastBodyItemConfig;
        private ItemConfig lastBackpackItemConfig;
        
        public InventoryPage
            (
                UIConfig uiConfig,
                Canvas canvas,
                PlayerInventory playerInventory,
                IObjectResolver resolver
            )
        {
            this.uiConfig = uiConfig;
            this.canvas = canvas;
            this.playerInventory = playerInventory;
            this.resolver = resolver;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            Current = this;
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            rightRect = resolver.Instantiate(uiConfig.RightSection, contentRect);
            slotsViewContainer = resolver.Instantiate(uiConfig.CenterSection, contentRect);
            
            var infoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            var infoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            inventoryView = resolver.Instantiate(uiConfig.InventoryView, rightRect);
            inventoryScrollRect = inventoryView.GetComponent<ScrollRect>();
            
            for (var y = 0; y < playerInventory.Tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < playerInventory.Tiles.tiles.GetLength(0); x++)
            {
                var tile = resolver.Instantiate(uiConfig.Tile, inventoryView.ContentForTiles);
                tile.Initialize(playerInventory, playerInventory.Tiles.GetTile(x, y));
                tile.GetComponentInChildren<TMP_Text>().text = $"{x}:{y}";
            }

            playerInventory.Items
                           .ObserveCountChanged()
                           .Subscribe(_ => ReDraw())
                           .AddTo(redrawDisposables);
            playerInventory.HandSlot
                           .Subscribe(_ => ReDraw())
                           .AddTo(redrawDisposables);

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

            UpdateInventoryScrollState();
            itemRects.Clear();
            itemGrabRects.Clear();
            ClearChildren(inventoryView.ContentForItems);
            DrawItems(inventoryView);
            DrawSlotItems();
            DrawHandSlot();
            CacheSlotItems();
        }
        
        public bool TryCaptureGrabOffset(Vector2 screenPoint)
        {
            var eventCamera = GetEventCamera();

            for (var i = itemRects.Count - 1; i >= 0; i--)
            {
                var itemRect = itemRects[i];
                if (!itemRect || !RectTransformUtility.RectangleContainsScreenPoint(itemRect, screenPoint, eventCamera))
                {
                    continue;
                }

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(itemRect, screenPoint, eventCamera, out var localPoint))
                {
                    continue;
                }

                handGrabOffset = localPoint;
                return true;
            }

            for (var i = itemGrabRects.Count - 1; i >= 0; i--)
            {
                var itemGrabRect = itemGrabRects[i];
                if (!itemGrabRect || !RectTransformUtility.RectangleContainsScreenPoint(itemGrabRect, screenPoint, eventCamera))
                {
                    continue;
                }

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(itemGrabRect, screenPoint, eventCamera, out var localPoint))
                {
                    continue;
                }

                handGrabOffset = localPoint;
                return true;
            }

            return false;
        }
        
        public void ResetGrabOffset()
        {
            handGrabOffset = Vector2.zero;
        }
        
        private void UpdateInventoryScrollState()
        {
            if (!inventoryScrollRect)
            {
                return;
            }

            inventoryScrollRect.enabled = playerInventory.HandSlot.Value?.ItemConfig == null;
        }

        private void DrawHandSlot()
        {
            if (handSlotRect)
            {
                Object.Destroy(handSlotRect.gameObject);
                handSlotRect = null;
            }

            var handItemConfig = playerInventory.HandSlot.Value?.ItemConfig;
            if (handItemConfig == null)
            {
                return;
            }

            var handItemObject = new GameObject($"Hand Item [{handItemConfig.Id}]", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handSlotRect = handItemObject.GetComponent<RectTransform>();
            handSlotRect.SetParent(canvasRect, false);
            handSlotRect.anchorMin = new Vector2(0.5f, 0.5f);
            handSlotRect.anchorMax = new Vector2(0.5f, 0.5f);
            handSlotRect.pivot = new Vector2(0.5f, 0.5f);
            handSlotRect.sizeDelta = handItemConfig.SizeInInventory;

            var handItemImage = handItemObject.GetComponent<Image>();
            handItemImage.sprite = handItemConfig.Icon;
            handItemImage.preserveAspect = true;
            handItemImage.raycastTarget = false;

            UpdateHandSlotPosition();
        }

        private void UpdateHandSlotPosition()
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            var pointerPosition = pointer.position.ReadValue();
            var dragParentRect = handSlotRect.parent as RectTransform;
            if (dragParentRect == null)
            {
                return;
            }

            var eventCamera = GetEventCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(dragParentRect, pointerPosition, eventCamera, out var localPoint))
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
        
        private void DrawSlotItems()
        {
            DrawSlotItem(slotsViewContainer.HeadSlot, playerInventory.HelmSlot);
            DrawSlotItem(slotsViewContainer.BodySlot, playerInventory.BodySlot);
            DrawSlotItem(slotsViewContainer.BackpackSlot, playerInventory.BackpackSlot);
        }

        private void DrawSlotItem(SlotView slotView, SlotModel slotModel)
        {
            if (!slotView)
            {
                return;
            }

            var slotRect = slotView.GetComponent<RectTransform>();
            if (!slotRect)
            {
                return;
            }

            ClearChildren(slotRect);
            if (slotModel?.ItemConfig == null)
            {
                return;
            }

            var itemImageObject = new GameObject($"Slot Item [{slotModel.ItemConfig.Id}]", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var itemImageRect = itemImageObject.GetComponent<RectTransform>();
            itemImageRect.SetParent(slotRect, false);
            itemImageRect.anchorMin = new Vector2(0.5f, 0.5f);
            itemImageRect.anchorMax = new Vector2(0.5f, 0.5f);
            itemImageRect.pivot = new Vector2(0.5f, 0.5f);
            itemImageRect.anchoredPosition = Vector2.zero;
            itemImageRect.sizeDelta = slotModel.ItemConfig.SizeInInventory;
            itemRects.Add(itemImageRect);
            itemGrabRects.Add(itemImageRect);

            var itemImage = itemImageObject.GetComponent<Image>();
            itemImage.sprite = slotModel.ItemConfig.Icon;
            itemImage.preserveAspect = true;
            itemImage.raycastTarget = false;
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

            if (!TryGetSlotRect(slotModel, out var slotRect))
            {
                return false;
            }

            var slotWorldPosition = slotRect.TransformPoint(Vector3.zero);
            var slotScreenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, slotWorldPosition);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(dragParentRect, slotScreenPosition, eventCamera, out snappedPosition);
        }

        private bool TryGetSlotRect(SlotModel slotModel, out RectTransform slotRect)
        {
            slotRect = null;
            if (slotModel == playerInventory.HelmSlot && slotsViewContainer.HeadSlot)
            {
                slotRect = slotsViewContainer.HeadSlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.BodySlot && slotsViewContainer.BodySlot)
            {
                slotRect = slotsViewContainer.BodySlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.BackpackSlot && slotsViewContainer.BackpackSlot)
            {
                slotRect = slotsViewContainer.BackpackSlot.GetComponent<RectTransform>();
            }

            return slotRect;
        }

        private bool TryGetSlotUnderPointer(SlotView slotView, SlotModel slotModel, Vector2 screenPoint, ItemType? requiredType, out SlotModel hoveredSlotModel)
        {
            hoveredSlotModel = null;
            if (!slotView)
            {
                return false;
            }

            var slotRect = slotView.GetComponent<RectTransform>();
            if (!slotRect)
            {
                return false;
            }

            if (requiredType.HasValue && slotModel.ItemType != requiredType.Value)
            {
                return false;
            }

            var eventCamera = GetEventCamera();
            if (!RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPoint, eventCamera))
            {
                return false;
            }

            hoveredSlotModel = slotModel;
            return true;
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

            var itemGrabSize = GetItemGrabSize(gridLayoutGroup, handItemConfig.Size);
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

        private static void ClearChildren(Transform parent)
        {
            var children = new List<GameObject>();
            foreach (Transform child in parent)
            {
                children.Add(child.gameObject);
            }

            foreach (var child in children)
            {
                Object.Destroy(child);
            }
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
                var itemImageObject = new GameObject($"Item [{item.ItemConfig.Id}]", typeof(RectTransform),
                                                     typeof(CanvasRenderer), typeof(Image));
                var itemImageRect = itemImageObject.GetComponent<RectTransform>();
                itemImageRect.SetParent(inventory.ContentForItems, false);
                itemImageRect.anchorMin = new Vector2(0, 1);
                itemImageRect.anchorMax = new Vector2(0, 1);
                itemImageRect.pivot = new Vector2(0.5f, 0.5f);
                itemImageRect.sizeDelta = item.ItemConfig.SizeInInventory;
                itemRects.Add(itemImageRect);
                
                var itemGrabRectObject = new GameObject($"Item Grab [{item.ItemConfig.Id}]", typeof(RectTransform));
                var itemGrabRect = itemGrabRectObject.GetComponent<RectTransform>();
                itemGrabRect.SetParent(inventory.ContentForItems, false);
                itemGrabRect.anchorMin = new Vector2(0, 1);
                itemGrabRect.anchorMax = new Vector2(0, 1);
                itemGrabRect.pivot = new Vector2(0.5f, 0.5f);
                itemGrabRect.sizeDelta = GetItemGrabSize(gridLayoutGroup, item.ItemConfig.Size);
                itemGrabRects.Add(itemGrabRect);
                
                var itemCenterPosition = item.Position.GetColumn(3);
                var itemAnchoredPosition = new Vector2(
                                                       gridLayoutGroup.padding.left
                                                     + (itemCenterPosition.x + 0.5f) * gridLayoutGroup.cellSize.x
                                                     + itemCenterPosition.x * gridLayoutGroup.spacing.x,
                                                       -(gridLayoutGroup.padding.top
                                                       + (itemCenterPosition.y + 0.5f) * gridLayoutGroup.cellSize.y
                                                       + itemCenterPosition.y * gridLayoutGroup.spacing.y));

                itemImageRect.anchoredPosition = itemAnchoredPosition;
                itemGrabRect.anchoredPosition = itemAnchoredPosition;

                var itemImage = itemImageObject.GetComponent<Image>();
                itemImage.sprite = item.ItemConfig.Icon;
                itemImage.preserveAspect = true;
                itemImage.raycastTarget = false;
            }
        }
        
        private Camera GetEventCamera() => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        
        public override void Hide()
        {
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
            rightRect = null;
            slotsViewContainer = null;
            inventoryView = null;
            inventoryScrollRect = null;
            handSlotRect = null;
        }
    }
}
