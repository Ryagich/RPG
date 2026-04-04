using System.Collections.Generic;
using System.Linq;
using Colors;
using Dialogue;
using GameModes;
using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using MessagePipe;
using Messages;
using UI.Configs;
using UI.Inventory;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using TMPro;
using Localization;
using UI.UIElements;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class TradePage : BasePage, ITickable, IInventoryInteractionPage
    {
        public override PageType Type { get; } = PageType.Trade;
        public static TradePage Current { get; private set; }
        public static IInventoryInteractionPage CurrentInteractionPage => Current;

        private enum TradeSide
        {
            None,
            Player,
            Target
        }

        private readonly struct SellItemOrigin
        {
            public readonly IInventory SourceInventory;
            public readonly SlotModel SourceSlot;
            public readonly Matrix4x4 SourcePosition;

            public SellItemOrigin(IInventory sourceInventory, SlotModel sourceSlot, Matrix4x4 sourcePosition)
            {
                SourceInventory = sourceInventory;
                SourceSlot = sourceSlot;
                SourcePosition = sourcePosition;
            }
        }
        
        private readonly UIConfig uiConfig;
        private readonly LocalizationConfig localizationConfig;
        private readonly ColorsConfig colorsConfig;
        private readonly PlayerInventory playerInventory;
        private readonly Character.CharacterInfo playerCharacterInfo;
        private readonly DialogueContext dialogueContext;
        private readonly Canvas canvas;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect = null!;
        private RectTransform leftRect = null!;
        private RectTransform rightRect = null!;
        private InfoAboutInventory leftInfoAboutInventory = null!;
        private InfoAboutInventory rightInfoAboutInventory = null!;
        private SlotsViewContainer centerSection = null!;
        private InventoryView playerInventoryView = null!;
        private InventoryView targetInventoryView = null!;
        private InventoryView playerSellInventoryView = null!;
        private InventoryView targetSellInventoryView = null!;
        private SellInfo leftSellInfo = null!;
        private SellInfo rightSellInfo = null!;
        private RectTransform handSlotRect = null!;
        private readonly CompositeDisposable redrawDisposables = new();
        private readonly Dictionary<IInventory, InventoryView> inventoryViews = new();
        private readonly Dictionary<IInventory, Vector2Int> lastGridSizes = new();
        private readonly List<ScrollRect> inventoryScrollRects = new();
        private readonly List<RectTransform> itemRects = new();
        private readonly List<RectTransform> itemGrabRects = new();
        private readonly Dictionary<ItemConfig, Queue<SellItemOrigin>> playerSellOrigins = new();
        private readonly Dictionary<ItemConfig, Queue<SellItemOrigin>> targetSellOrigins = new();
        private TradeSellInventory playerSellInventory;
        private TradeSellInventory targetSellInventory;
        private IInventory dragSourceInventory;
        private SlotModel dragSourceSlot;
        private Vector2 handGrabOffset;
        private ItemConfig lastHelmItemConfig;
        private ItemConfig lastBodyItemConfig;
        private ItemConfig lastBackpackItemConfig;
        
        public TradePage
            (
                UIConfig uiConfig,
                LocalizationConfig localizationConfig,
                ColorsConfig colorsConfig,
                PlayerInventory playerInventory,
                Character.CharacterInfo playerCharacterInfo,
                DialogueContext dialogueContext,
                Canvas canvas,
                IObjectResolver resolver,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher
            )
        {
            this.uiConfig = uiConfig;
            this.localizationConfig = localizationConfig;
            this.colorsConfig = colorsConfig;
            this.playerInventory = playerInventory;
            this.playerCharacterInfo = playerCharacterInfo;
            this.dialogueContext = dialogueContext;
            this.canvas = canvas;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            if (dialogueContext.CurrentTarget == null)
            {
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                return;
            }
            
            var targetInventory = dialogueContext.CurrentTargetInventory;
            if (targetInventory == null)
            {
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
                return;
            }

            Current = this;
            playerSellInventory = new TradeSellInventory();
            targetSellInventory = new TradeSellInventory();
            playerSellOrigins.Clear();
            targetSellOrigins.Clear();

            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            leftRect = resolver.Instantiate(uiConfig.LeftSection, contentRect);
            centerSection = resolver.Instantiate(uiConfig.CenterSection, contentRect);
            rightRect = resolver.Instantiate(uiConfig.RightSection, contentRect);

            var leftInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, leftRect);
            leftInfoAboutInventory = leftInfoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, leftRect);
            leftSellInfo = resolver.Instantiate(uiConfig.SellInfo, leftRect);
            targetSellInventoryView = resolver.Instantiate(uiConfig.SellInventory, leftRect).GetComponent<InventoryView>();
            targetInventoryView = resolver.Instantiate(uiConfig.InventoryInTrading, leftRect).GetComponent<InventoryView>();
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, dialogueContext.CurrentTargetCharacterInfo);

            var rightInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            rightInfoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            rightSellInfo = resolver.Instantiate(uiConfig.SellInfo, rightRect);
            playerSellInventoryView = resolver.Instantiate(uiConfig.SellInventory, rightRect).GetComponent<InventoryView>();
            playerInventoryView = resolver.Instantiate(uiConfig.InventoryInTrading, rightRect).GetComponent<InventoryView>();
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo);

            inventoryViews.Clear();
            inventoryViews[playerInventory] = playerInventoryView;
            inventoryViews[targetInventory] = targetInventoryView;
            inventoryViews[playerSellInventory] = playerSellInventoryView;
            inventoryViews[targetSellInventory] = targetSellInventoryView;
            CacheInventoryScrollRects();

            foreach (var inventory in inventoryViews.Keys.ToArray())
            {
                DrawTiles(inventory);
            }

            playerInventory.Items.ObserveCountChanged().Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            targetInventory.Items.ObserveCountChanged().Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            playerSellInventory.Items.ObserveCountChanged().Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            targetSellInventory.Items.ObserveCountChanged().Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            playerInventory.HandSlot.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            
            // ReSharper disable once Unity.NoNullPropagation
            if (rightSellInfo?.TradeButton)
            {
                rightSellInfo.TradeButton.onClick.AddListener(CompletePlayerSell);
            }

            // ReSharper disable once Unity.NoNullPropagation
            if (leftSellInfo?.TradeButton)
            {
                leftSellInfo.TradeButton.onClick.AddListener(CompleteTargetSell);
            }

            var tradingExitButton = resolver.Instantiate(uiConfig.TradingExitButton, centerSection.transform);
            tradingExitButton.onClick.AddListener(ReturnToDialogue);
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
        
        public override void Hide()
        {
            ReturnItemsFromSellInventories();
            redrawDisposables.Clear();
            itemRects.Clear();
            itemGrabRects.Clear();
            inventoryViews.Clear();
            lastGridSizes.Clear();
            inventoryScrollRects.Clear();
            playerSellOrigins.Clear();
            targetSellOrigins.Clear();
            
            // ReSharper disable once Unity.NoNullPropagation
            if (rightSellInfo?.TradeButton)
            {
                rightSellInfo.TradeButton.onClick.RemoveListener(CompletePlayerSell);
            }

            // ReSharper disable once Unity.NoNullPropagation
            if (leftSellInfo?.TradeButton)
            {
                leftSellInfo.TradeButton.onClick.RemoveListener(CompleteTargetSell);
            }

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }

            if (handSlotRect)
            {
                Object.Destroy(handSlotRect.gameObject);
            }

            contentRect = null;
            leftRect = null;
            rightRect = null;
            leftInfoAboutInventory = null;
            rightInfoAboutInventory = null;
            centerSection = null;
            playerInventoryView = null;
            targetInventoryView = null;
            playerSellInventoryView = null;
            targetSellInventoryView = null;
            leftSellInfo = null;
            rightSellInfo = null;
            handSlotRect = null;
            playerSellInventory = null;
            targetSellInventory = null;
            dragSourceInventory = null;
            dragSourceSlot = null;
            Current = null;
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
            if (!centerSection)
            {
                return false;
            }

            // ReSharper disable once Unity.NoNullPropagation
            var handItemType = playerInventory.HandSlot.Value?.ItemConfig?.ItemType;
            var eventCamera = GetEventCamera();

            if (PageUiUtilities.TryGetSlotUnderPointer(centerSection.HeadSlot, playerInventory.HelmSlot, screenPoint, handItemType, eventCamera, out slotModel))
            {
                return true;
            }

            if (PageUiUtilities.TryGetSlotUnderPointer(centerSection.BodySlot, playerInventory.BodySlot, screenPoint, handItemType, eventCamera, out slotModel))
            {
                return true;
            }

            return PageUiUtilities.TryGetSlotUnderPointer(centerSection.BackpackSlot, playerInventory.BackpackSlot, screenPoint, handItemType, eventCamera, out slotModel);
        }

        public bool TryGetPlacementTile(Vector2 screenPoint, IInventory inventory, out Tile tile)
        {
            tile = null;
            if (inventory == null || !TryGetPlacementCell(screenPoint, inventory, out var placementCell))
            {
                return false;
            }

            var tiles = (inventory as ITiledInventory)?.Tiles;
            return tiles != null && tiles.TryGetTile(placementCell.x, placementCell.y, out tile);
        }

        public bool IsInPlayerSections(Vector2 screenPoint)
        {
            var eventCamera = GetEventCamera();
            if (rightRect && RectTransformUtility.RectangleContainsScreenPoint(rightRect, screenPoint, eventCamera))
            {
                return true;
            }

            var centerRect = centerSection ? centerSection.GetComponent<RectTransform>() : null;
            return centerRect && RectTransformUtility.RectangleContainsScreenPoint(centerRect, screenPoint, eventCamera);
        }

        public bool IsInTargetSection(Vector2 screenPoint)
        {
            return leftRect && RectTransformUtility.RectangleContainsScreenPoint(leftRect, screenPoint, GetEventCamera());
        }

        public bool IsSellInventory(IInventory inventory)
        {
            return inventory == playerSellInventory || inventory == targetSellInventory;
        }

        public void SetDragSource(IInventory sourceInventory, SlotModel sourceSlot)
        {
            dragSourceInventory = sourceInventory;
            dragSourceSlot = sourceSlot;
        }

        public void ClearDragSource()
        {
            dragSourceInventory = null;
            dragSourceSlot = null;
        }

        public bool TryAddToSourceSellInventory(ItemConfig itemConfig, IInventory sourceInventory, SlotModel sourceSlot, Matrix4x4 sourcePosition)
        {
            var sourceSide = ResolveSourceSide(sourceInventory, sourceSlot);
            var destinationSellInventory = sourceSide switch
                                           {
                                               TradeSide.Player => playerSellInventory,
                                               TradeSide.Target => targetSellInventory,
                                               _ => null
                                           };

            if (itemConfig == null || destinationSellInventory == null || !destinationSellInventory.TryAdd(itemConfig))
            {
                return false;
            }

            RegisterMoveIntoSell(itemConfig, destinationSellInventory, sourceInventory, sourceSlot, sourcePosition);
            return true;
        }

        public IInventory GetTargetInventory()
        {
            return dialogueContext.CurrentTargetInventory;
        }

        public bool CanMoveToInventory(IInventory fromInventory, SlotModel fromSlot, IInventory destinationInventory)
        {
            if (destinationInventory == null)
            {
                return false;
            }

            var sourceSide = ResolveSourceSide(fromInventory, fromSlot);
            var destinationSide = ResolveInventorySide(destinationInventory);
            return sourceSide != TradeSide.None && sourceSide == destinationSide;
        }

        public bool CanMoveToPlayerSlot(IInventory fromInventory, SlotModel fromSlot)
        {
            return ResolveSourceSide(fromInventory, fromSlot) == TradeSide.Player;
        }

        public void RegisterMoveIntoSell(ItemConfig itemConfig, IInventory destinationInventory, IInventory sourceInventory, SlotModel sourceSlot, Matrix4x4 sourcePosition)
        {
            if (itemConfig == null || !IsSellInventory(destinationInventory))
            {
                return;
            }

            if (IsSellInventory(sourceInventory))
            {
                return;
            }
            
            var queue = GetOriginDictionary(destinationInventory);
            if (!queue.TryGetValue(itemConfig, out var origins))
            {
                origins = new Queue<SellItemOrigin>();
                queue[itemConfig] = origins;
            }

            origins.Enqueue(new SellItemOrigin(sourceInventory, sourceSlot, sourcePosition));
        }

        public void ConsumeSellOriginIfAny(ItemConfig itemConfig, IInventory sourceInventory)
        {
            if (itemConfig == null || !IsSellInventory(sourceInventory))
            {
                return;
            }

            var dictionary = GetOriginDictionary(sourceInventory);
            if (!dictionary.TryGetValue(itemConfig, out var queue) || queue.Count == 0)
            {
                return;
            }

            queue.Dequeue();
            if (queue.Count == 0)
            {
                dictionary.Remove(itemConfig);
            }
        }

        private void ReDraw()
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
            UpdateSellInfo();
            CacheSlotItems();
        }
        
        private void UpdateInventoryInfo()
        {
            var targetInventory = dialogueContext.CurrentTargetInventory;
            var handWeight = playerInventory.HandSlot.Value?.ItemConfig?.Weight ?? 0f;
            var handSourceInventory = playerInventory.HandSourceInventory.Value;

            var playerWeight = PageUiUtilities.GetItemsWeight(playerInventory)
                             + PageUiUtilities.GetSlotsWeight(playerInventory.HelmSlot, playerInventory.BodySlot, playerInventory.BackpackSlot)
                             + PageUiUtilities.GetItemsWeight(playerSellInventory);
            if (handWeight > 0f && (handSourceInventory == playerInventory || handSourceInventory == playerSellInventory))
            {
                playerWeight += handWeight;
            }

            PageUiUtilities.FillInfoAboutInventory(rightInfoAboutInventory, localizationConfig, colorsConfig, playerWeight, playerInventory.MaxWeight);

            var targetWeight = PageUiUtilities.GetItemsWeight(targetInventory)
                             + PageUiUtilities.GetItemsWeight(targetSellInventory);
            if (handWeight > 0f && (handSourceInventory == targetInventory || handSourceInventory == targetSellInventory))
            {
                targetWeight += handWeight;
            }

            PageUiUtilities.FillInfoAboutInventory(leftInfoAboutInventory, localizationConfig, colorsConfig, targetWeight, targetInventory?.MaxWeight);
        }

        private void UpdateSellInfo()
        {
            UpdateSellInfoText(rightSellInfo, playerSellInventory);
            UpdateSellInfoText(leftSellInfo, targetSellInventory);
        }

        private void UpdateSellInfoText(SellInfo sellInfo, IInventory sellInventory)
        {
            if (!sellInfo.InfoText || sellInventory == null)
            {
                return;
            }

            var totalWeight = sellInventory.Items.Sum(item => item.ItemConfig.Weight);
            var handWeight = playerInventory.HandSlot.Value?.ItemConfig?.Weight ?? 0f;
            if (handWeight > 0f && playerInventory.HandSourceInventory.Value == sellInventory)
            {
                totalWeight += handWeight;
            }

            PageUiUtilities.FillSellInventoryWeightText(sellInfo.InfoText, localizationConfig, colorsConfig, totalWeight);
        }
        
        private void DrawTiles(IInventory inventory)
        {
            if (!inventoryViews.TryGetValue(inventory, out var view) || inventory is not ITiledInventory tiledInventory)
            {
                return;
            }
            PageUiUtilities.ClearChildren(view.ContentForTiles);
            var gridWidth = tiledInventory.Tiles.tiles.GetLength(0);
            var gridHeight = tiledInventory.Tiles.tiles.GetLength(1);

            for (var y = 0; y < gridHeight; y++)
            for (var x = 0; x < gridWidth; x++)
            {
                var tile = resolver.Instantiate(uiConfig.Tile, view.ContentForTiles);
                tile.Initialize(inventory, tiledInventory.Tiles.GetTile(x, y));
                tile.GetComponentInChildren<TMP_Text>().text = $"{x}:{y}";
            }
            
            lastGridSizes[inventory] = new Vector2Int(gridWidth, gridHeight);
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

        private void DrawSlotItems()
        {
            PageUiUtilities.DrawSlotItem(centerSection.HeadSlot, playerInventory.HelmSlot, itemRects, itemGrabRects);
            PageUiUtilities.DrawSlotItem(centerSection.BodySlot, playerInventory.BodySlot, itemRects, itemGrabRects);
            PageUiUtilities.DrawSlotItem(centerSection.BackpackSlot, playerInventory.BackpackSlot, itemRects, itemGrabRects);
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

            if (!CanMoveToInventory(dragSourceInventory, dragSourceSlot, hoveredInventory))
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
            if (handItemConfig == null
             || slotModel.ItemType != handItemConfig.ItemType
             || !CanMoveToPlayerSlot(dragSourceInventory, dragSourceSlot))
            {
                return false;
            }

            if (!PageUiUtilities.TryGetSlotRect(centerSection, playerInventory, slotModel, out var slotRect))
            {
                return false;
            }

            var slotWorldPosition = slotRect.TransformPoint(slotRect.rect.center);
            var slotScreenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, slotWorldPosition);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(dragParentRect, slotScreenPosition, eventCamera, out snappedPosition);
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
            var tiles = (inventory as ITiledInventory)?.Tiles;
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
        
        private bool HaveGridChanged()
        {
            foreach (var pair in inventoryViews)
            {
                if (pair.Key is not ITiledInventory tiledInventory)
                {
                    continue;
                }

                var currentSize = new Vector2Int(tiledInventory.Tiles.tiles.GetLength(0), tiledInventory.Tiles.tiles.GetLength(1));
                if (!lastGridSizes.TryGetValue(pair.Key, out var lastSize) || currentSize != lastSize)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureTilesMatchInventorySize()
        {
            foreach (var inventory in inventoryViews.Keys.ToArray())
            {
                if (inventory is not ITiledInventory tiledInventory)
                {
                    continue;
                }

                var currentSize = new Vector2Int(tiledInventory.Tiles.tiles.GetLength(0), tiledInventory.Tiles.tiles.GetLength(1));
                if (!lastGridSizes.TryGetValue(inventory, out var lastSize) || currentSize != lastSize)
                {
                    DrawTiles(inventory);
                }
            }
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

        private bool TryGetPlacementCell(Vector2 screenPoint, IInventory inventory, out Vector2Int placementCell)
        {
            placementCell = default;
            if (!inventoryViews.TryGetValue(inventory, out var view))
            {
                return false;
            }

            var gridRect = view.ContentForTiles;
            var eventCamera = GetEventCamera();
            if (!gridRect
             || !RectTransformUtility.RectangleContainsScreenPoint(gridRect, screenPoint, eventCamera)
             || !RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPoint, eventCamera, out var localPoint))
            {
                return false;
            }

            var gridLayoutGroup = gridRect.GetComponent<GridLayoutGroup>();
            var handItemConfig = playerInventory.HandSlot.Value?.ItemConfig;
            if (gridLayoutGroup == null || handItemConfig == null)
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
            var cursorCell = new Vector2Int(Mathf.FloorToInt(xInCells), Mathf.FloorToInt(yInCells));

            var itemGrabSize = PageUiUtilities.GetItemGrabSize(gridLayoutGroup, handItemConfig.Size);
            var grabFromLeft = handGrabOffset.x + itemGrabSize.x * 0.5f;
            var grabFromTop = itemGrabSize.y * 0.5f - handGrabOffset.y;
            var grabOffsetX = Mathf.Clamp(Mathf.FloorToInt(grabFromLeft / stepX), 0, handItemConfig.Size.x - 1);
            var grabOffsetY = Mathf.Clamp(Mathf.FloorToInt(grabFromTop / stepY), 0, handItemConfig.Size.y - 1);

            placementCell = new Vector2Int(cursorCell.x - grabOffsetX, cursorCell.y - grabOffsetY);
            return true;
        }

        private TradeSide ResolveSourceSide(IInventory fromInventory, SlotModel fromSlot)
        {
            if (fromSlot != null)
            {
                return TradeSide.Player;
            }

            return ResolveInventorySide(fromInventory);
        }

        private TradeSide ResolveInventorySide(IInventory inventory)
        {
            if (inventory == playerInventory || inventory == playerSellInventory)
            {
                return TradeSide.Player;
            }

            if (inventory == dialogueContext.CurrentTargetInventory || inventory == targetSellInventory)
            {
                return TradeSide.Target;
            }

            return TradeSide.None;
        }

        private Dictionary<ItemConfig, Queue<SellItemOrigin>> GetOriginDictionary(IInventory sellInventory)
        {
            return sellInventory == playerSellInventory ? playerSellOrigins : targetSellOrigins;
        }

        private void ReturnItemsFromSellInventories()
        {
            ReturnItemsFromSellInventory(playerSellInventory, playerInventory, playerSellOrigins);
            ReturnItemsFromSellInventory(targetSellInventory, dialogueContext.CurrentTargetInventory, targetSellOrigins);
        }

        private static void ReturnItemsFromSellInventory(TradeSellInventory sellInventory, IInventory defaultInventory, Dictionary<ItemConfig, Queue<SellItemOrigin>> origins)
        {
            if (sellInventory == null)
            {
                return;
            }

            var itemsToReturn = sellInventory.Items.Select(item => item.ItemConfig).ToList();
            foreach (var itemConfig in itemsToReturn)
            {
                if (!TryTakeOne(sellInventory, itemConfig))
                {
                    continue;
                }

                if (!TryReturnToOrigin(itemConfig, origins) && (defaultInventory == null || !defaultInventory.TryAdd(itemConfig)))
                {
                    // ignore
                }
            }

            origins.Clear();
        }

        private static bool TryReturnToOrigin(ItemConfig itemConfig, Dictionary<ItemConfig, Queue<SellItemOrigin>> origins)
        {
            if (!origins.TryGetValue(itemConfig, out var queue) || queue.Count == 0)
            {
                return false;
            }

            var origin = queue.Dequeue();
            if (queue.Count == 0)
            {
                origins.Remove(itemConfig);
            }

            if (origin.SourceSlot != null)
            {
                if (origin.SourceSlot.ItemConfig == null)
                {
                    origin.SourceSlot.ItemConfig = itemConfig;
                    return true;
                }

                return origin.SourceInventory != null && origin.SourceInventory.TryAdd(itemConfig);
            }

            if (origin.SourceInventory == null)
            {
                return false;
            }

            if (origin.SourceInventory is ITiledInventory tiledInventory)
            {
                var center = origin.SourcePosition.GetColumn(3);
                var startPosition = new Vector2Int(Mathf.RoundToInt(center.x - (itemConfig.Size.x - 1) * 0.5f), Mathf.RoundToInt(center.y - (itemConfig.Size.y - 1) * 0.5f));
                if (tiledInventory.Tiles.TryGetTile(startPosition.x, startPosition.y, out var tile) && origin.SourceInventory.TryAdd(itemConfig, tile))
                {
                    return true;
                }
            }

            return origin.SourceInventory.TryAdd(itemConfig);
        }

        private static bool TryTakeOne(IInventory inventory, ItemConfig itemConfig)
        {
            var item = inventory.Items.FirstOrDefault(current => current.ItemConfig == itemConfig);
            if (item == null || item.Tiles == null || item.Tiles.Count == 0)
            {
                return false;
            }

            return inventory.TryGet(item.Tiles[0], out _);
        }

        private void CompletePlayerSell()
        {
            TransferSellInventory(playerSellInventory, dialogueContext.CurrentTargetInventory, playerSellOrigins);
        }

        private void CompleteTargetSell()
        {
            TransferSellInventory(targetSellInventory, playerInventory, targetSellOrigins);
        }

        private static void TransferSellInventory(TradeSellInventory sourceSellInventory, IInventory destinationInventory, Dictionary<ItemConfig, Queue<SellItemOrigin>> origins)
        {
            if (sourceSellInventory == null || destinationInventory == null)
            {
                return;
            }

            var items = sourceSellInventory.Items.Select(item => item.ItemConfig).ToList();
            foreach (var itemConfig in items)
            {
                if (!destinationInventory.TryAdd(itemConfig))
                {
                    continue;
                }

                TryTakeOne(sourceSellInventory, itemConfig);
                if (origins.TryGetValue(itemConfig, out var queue) && queue.Count > 0)
                {
                    queue.Dequeue();
                    if (queue.Count == 0)
                    {
                        origins.Remove(itemConfig);
                    }
                }
            }
        }
        
        private void ReturnToDialogue()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }

        private Camera GetEventCamera() => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }
}