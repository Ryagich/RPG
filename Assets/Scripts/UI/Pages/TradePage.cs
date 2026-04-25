using System;
using System.Collections.Generic;
using System.Linq;
using Colors;
using Dialogue;
using GameModes;
using Inventory;
using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using Localization;
using MessagePipe;
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
using Object = UnityEngine.Object;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class TradePage : BasePage, ITickable, IInventoryInteractionPage
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
            public readonly int Count;

            public SellItemOrigin(IInventory sourceInventory, SlotModel sourceSlot, Matrix4x4 sourcePosition, int count)
            {
                SourceInventory = sourceInventory;
                SourceSlot = sourceSlot;
                SourcePosition = sourcePosition;
                Count = count;
            }
        }

        private readonly struct SellItemKey : IEquatable<SellItemKey>
        {
            public readonly ItemConfig ItemConfig;
            public readonly bool IsRotated;

            public SellItemKey(ItemConfig itemConfig, bool isRotated)
            {
                ItemConfig = itemConfig;
                IsRotated = isRotated;
            }

            public bool Equals(SellItemKey other)
            {
                return ItemConfig == other.ItemConfig && IsRotated == other.IsRotated;
            }

            public override bool Equals(object obj)
            {
                return obj is SellItemKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((ItemConfig != null ? ItemConfig.GetHashCode() : 0) * 397) ^ IsRotated.GetHashCode();
                }
            }
        }

        public override PageType Type { get; } = PageType.Trade;
        public static TradePage Current { get; private set; }
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
        private readonly DialogueContext dialogueContext;
        private readonly StatsController statsController;
        private readonly Canvas canvas;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect = null!;
        private RectTransform leftRect = null!;
        private RectTransform rightRect = null!;
        private InfoAboutPlayer leftInfoAboutPlayer = null!;
        private InfoAboutPlayer rightInfoAboutPlayer = null!;
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
        private RectTransform popupRect;
        private RectTransform popupParentRect;
        private readonly CompositeDisposable redrawDisposables = new();
        private readonly Dictionary<IInventory, InventoryView> inventoryViews = new();
        private readonly Dictionary<IInventory, Vector2Int> lastGridSizes = new();
        private readonly List<ScrollRect> inventoryScrollRects = new();
        private readonly List<RectTransform> itemRects = new();
        private readonly List<RectTransform> itemGrabRects = new();
        private readonly List<PopupTarget> popupTargets = new();
        private readonly Dictionary<SellItemKey, Queue<SellItemOrigin>> playerSellOrigins = new();
        private readonly Dictionary<SellItemKey, Queue<SellItemOrigin>> targetSellOrigins = new();
        private HeartbeatPulse heartbeatPulse;
        private BloodScreenController bloodScreenController;
        private Image bloodScreen;
        private TradeSellInventory playerSellInventory;
        private TradeSellInventory targetSellInventory;
        private IInventory dragSourceInventory;
        private SlotModel dragSourceSlot;
        private Vector2 handGrabOffset;
        private PopupTarget hoverPopupTarget;
        private float hoverPopupElapsed;
        private PopupOpenMode popupOpenMode;
        private ItemConfig lastHelmItemConfig;
        private ItemConfig lastBodyItemConfig;
        private ItemConfig lastBackpackItemConfig;
        private ItemConfig lastFastSlot1ItemConfig;
        private ItemConfig lastFastSlot2ItemConfig;
        private ItemConfig lastFastSlot3ItemConfig;
        private ItemConfig lastFastSlot4ItemConfig;

        public TradePage(
            UIConfig uiConfig,
            StatsConfig statsConfig,
            StatFillers statFillers,
            LocalizationConfig localizationConfig,
            ColorsConfig colorsConfig,
            StatIconsConfig statIconsConfig,
            PlayerInventory playerInventory,
            InventoryHandController inventoryHandController,
            MoneyStorage playerMoneyStorage,
            Character.CharacterInfo playerCharacterInfo,
            DialogueContext dialogueContext,
            StatsController statsController,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher)
        {
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            hpFiller = statFillers.Get(StatType.Hp);
            this.localizationConfig = localizationConfig;
            this.colorsConfig = colorsConfig;
            this.statIconsConfig = statIconsConfig;
            this.playerInventory = playerInventory;
            this.inventoryHandController = inventoryHandController;
            this.playerMoneyStorage = playerMoneyStorage;
            this.playerCharacterInfo = playerCharacterInfo;
            this.dialogueContext = dialogueContext;
            this.statsController = statsController;
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
            popupParentRect = contentRect;

            leftRect = resolver.Instantiate(uiConfig.LeftSection, contentRect);
            centerSection = resolver.Instantiate(uiConfig.CenterSection, contentRect);
            PageUiUtilities.FillSlotsViewContainerStats(centerSection, statsController);
            rightRect = resolver.Instantiate(uiConfig.RightSection, contentRect);

            leftInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, leftRect);
            leftInfoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, leftRect);
            leftSellInfo = resolver.Instantiate(uiConfig.SellInfo, leftRect);
            targetSellInventoryView = resolver.Instantiate(uiConfig.SellInventory, leftRect).GetComponent<InventoryView>();
            targetInventoryView = resolver.Instantiate(uiConfig.InventoryInTrading, leftRect).GetComponent<InventoryView>();
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTargetMoneyStorage);

            rightInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            rightInfoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            rightSellInfo = resolver.Instantiate(uiConfig.SellInfo, rightRect);
            playerSellInventoryView = resolver.Instantiate(uiConfig.SellInventory, rightRect).GetComponent<InventoryView>();
            playerInventoryView = resolver.Instantiate(uiConfig.InventoryInTrading, rightRect).GetComponent<InventoryView>();
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo, playerMoneyStorage);

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

            bloodScreen = PageUiUtilities.CreateBloodScreen(uiConfig, resolver, contentRect, Type);
            heartbeatPulse = new HeartbeatPulse(statsConfig, statsController.Hp, hpFiller);
            bloodScreenController = new BloodScreenController(statsConfig, statsController.Hp, hpFiller, heartbeatPulse, bloodScreen);

            playerInventory.Changed.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            targetInventory.Changed.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            playerSellInventory.Changed.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            targetSellInventory.Changed.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            playerInventory.HandSlot.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            playerMoneyStorage.CurrentMoney.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            dialogueContext.CurrentTargetMoneyStorage?.CurrentMoney.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            statsController.Changed
                .Subscribe(_ => PageUiUtilities.FillSlotsViewContainerStats(centerSection, statsController))
                .AddTo(redrawDisposables);

            if (rightSellInfo?.TradeButton)
            {
                rightSellInfo.TradeButton.onClick.AddListener(CompletePlayerSell);
            }

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

            HandleHoverPopup();

            if (HaveSlotsChanged() || HaveGridChanged())
            {
                ReDraw();
            }
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

        public override void Hide()
        {
            bloodScreenController?.Dispose();
            bloodScreenController = null;

            heartbeatPulse?.Dispose();
            heartbeatPulse = null;

            ReturnItemsFromSellInventories();
            redrawDisposables.Clear();
            itemRects.Clear();
            itemGrabRects.Clear();
            popupTargets.Clear();
            inventoryViews.Clear();
            lastGridSizes.Clear();
            inventoryScrollRects.Clear();
            playerSellOrigins.Clear();
            targetSellOrigins.Clear();
            ClosePopup();
            ResetHoverPopupState();

            if (rightSellInfo?.TradeButton)
            {
                rightSellInfo.TradeButton.onClick.RemoveListener(CompletePlayerSell);
            }

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
            leftInfoAboutPlayer = null;
            rightInfoAboutPlayer = null;
            leftInfoAboutInventory = null;
            rightInfoAboutInventory = null;
            centerSection = null;
            bloodScreen = null;
            playerInventoryView = null;
            targetInventoryView = null;
            playerSellInventoryView = null;
            targetSellInventoryView = null;
            leftSellInfo = null;
            rightSellInfo = null;
            handSlotRect = null;
            popupRect = null;
            popupParentRect = null;
            playerSellInventory = null;
            targetSellInventory = null;
            dragSourceInventory = null;
            dragSourceSlot = null;
            Current = null;
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
            if (!centerSection)
            {
                return false;
            }

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

        public bool TryGetHoveredFastSlot(Vector2 screenPoint, out FastSlotModel fastSlotModel)
        {
            fastSlotModel = null;
            if (!centerSection
             || playerInventory.HandSlot.Value?.ItemConfig?.ItemType != ItemType.Usable
             || !CanMoveToPlayerSlot(dragSourceInventory, dragSourceSlot))
            {
                return false;
            }

            var eventCamera = GetEventCamera();
            return PageUiUtilities.TryGetFastSlotUnderPointer(centerSection.FastSlot1, playerInventory.FastSlot1, screenPoint, eventCamera, out fastSlotModel)
                   || PageUiUtilities.TryGetFastSlotUnderPointer(centerSection.FastSlot2, playerInventory.FastSlot2, screenPoint, eventCamera, out fastSlotModel)
                   || PageUiUtilities.TryGetFastSlotUnderPointer(centerSection.FastSlot3, playerInventory.FastSlot3, screenPoint, eventCamera, out fastSlotModel)
                   || PageUiUtilities.TryGetFastSlotUnderPointer(centerSection.FastSlot4, playerInventory.FastSlot4, screenPoint, eventCamera, out fastSlotModel);
        }

        public bool TryGetFastSlotRect(FastSlotModel fastSlotModel, out RectTransform slotRect)
        {
            return PageUiUtilities.TryGetFastSlotRect(centerSection, playerInventory, fastSlotModel, out slotRect);
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

        public bool TryAddToSourceSellInventory(ItemStack itemStack, IInventory sourceInventory, SlotModel sourceSlot, Matrix4x4 sourcePosition)
        {
            var sourceSide = ResolveSourceSide(sourceInventory, sourceSlot);
            var destinationSellInventory = sourceSide switch
            {
                TradeSide.Player => playerSellInventory,
                TradeSide.Target => targetSellInventory,
                _ => null
            };

            if (itemStack == null || destinationSellInventory == null || destinationSellInventory.TryAdd(itemStack) != null)
            {
                return false;
            }

            RegisterMoveIntoSell(itemStack, itemStack.Count, destinationSellInventory, sourceInventory, sourceSlot, sourcePosition);
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

        public void RegisterMoveIntoSell(ItemStack itemStack, int count, IInventory destinationInventory, IInventory sourceInventory, SlotModel sourceSlot, Matrix4x4 sourcePosition)
        {
            if (itemStack?.ItemConfig == null || count <= 0 || !IsSellInventory(destinationInventory))
            {
                return;
            }

            if (IsSellInventory(sourceInventory))
            {
                return;
            }

            var key = GetSellItemKey(itemStack);
            var queue = GetOriginDictionary(destinationInventory);
            if (!queue.TryGetValue(key, out var origins))
            {
                origins = new Queue<SellItemOrigin>();
                queue[key] = origins;
            }

            origins.Enqueue(new SellItemOrigin(sourceInventory, sourceSlot, sourcePosition, count));
        }

        public void ConsumeSellOriginIfAny(ItemStack itemStack, IInventory sourceInventory)
        {
            if (itemStack?.ItemConfig == null || !IsSellInventory(sourceInventory))
            {
                return;
            }

            var dictionary = GetOriginDictionary(sourceInventory);
            var key = GetSellItemKey(itemStack);
            if (!dictionary.TryGetValue(key, out var queue) || queue.Count == 0)
            {
                return;
            }

            var remaining = itemStack.Count;
            while (remaining > 0 && queue.Count > 0)
            {
                var origin = queue.Peek();
                if (origin.Count <= remaining)
                {
                    remaining -= origin.Count;
                    queue.Dequeue();
                    continue;
                }

                queue.Dequeue();
                queue.Enqueue(new SellItemOrigin(origin.SourceInventory, origin.SourceSlot, origin.SourcePosition, origin.Count - remaining));
                remaining = 0;
            }

            if (queue.Count == 0)
            {
                dictionary.Remove(key);
            }
        }

        private void ReDraw()
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
            UpdateSellInfo();
            UpdatePlayersInfo();
            PageUiUtilities.FillSlotsViewContainerStats(centerSection, statsController);
            CacheSlotItems();
        }

        private void UpdateInventoryInfo()
        {
            var targetInventory = dialogueContext.CurrentTargetInventory;
            var handWeight = playerInventory.HandSlot.Value?.ItemStack?.TotalWeight ?? 0f;
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
            UpdateTradeButtonsState();
        }

        private void UpdateSellInfoText(SellInfo sellInfo, IInventory sellInventory)
        {
            if (!sellInfo.InfoText || sellInventory == null)
            {
                return;
            }

            var totalWeight = sellInventory.Items.Sum(item => item.ItemStack.TotalWeight);
            var totalPrice = CalculateItemsPrice(sellInventory);
            var handWeight = playerInventory.HandSlot.Value?.ItemStack?.TotalWeight ?? 0f;
            var handPrice = playerInventory.HandSlot.Value?.ItemStack?.TotalPrice ?? 0;
            if (playerInventory.HandSourceInventory.Value == sellInventory)
            {
                totalWeight += handWeight > 0f ? handWeight : 0f;
                totalPrice += handPrice > 0 ? handPrice : 0;
            }

            PageUiUtilities.FillSellInventoryInfoText(sellInfo.InfoText, localizationConfig, colorsConfig, totalPrice, totalWeight);
        }

        private void UpdatePlayersInfo()
        {
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo, playerMoneyStorage);
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTargetMoneyStorage);
        }

        private void UpdateTradeButtonsState()
        {
            var playerSellPrice = CalculateItemsPrice(playerSellInventory);
            var targetSellPrice = CalculateItemsPrice(targetSellInventory);
            var targetCanBuy = dialogueContext.CurrentTargetMoneyStorage?.CanSpend(playerSellPrice) ?? false;
            var playerCanBuy = playerMoneyStorage.CanSpend(targetSellPrice);

            if (rightSellInfo?.TradeButton)
            {
                rightSellInfo.TradeButton.interactable = targetCanBuy;
            }

            if (leftSellInfo?.TradeButton)
            {
                leftSellInfo.TradeButton.interactable = playerCanBuy;
            }
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

        private void DrawSlotItems()
        {
            DrawSlotItem(centerSection.HeadSlot, playerInventory.HelmSlot);
            DrawSlotItem(centerSection.BodySlot, playerInventory.BodySlot);
            DrawSlotItem(centerSection.BackpackSlot, playerInventory.BackpackSlot);
            DrawFastSlotItem(centerSection.FastSlot1, playerInventory.FastSlot1);
            DrawFastSlotItem(centerSection.FastSlot2, playerInventory.FastSlot2);
            DrawFastSlotItem(centerSection.FastSlot3, playerInventory.FastSlot3);
            DrawFastSlotItem(centerSection.FastSlot4, playerInventory.FastSlot4);
        }

        private void DrawSlotItem(SlotView slotView, SlotModel slotModel)
        {
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
            if (TryGetHoveredFastSlot(screenPoint, out var fastSlotModel)
             && TryGetFastSlotRect(fastSlotModel, out var fastSlotRect)
             && CanMoveToPlayerSlot(dragSourceInventory, dragSourceSlot))
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

            var handItemStack = playerInventory.HandSlot.Value?.ItemStack;
            var gridLayoutGroup = view.ContentForTiles.GetComponent<GridLayoutGroup>();
            var tiles = (inventory as ITiledInventory)?.Tiles;
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
                   || lastBackpackItemConfig != playerInventory.BackpackSlot.ItemConfig
                   || lastFastSlot1ItemConfig != playerInventory.FastSlot1.ItemConfig
                   || lastFastSlot2ItemConfig != playerInventory.FastSlot2.ItemConfig
                   || lastFastSlot3ItemConfig != playerInventory.FastSlot3.ItemConfig
                   || lastFastSlot4ItemConfig != playerInventory.FastSlot4.ItemConfig;
        }

        private void CacheSlotItems()
        {
            lastHelmItemConfig = playerInventory.HelmSlot.ItemConfig;
            lastBodyItemConfig = playerInventory.BodySlot.ItemConfig;
            lastBackpackItemConfig = playerInventory.BackpackSlot.ItemConfig;
            lastFastSlot1ItemConfig = playerInventory.FastSlot1.ItemConfig;
            lastFastSlot2ItemConfig = playerInventory.FastSlot2.ItemConfig;
            lastFastSlot3ItemConfig = playerInventory.FastSlot3.ItemConfig;
            lastFastSlot4ItemConfig = playerInventory.FastSlot4.ItemConfig;
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
                hoverPopupElapsed = 0f;
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

            ClosePopup();
            popupRect = resolver.Instantiate(uiConfig.PopupRect, popupParentRect);
            popupRect.name = $"{uiConfig.PopupRect.name} | Trade Popup";
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

        private void CreatePopupButtons(PopupTarget target)
        {
            if (target?.InventoryItem?.ItemStack?.ItemConfig != null && IsSellInventory(target.SourceInventory))
            {
                CreatePopupButton("Out Sell", () => ExecutePopupAction(() => TryReturnSellItem(target.SourceInventory, target.InventoryItem)));
                return;
            }

            var itemCount = GetPopupItemStack(target)?.Count ?? 0;
            if (itemCount <= 0)
            {
                return;
            }

            CreatePopupButton("To Sell", () => ExecutePopupAction(() => TryMoveTargetToSell(target, itemCount)));

            var halfCount = GetHalfCount(itemCount);
            if (halfCount > 0)
            {
                CreatePopupButton("Half To Sell", () => ExecutePopupAction(() => TryMoveTargetToSell(target, halfCount)));
            }
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

        private bool TryMoveTargetToSell(PopupTarget target, int count)
        {
            if (count <= 0)
            {
                return false;
            }

            if (target?.SlotModel?.ItemStack?.ItemConfig != null)
            {
                return TryMoveSlotToSell(target.SlotModel, count);
            }

            if (target?.InventoryItem?.ItemStack?.ItemConfig != null)
            {
                return TryMoveInventoryItemToSell(target.SourceInventory, target.InventoryItem, count);
            }

            return false;
        }

        private bool TryMoveSlotToSell(SlotModel slotModel, int count)
        {
            if (slotModel?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            var destinationSellInventory = playerSellInventory;
            if (destinationSellInventory == null
             || !playerInventory.TryTakeFromSlot(slotModel.ItemType, count, out var itemStack)
             || itemStack?.ItemConfig == null)
            {
                return false;
            }

            if (destinationSellInventory.TryAdd(itemStack) != null)
            {
                playerInventory.TryPlaceInSlot(slotModel.ItemType, itemStack, out _, out _);
                return false;
            }

            RegisterMoveIntoSell(itemStack, itemStack.Count, destinationSellInventory, playerInventory, slotModel, Matrix4x4.identity);
            HandleBackpackResizeIfNeeded(slotModel.ItemType, itemStack.ItemConfig);
            return true;
        }

        private bool TryMoveInventoryItemToSell(IInventory sourceInventory, ItemInInventory itemInInventory, int count)
        {
            if (sourceInventory == null || itemInInventory?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            var destinationSellInventory = GetSellInventoryForSource(sourceInventory);
            if (destinationSellInventory == null)
            {
                return false;
            }

            var itemConfig = itemInInventory.ItemConfig;
            var originalPosition = itemInInventory.Position;
            var originalCount = itemInInventory.Count;
            var moveCount = Mathf.Clamp(count, 1, originalCount);
            var movingStack = new ItemStack(itemConfig, moveCount, itemInInventory.ItemStack.IsRotated);
            var remainder = destinationSellInventory.TryAdd(movingStack);
            var movedCount = moveCount - (remainder?.Count ?? 0);
            if (movedCount <= 0)
            {
                return false;
            }

            sourceInventory.Remove(itemInInventory);
            var remainingSourceCount = originalCount - movedCount;
            if (remainingSourceCount > 0)
            {
                sourceInventory.Add(new ItemStack(itemConfig, remainingSourceCount, itemInInventory.ItemStack.IsRotated), originalPosition);
            }

            RegisterMoveIntoSell(new ItemStack(itemConfig, movedCount, itemInInventory.ItemStack.IsRotated), movedCount, destinationSellInventory, sourceInventory, null, originalPosition);
            return true;
        }

        private bool TryReturnSellItem(IInventory sellInventory, ItemInInventory itemInInventory)
        {
            if (sellInventory == null || itemInInventory?.ItemStack?.ItemConfig == null || !IsSellInventory(sellInventory))
            {
                return false;
            }

            var returningStack = itemInInventory.ItemStack.Clone();
            var totalCount = returningStack.Count;
            var origins = GetOriginDictionary(sellInventory);
            var fallbackInventory = GetDefaultInventoryForSellInventory(sellInventory);

            sellInventory.Remove(itemInInventory);
            var remainder = RestoreFromOrigins(returningStack, origins, fallbackInventory);
            var returnedCount = totalCount - (remainder?.Count ?? 0);

            if (remainder != null)
            {
                sellInventory.TryAdd(remainder);
            }

            if (returnedCount > 0 && sellInventory == playerSellInventory && returningStack.ItemConfig.ItemType == ItemType.Backpack)
            {
                RebuildInventoryAndDropOverflow();
            }

            return returnedCount > 0;
        }

        private ItemStack RestoreFromOrigins(ItemStack itemStack, Dictionary<SellItemKey, Queue<SellItemOrigin>> origins, IInventory fallbackInventory)
        {
            if (itemStack?.ItemConfig == null)
            {
                return itemStack;
            }

            var key = GetSellItemKey(itemStack);
            if (!origins.TryGetValue(key, out var queue) || queue.Count == 0)
            {
                return fallbackInventory?.TryAdd(itemStack) ?? itemStack;
            }

            var remainingCount = itemStack.Count;
            var remainingOrigins = new Queue<SellItemOrigin>();

            while (remainingCount > 0 && queue.Count > 0)
            {
                var origin = queue.Dequeue();
                var countToReturn = Mathf.Min(remainingCount, origin.Count);
                var stackToReturn = new ItemStack(itemStack.ItemConfig, countToReturn, itemStack.IsRotated);
                var remainder = TryRestoreToOrigin(origin, stackToReturn, fallbackInventory);
                var returnedCount = countToReturn - (remainder?.Count ?? 0);

                if (returnedCount > 0)
                {
                    remainingCount -= returnedCount;
                }

                var originCountLeft = origin.Count - returnedCount;
                if (originCountLeft > 0)
                {
                    remainingOrigins.Enqueue(new SellItemOrigin(origin.SourceInventory, origin.SourceSlot, origin.SourcePosition, originCountLeft));
                }

                if (returnedCount <= 0)
                {
                    while (queue.Count > 0)
                    {
                        remainingOrigins.Enqueue(queue.Dequeue());
                    }

                    break;
                }
            }

            while (queue.Count > 0)
            {
                remainingOrigins.Enqueue(queue.Dequeue());
            }

            if (remainingOrigins.Count > 0)
            {
                origins[key] = remainingOrigins;
            }
            else
            {
                origins.Remove(key);
            }

            if (remainingCount <= 0)
            {
                return null;
            }

            var remainingStack = new ItemStack(itemStack.ItemConfig, remainingCount, itemStack.IsRotated);
            return fallbackInventory?.TryAdd(remainingStack) ?? remainingStack;
        }

        private ItemStack TryRestoreToOrigin(SellItemOrigin origin, ItemStack itemStack, IInventory fallbackInventory)
        {
            if (itemStack?.ItemConfig == null)
            {
                return itemStack;
            }

            var remainder = itemStack;

            if (origin.SourceSlot != null)
            {
                if (TryPlaceBackIntoOriginalSlot(origin.SourceSlot, remainder))
                {
                    return null;
                }
            }
            else if (origin.SourceInventory is ITiledInventory tiledInventory)
            {
                var center = origin.SourcePosition.GetColumn(3);
                var startPosition = new Vector2Int(
                    Mathf.RoundToInt(center.x - (itemStack.Size.x - 1) * 0.5f),
                    Mathf.RoundToInt(center.y - (itemStack.Size.y - 1) * 0.5f));
                if (tiledInventory.Tiles.TryGetTile(startPosition.x, startPosition.y, out var tile))
                {
                    remainder = origin.SourceInventory.TryAdd(remainder, tile);
                }
            }

            if (remainder == null)
            {
                return null;
            }

            if (origin.SourceInventory != null)
            {
                remainder = origin.SourceInventory.TryAdd(remainder);
            }

            if (remainder == null || fallbackInventory == null || fallbackInventory == origin.SourceInventory)
            {
                return remainder;
            }

            return fallbackInventory.TryAdd(remainder);
        }

        private bool TryPlaceBackIntoOriginalSlot(SlotModel slotModel, ItemStack itemStack)
        {
            if (slotModel?.ItemType != itemStack?.ItemConfig?.ItemType)
            {
                return false;
            }

            if (slotModel.ItemStack != null)
            {
                if (!slotModel.ItemStack.CanStackWith(itemStack))
                {
                    return false;
                }

                var maxStack = slotModel.GetMaxStack(itemStack.ItemConfig);
                if (slotModel.ItemStack.Count >= maxStack)
                {
                    return false;
                }
            }

            return playerInventory.TryPlaceInSlot(slotModel.ItemType, itemStack, out var remainderStack, out var replacedStack)
                   && replacedStack == null
                   && remainderStack == null;
        }

        private void HandleBackpackResizeIfNeeded(ItemType slotType, ItemConfig itemConfig)
        {
            if (slotType == ItemType.Backpack && itemConfig?.ItemType == ItemType.Backpack)
            {
                RebuildInventoryAndDropOverflow();
            }
        }

        private void RebuildInventoryAndDropOverflow()
        {
            var droppedItems = playerInventory.RebuildInventoryFromCurrentBackpack();
            foreach (var droppedItem in droppedItems)
            {
                inventoryHandController?.Drop(droppedItem);
            }
        }

        private TradeSellInventory GetSellInventoryForSource(IInventory sourceInventory)
        {
            if (sourceInventory == playerInventory)
            {
                return playerSellInventory;
            }

            return sourceInventory == dialogueContext.CurrentTargetInventory ? targetSellInventory : null;
        }

        private IInventory GetDefaultInventoryForSellInventory(IInventory sellInventory)
        {
            if (sellInventory == playerSellInventory)
            {
                return playerInventory;
            }

            return sellInventory == targetSellInventory ? dialogueContext.CurrentTargetInventory : null;
        }

        private static int GetHalfCount(int totalCount)
        {
            return totalCount > 1 ? totalCount / 2 : 0;
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

        private void ClosePopup()
        {
            if (popupRect == null)
            {
                popupOpenMode = PopupOpenMode.None;
                return;
            }

            Object.Destroy(popupRect.gameObject);
            popupRect = null;
            popupOpenMode = PopupOpenMode.None;
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
            var handItemStack = playerInventory.HandSlot.Value?.ItemStack;
            if (gridLayoutGroup == null || handItemStack?.ItemConfig == null)
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

            var itemGrabSize = PageUiUtilities.GetItemGrabSize(gridLayoutGroup, handItemStack.Size);
            var grabFromLeft = handGrabOffset.x + itemGrabSize.x * 0.5f;
            var grabFromTop = itemGrabSize.y * 0.5f - handGrabOffset.y;
            var grabOffsetX = Mathf.Clamp(Mathf.FloorToInt(grabFromLeft / stepX), 0, handItemStack.Size.x - 1);
            var grabOffsetY = Mathf.Clamp(Mathf.FloorToInt(grabFromTop / stepY), 0, handItemStack.Size.y - 1);

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

        private Dictionary<SellItemKey, Queue<SellItemOrigin>> GetOriginDictionary(IInventory sellInventory)
        {
            return sellInventory == playerSellInventory ? playerSellOrigins : targetSellOrigins;
        }

        private void ReturnItemsFromSellInventories()
        {
            ReturnItemsFromSellInventory(playerSellInventory, playerInventory, playerSellOrigins);
            ReturnItemsFromSellInventory(targetSellInventory, dialogueContext.CurrentTargetInventory, targetSellOrigins);
            RebuildInventoryAndDropOverflow();
        }

        private void ReturnItemsFromSellInventory(TradeSellInventory sellInventory, IInventory defaultInventory, Dictionary<SellItemKey, Queue<SellItemOrigin>> origins)
        {
            if (sellInventory == null)
            {
                return;
            }

            var itemsToReturn = sellInventory.Items.ToList();
            foreach (var item in itemsToReturn)
            {
                sellInventory.Remove(item);
                var remainder = RestoreFromOrigins(item.ItemStack.Clone(), origins, defaultInventory);
                if (remainder != null)
                {
                    var overflow = defaultInventory?.TryAdd(remainder) ?? remainder;
                    if (overflow != null)
                    {
                        inventoryHandController?.Drop(overflow);
                    }
                }
            }

            origins.Clear();
        }

        private void CompletePlayerSell()
        {
            TransferSellInventory(
                playerSellInventory,
                dialogueContext.CurrentTargetInventory,
                playerSellOrigins,
                playerMoneyStorage,
                dialogueContext.CurrentTargetMoneyStorage);
        }

        private void CompleteTargetSell()
        {
            TransferSellInventory(
                targetSellInventory,
                playerInventory,
                targetSellOrigins,
                dialogueContext.CurrentTargetMoneyStorage,
                playerMoneyStorage);
        }

        private static void TransferSellInventory(
            TradeSellInventory sourceSellInventory,
            IInventory destinationInventory,
            Dictionary<SellItemKey, Queue<SellItemOrigin>> origins,
            MoneyStorage sellerMoneyStorage,
            MoneyStorage buyerMoneyStorage)
        {
            if (sourceSellInventory == null || destinationInventory == null || sellerMoneyStorage == null || buyerMoneyStorage == null)
            {
                return;
            }

            var items = sourceSellInventory.Items.ToList();
            foreach (var item in items)
            {
                var stackToSell = item.ItemStack.Clone();
                var maxAffordableCount = stackToSell.ItemConfig.Price > 0
                    ? buyerMoneyStorage.CurrentMoney.Value / stackToSell.ItemConfig.Price
                    : stackToSell.Count;
                if (maxAffordableCount <= 0)
                {
                    continue;
                }

                if (maxAffordableCount < stackToSell.Count)
                {
                    stackToSell.Count = maxAffordableCount;
                }

                var remainder = destinationInventory.TryAdd(stackToSell);
                var soldCount = stackToSell.Count - (remainder?.Count ?? 0);
                if (soldCount <= 0)
                {
                    continue;
                }

                if (soldCount >= item.Count)
                {
                    sourceSellInventory.Remove(item);
                }
                else
                {
                    item.ItemStack.Count -= soldCount;
                }

                var soldPrice = item.ItemConfig.Price * soldCount;
                buyerMoneyStorage.TrySpend(soldPrice);
                sellerMoneyStorage.Add(soldPrice);

                var key = new SellItemKey(item.ItemConfig, item.ItemStack.IsRotated);
                if (origins.TryGetValue(key, out var queue) && queue.Count > 0)
                {
                    var remainingOriginCount = soldCount;
                    while (remainingOriginCount > 0 && queue.Count > 0)
                    {
                        var origin = queue.Dequeue();
                        if (origin.Count > remainingOriginCount)
                        {
                            queue.Enqueue(new SellItemOrigin(origin.SourceInventory, origin.SourceSlot, origin.SourcePosition, origin.Count - remainingOriginCount));
                            remainingOriginCount = 0;
                        }
                        else
                        {
                            remainingOriginCount -= origin.Count;
                        }
                    }

                    if (queue.Count == 0)
                    {
                        origins.Remove(key);
                    }
                }
            }
        }

        private static SellItemKey GetSellItemKey(ItemStack itemStack)
        {
            return new SellItemKey(itemStack.ItemConfig, itemStack.IsRotated);
        }

        private static int CalculateItemsPrice(IInventory inventory)
        {
            if (inventory == null)
            {
                return 0;
            }

            var totalPrice = 0;
            foreach (var item in inventory.Items)
            {
                if (item?.ItemConfig != null)
                {
                    totalPrice += item.ItemStack.TotalPrice;
                }
            }

            return totalPrice;
        }

        private void ReturnToDialogue()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }

        private Camera GetEventCamera() => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }
}
