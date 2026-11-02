using System;
using System.Collections.Generic;
using System.Linq;
using Colors;
using Factions;
using Dialogue;
using GameModes;
using Inventory;
using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using Input;
using Localization;
using MessagePipe;
using Messages;
using Money;
using Stats;
using Trade;
using TMPro;
using UI.Configs;
using UI.Inventory;
using UI.UIElements;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
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

        private enum TradeSide
        {
            None,
            Player,
            Target
        }

        public override PageType Type { get; } = PageType.Trade;
        private readonly UIConfig uiConfig;
        private readonly StatsConfig statsConfig;
        private readonly StatFiller hpFiller;
        private readonly LocalizationConfig localizationConfig;
        private readonly ColorsConfig colorsConfig;
        private readonly StatIconsConfig statIconsConfig;
        private readonly PlayerInventory playerInventory;
        private readonly InputConfig inputConfig;
        private readonly TradeHandInteractionService tradeHandInteractionService;
        private readonly TradeSettlementService tradeSettlementService;
        private readonly TradeOfferService tradeOfferService;
        private readonly MoneyStorage playerMoneyStorage;
        private readonly Character.CharacterInfo playerCharacterInfo;
        private readonly FactionConfig playerFaction;
        private readonly DialogueContext dialogueContext;
        private readonly StatsController statsController;
        private readonly Canvas canvas;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly UI.Inventory.InventoryInteractionContext interactionContext;

        private RectTransform contentRect = null!;
        private RectTransform sectionsLayoutRect = null!;
        private RectTransform leftRect = null!;
        private RectTransform rightRect = null!;
        private InfoAboutPlayer leftInfoAboutPlayer = null!;
        private InfoAboutPlayer rightInfoAboutPlayer = null!;
        private InfoAboutInventory leftInfoAboutInventory = null!;
        private InfoAboutInventory rightInfoAboutInventory = null!;
        private SlotsViewContainer centerSection = null!;
        private PlayerInventorySlotsView playerSlotsView;
        private InventoryView playerInventoryView = null!;
        private InventoryView targetInventoryView = null!;
        private InventoryView playerSellInventoryView = null!;
        private InventoryView targetSellInventoryView = null!;
        private InventoryItemsView itemsView;
        private SellInfo leftSellInfo = null!;
        private SellInfo rightSellInfo = null!;
        private InventoryHandPresenter handPresenter;
        private InventoryPopupController popupController;
        private readonly CompositeDisposable redrawDisposables = new();
        private readonly InventoryGridCollectionView inventoryGrids = new();
        private InventoryInteractionPresenter interactionPresenter;
        private readonly PlayerInventorySlotStateTracker slotStateTracker;
        private readonly List<RectTransform> itemRects = new();
        private readonly List<RectTransform> itemGrabRects = new();
        private readonly List<PopupTarget> popupTargets = new();
        private HeartbeatPulse heartbeatPulse;
        private BloodScreenController bloodScreenController;
        private Image bloodScreen;
        private TradeSession tradeSession;
        private IInventory dragSourceInventory;
        private SlotModel dragSourceSlot;
        private Vector2 handGrabOffset => interactionPresenter?.GrabOffset ?? Vector2.zero;
        private InventoryTooltipPresenter<PopupTarget> tooltipPresenter;

        public TradePage
            (
                UIConfig uiConfig,
                StatsConfig statsConfig,
                StatFillers statFillers,
                LocalizationConfig localizationConfig,
                ColorsConfig colorsConfig,
                StatIconsConfig statIconsConfig,
                InputConfig inputConfig,
                PlayerInventory playerInventory,
                InventoryHandController inventoryHandController,
                TradeHandInteractionService tradeHandInteractionService,
                MoneyStorage playerMoneyStorage,
                Character.CharacterInfo playerCharacterInfo,
                DialogueContext dialogueContext,
                StatsController statsController,
                Canvas canvas,
                IObjectResolver resolver,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
                InventoryInteractionContext interactionContext
            )
        {
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            hpFiller = statFillers.Get(StatType.Hp);
            this.localizationConfig = localizationConfig;
            this.colorsConfig = colorsConfig;
            this.statIconsConfig = statIconsConfig;
            this.inputConfig = inputConfig;
            this.playerInventory = playerInventory;
            slotStateTracker = new PlayerInventorySlotStateTracker(playerInventory);
            this.tradeHandInteractionService = tradeHandInteractionService;
            tradeSettlementService = new TradeSettlementService();
            tradeOfferService = new TradeOfferService(playerInventory, itemStack => inventoryHandController?.Drop(itemStack));
            this.playerMoneyStorage = playerMoneyStorage;
            this.playerCharacterInfo = playerCharacterInfo;
            playerFaction = resolver.TryResolve<FactionConfig>(out var resolvedFaction) ? resolvedFaction : null;
            this.dialogueContext = dialogueContext;
            this.statsController = statsController;
            this.canvas = canvas;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.interactionContext = interactionContext;

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

            interactionContext.SetActivePage(this);
            tradeSession = new TradeSession(playerInventory, dialogueContext.CurrentTargetInventory);

            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";
            popupController = new InventoryPopupController(uiConfig, resolver, contentRect, GetEventCamera);
            tooltipPresenter = new InventoryTooltipPresenter<PopupTarget>(
                () => popupController?.Mode ?? InventoryPopupMode.None,
                () => popupController?.Close(),
                screenPoint => popupController?.UpdatePosition(screenPoint),
                IsSamePopupTarget,
                true);
            handPresenter = new InventoryHandPresenter(
                playerInventory,
                new InventoryHandView(canvasRect, uiConfig.FastSlotLabelFont, GetEventCamera),
                inventory => inventoryGrids.TryGetInventoryView(inventory, out var view) ? view : null,
                GetEventCamera);

            sectionsLayoutRect = PageUiUtilities.CreateSectionsLayout(contentRect, Type.ToString());
            leftRect = resolver.Instantiate(uiConfig.LeftAnotherInventoryInTrade, sectionsLayoutRect).GetComponent<RectTransform>();
            centerSection = resolver.Instantiate(uiConfig.CenterSection, sectionsLayoutRect);
            playerSlotsView = new PlayerInventorySlotsView(centerSection, playerInventory, inputConfig, uiConfig.FastSlotLabelFont, itemRects, itemGrabRects);
            PageUiUtilities.FillSlotsViewContainerStats(centerSection, statsController);
            rightRect = resolver.Instantiate(uiConfig.RightPlayerInventoryInTrade, sectionsLayoutRect).GetComponent<RectTransform>();
            PageUiUtilities.RegisterSectionInLayout(leftRect);
            PageUiUtilities.RegisterSectionInLayout(centerSection.GetComponent<RectTransform>());
            PageUiUtilities.RegisterSectionInLayout(rightRect);

            leftInfoAboutPlayer = GetRequiredComponentInChildren<InfoAboutPlayer>(leftRect, "left trade inventory");
            leftInfoAboutInventory = GetRequiredComponentInChildren<InfoAboutInventory>(leftRect, "left trade inventory");
            leftSellInfo = GetRequiredComponentInChildren<SellInfo>(leftRect, "left trade inventory");
            targetSellInventoryView = GetRequiredInventoryView(leftRect, "Sell Inventory");
            targetInventoryView = GetRequiredInventoryView(leftRect, "Inventory In Trade");
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTargetMoneyStorage, dialogueContext.CurrentTargetFaction);

            rightInfoAboutPlayer = GetRequiredComponentInChildren<InfoAboutPlayer>(rightRect, "right trade inventory");
            rightInfoAboutInventory = GetRequiredComponentInChildren<InfoAboutInventory>(rightRect, "right trade inventory");
            rightSellInfo = GetRequiredComponentInChildren<SellInfo>(rightRect, "right trade inventory");
            playerSellInventoryView = GetRequiredInventoryView(rightRect, "Sell Inventory");
            playerInventoryView = GetRequiredInventoryView(rightRect, "Inventory In Trade");
            itemsView = new InventoryItemsView(uiConfig.FastSlotLabelFont, itemRects, itemGrabRects);
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo, playerMoneyStorage, playerFaction);

            inventoryGrids.Configure(new[]
            {
                new KeyValuePair<IInventory, InventoryView>(playerInventory, playerInventoryView),
                new KeyValuePair<IInventory, InventoryView>(targetInventory, targetInventoryView),
                new KeyValuePair<IInventory, InventoryView>(tradeSession.PlayerOffer.Inventory, playerSellInventoryView),
                new KeyValuePair<IInventory, InventoryView>(tradeSession.TargetOffer.Inventory, targetSellInventoryView)
            });
            interactionPresenter = new InventoryInteractionPresenter(
                playerInventory,
                () => centerSection,
                inventory => inventoryGrids.TryGetGridView(inventory, out var gridView) ? gridView : null,
                screenPoint => inventoryGrids.TryGetInventoryAt(GetEventCamera(), screenPoint, out var inventory) ? inventory : null,
                GetEventCamera,
                () => CanMoveToPlayerSlot(dragSourceInventory, dragSourceSlot),
                itemRects,
                itemGrabRects);
            inventoryGrids.EnsureTiles(CreateTile);

            bloodScreen = PageUiUtilities.CreateBloodScreen(uiConfig, resolver, contentRect, Type);
            heartbeatPulse = new HeartbeatPulse(statsConfig, statsController.Hp, hpFiller);
            bloodScreenController = new BloodScreenController(statsConfig, statsController.Hp, hpFiller, heartbeatPulse, bloodScreen);

            playerInventory.Changed.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            targetInventory.Changed.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            tradeSession.PlayerOffer.Inventory.Changed.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            tradeSession.TargetOffer.Inventory.Changed.Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
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

        private static T GetRequiredComponentInChildren<T>(RectTransform root, string ownerName) where T : Component
        {
            var component = root ? root.GetComponentInChildren<T>(true) : null;
            if (!component)
            {
                Debug.LogError($"Missing {typeof(T).Name} in {ownerName} prefab.");
            }

            return component;
        }

        private static InventoryView GetRequiredInventoryView(RectTransform root, string namePart)
        {
            var inventoryView = root
                ? root.GetComponentsInChildren<InventoryView>(true)
                    .FirstOrDefault(view => view.name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
                : null;
            if (!inventoryView)
            {
                Debug.LogError($"Missing InventoryView with name containing '{namePart}' in trade inventory prefab.");
            }

            return inventoryView;
        }

        public void Tick()
        {
            handPresenter?.UpdatePosition(handGrabOffset, TryGetSnappedHandPosition);

            HandleHoverPopup();

            if (HaveSlotsChanged() || HaveGridChanged())
            {
                ReDraw();
            }
        }

        public bool TryHandleMouseDown(MouseButtonType button, Vector2 screenPoint)
        {
            if (interactionContext.ActivePage != this || contentRect == null || playerInventory.HandSlot.Value?.ItemStack != null)
            {
                return false;
            }

            if (button == MouseButtonType.Right)
            {
                popupController?.Close();
                ResetHoverPopupState();
                TryOpenPopup(screenPoint, InventoryPopupMode.ContextMenu);
                return true;
            }

            if (button != MouseButtonType.Left || popupController?.IsOpen != true)
            {
                return false;
            }

            if (popupController.Mode == InventoryPopupMode.ContextMenu
             && popupController.ContainsScreenPoint(screenPoint))
            {
                return true;
            }

            if (popupController.Mode == InventoryPopupMode.Hover)
            {
                popupController?.Close();
                ResetHoverPopupState();
                return false;
            }

            popupController?.Close();
            ResetHoverPopupState();
            return false;
        }

        public override void Hide()
        {
            bloodScreenController?.Dispose();
            bloodScreenController = null;

            heartbeatPulse?.Dispose();
            heartbeatPulse = null;

            if (tradeSession != null && !tradeHandInteractionService.TryReturnHeldItemBeforeCancel(tradeSession))
            {
                return;
            }

            ReturnItemsFromSellInventories();
            redrawDisposables.Clear();
            itemRects.Clear();
            itemGrabRects.Clear();
            popupTargets.Clear();
            inventoryGrids.Clear();
            popupController?.Close();
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

            contentRect = null;
            sectionsLayoutRect = null;
            leftRect = null;
            rightRect = null;
            leftInfoAboutPlayer = null;
            rightInfoAboutPlayer = null;
            leftInfoAboutInventory = null;
            rightInfoAboutInventory = null;
            centerSection = null;
            playerSlotsView = null;
            bloodScreen = null;
            playerInventoryView = null;
            targetInventoryView = null;
            playerSellInventoryView = null;
            targetSellInventoryView = null;
            itemsView = null;
            leftSellInfo = null;
            rightSellInfo = null;
            handPresenter = null;
            popupController = null;
            tradeSession = null;
            dragSourceInventory = null;
            dragSourceSlot = null;
            interactionContext.ClearActivePage(this);
        }

        public bool TryCaptureGrabOffset(Vector2 screenPoint, out Vector2 handGrabOffset)
        {
            return interactionPresenter.TryCaptureGrabOffset(screenPoint, out handGrabOffset);
        }

        public void SetGrabOffset(Vector2 handGrabOffset)
        {
            interactionPresenter.SetGrabOffset(handGrabOffset);
        }

        public void ResetGrabOffset()
        {
            interactionPresenter.ResetGrabOffset();
        }

        public bool TryGetInventoryItemAt(Vector2 screenPoint, out IInventory inventory, out ItemInInventory itemInInventory)
        {
            return interactionPresenter.TryGetInventoryItemAt(screenPoint, out inventory, out itemInInventory);
        }

        public bool TryGetInventoryAt(Vector2 screenPoint, out IInventory inventory)
        {
            return interactionPresenter.TryGetInventoryAt(screenPoint, out inventory);
        }

        public bool TryGetHoveredSlot(Vector2 screenPoint, out SlotModel slotModel)
        {
            return interactionPresenter.TryGetHoveredSlot(screenPoint, out slotModel);
        }

        public bool TryGetSlotSize(SlotModel slotModel, out Vector2 slotSize)
        {
            return interactionPresenter.TryGetSlotSize(slotModel, out slotSize);
        }

        public bool TryGetHoveredFastSlot(Vector2 screenPoint, out FastSlotModel fastSlotModel)
        {
            return interactionPresenter.TryGetHoveredFastSlot(screenPoint, out fastSlotModel);
        }

        public bool TryGetFastSlotRect(FastSlotModel fastSlotModel, out RectTransform slotRect)
        {
            return interactionPresenter.TryGetFastSlotRect(fastSlotModel, out slotRect);
        }

        public bool TryGetPlacementTile(Vector2 screenPoint, IInventory inventory, out Tile tile)
        {
            return interactionPresenter.TryGetPlacementTile(screenPoint, inventory, out tile);
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
            return tradeOfferService.IsOfferInventory(tradeSession, inventory);
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

        public bool TryAddToSourceSellInventory(ItemStack itemStack, IInventory sourceInventory, SlotModel sourceSlot)
        {
            return tradeOfferService.TryAddToSourceOffer(tradeSession, itemStack, sourceInventory, sourceSlot);
        }

        public ItemStack TryAddToPlayerInventory(ItemStack itemStack)
        {
            return tradeOfferService.TryAddToPlayerInventory(itemStack, GetSlotSizeOrNull);
        }

        public IInventory GetTargetInventory()
        {
            return dialogueContext.CurrentTargetInventory;
        }

        public bool CanMoveToInventory(IInventory fromInventory, SlotModel fromSlot, IInventory destinationInventory)
        {
            return tradeOfferService.CanMoveToInventory(tradeSession, fromInventory, fromSlot, destinationInventory);
        }

        public bool CanMoveToPlayerSlot(IInventory fromInventory, SlotModel fromSlot)
        {
            return tradeOfferService.CanMoveToPlayerSlot(tradeSession, fromInventory, fromSlot);
        }

        private void ReDraw()
        {
            if (!playerInventoryView)
            {
                return;
            }

            popupController?.Close();
            ResetHoverPopupState();
            inventoryGrids.EnsureTiles(CreateTile);
            inventoryGrids.SetScrollEnabled(playerInventory.HandSlot.Value?.ItemStack == null);
            itemRects.Clear();
            itemGrabRects.Clear();
            popupTargets.Clear();
            interactionPresenter.ClearInventoryItems();

            inventoryGrids.ClearItems();
            inventoryGrids.RenderItems(itemsView, (inventory, itemRect, item) =>
            {
                interactionPresenter.RegisterInventoryItem(inventory, itemRect, item);
                popupTargets.Add(new PopupTarget
                {
                    Rect = itemRect,
                    SourceInventory = inventory,
                    InventoryItem = item
                });
            });

            DrawSlotItems();
            handPresenter?.RenderAndPosition(handGrabOffset, TryGetSnappedHandPosition);
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
                             + PageUiUtilities.GetSlotsWeight(
                                 playerInventory.HelmSlot,
                                 playerInventory.FaceSlot,
                                 playerInventory.BodySlot,
                                 playerInventory.HandsSlot,
                                 playerInventory.ArmsSlot,
                                 playerInventory.LegsSlot,
                                 playerInventory.HipsSlot,
                                 playerInventory.BackpackSlot,
                                 playerInventory.LeftWeaponSlot,
                                 playerInventory.RightWeaponSlot)
                             + PageUiUtilities.GetItemsWeight(tradeSession?.PlayerOffer.Inventory);
            if (handWeight > 0f && (handSourceInventory == playerInventory || handSourceInventory == tradeSession?.PlayerOffer.Inventory))
            {
                playerWeight += handWeight;
            }

            PageUiUtilities.FillInfoAboutInventory(rightInfoAboutInventory, localizationConfig, colorsConfig, playerWeight, playerInventory.MaxWeight);

            var targetWeight = PageUiUtilities.GetItemsWeight(targetInventory)
                             + PageUiUtilities.GetItemsWeight(tradeSession?.TargetOffer.Inventory);
            if (handWeight > 0f && (handSourceInventory == targetInventory || handSourceInventory == tradeSession?.TargetOffer.Inventory))
            {
                targetWeight += handWeight;
            }

            PageUiUtilities.FillInfoAboutInventory(leftInfoAboutInventory, localizationConfig, colorsConfig, targetWeight, targetInventory?.MaxWeight);
        }

        private void UpdateSellInfo()
        {
            UpdateSellInfoText(rightSellInfo, tradeSession?.PlayerOffer.Inventory);
            UpdateSellInfoText(leftSellInfo, tradeSession?.TargetOffer.Inventory);
            UpdateTradeButtonsState();
        }

        private void UpdateSellInfoText(SellInfo sellInfo, IInventory sellInventory)
        {
            if (!sellInfo.InfoText || sellInventory == null)
            {
                return;
            }

            var totalWeight = sellInventory.Items.Sum(item => item.ItemStack.TotalWeight);
            var totalPrice = tradeSettlementService.CalculatePrice(sellInventory);
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
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo, playerMoneyStorage, playerFaction);
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTargetMoneyStorage, dialogueContext.CurrentTargetFaction);
        }

        private void UpdateTradeButtonsState()
        {
            var playerSellPrice = tradeSettlementService.CalculatePrice(tradeSession?.PlayerOffer.Inventory);
            var targetSellPrice = tradeSettlementService.CalculatePrice(tradeSession?.TargetOffer.Inventory);
            var targetCanBuy = dialogueContext.CurrentTargetMoneyStorage?.CanSpend(playerSellPrice) ?? false;
            var playerCanBuy = playerMoneyStorage.CanSpend(targetSellPrice);
            var targetHasTradeCapacity = CanTargetAcceptPlayerSell();
            var playerHasTradeCapacity = CanPlayerAcceptTargetSell();

            if (rightSellInfo?.TradeButton)
            {
                rightSellInfo.TradeButton.interactable = targetCanBuy && targetHasTradeCapacity;
            }

            if (leftSellInfo?.TradeButton)
            {
                leftSellInfo.TradeButton.interactable = playerCanBuy && playerHasTradeCapacity;
            }
        }

        private void CreateTile(IInventory inventory, RectTransform parent, Tile gridTile)
        {
            var tile = resolver.Instantiate(uiConfig.Tile, parent);
            tile.Initialize(inventory, gridTile);
            tile.GetComponentInChildren<TMP_Text>().text = $"{gridTile.Index.x}:{gridTile.Index.y}";
        }

        private void DrawSlotItems()
        {
            playerSlotsView.Render((slotRect, slotModel) => popupTargets.Add(new PopupTarget
            {
                Rect = slotRect,
                SourceInventory = playerInventory,
                SlotModel = slotModel
            }));
        }

        private bool TryGetSnappedHandPosition(Vector2 screenPoint, Camera eventCamera, RectTransform dragParentRect, out Vector2 snappedPosition)
        {
            snappedPosition = Vector2.zero;
            if (TryGetSnappedPositionInSlot(screenPoint, eventCamera, dragParentRect, out var slotSnappedPosition))
            {
                snappedPosition = slotSnappedPosition;
                return true;
            }

            if (!interactionPresenter.TryGetInventoryAt(screenPoint, out var hoveredInventory)
             || !TryGetSnappedPositionInInventoryGridLocal(screenPoint, hoveredInventory, out var snappedLocalPosition))
            {
                return false;
            }

            if (!CanMoveToInventory(dragSourceInventory, dragSourceSlot, hoveredInventory))
            {
                return false;
            }

            if (!inventoryGrids.TryGetInventoryView(hoveredInventory, out var hoveredView))
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
            return interactionPresenter.TryGetSnappedGridPosition(screenPoint, inventory, out snappedLocalPosition);
        }

        private bool HaveGridChanged()
        {
            return inventoryGrids.HasGridSizeChanged();
        }

        private bool HaveSlotsChanged()
        {
            return slotStateTracker.HasChanged();
        }

        private void CacheSlotItems()
        {
            slotStateTracker.Capture();
        }

        private void HandleHoverPopup()
        {
            if (tooltipPresenter == null || contentRect == null)
            {
                return;
            }

            tooltipPresenter.Update(
                interactionContext.ActivePage == this && contentRect != null && playerInventory.HandSlot.Value?.ItemStack == null,
                Pointer.current?.position.ReadValue(),
                Time.deltaTime,
                uiConfig.PopupHoverOpenDelaySeconds,
                FindPopupTarget,
                TryOpenPopup);
        }

        private void ResetHoverPopupState()
        {
            tooltipPresenter?.Reset();
        }

        private PopupTarget FindPopupTarget(Vector2 screenPoint)
        {
            return TryGetPopupTarget(screenPoint, out var target) ? target : null;
        }

        private bool TryOpenPopup(Vector2 screenPoint, InventoryPopupMode openMode)
        {
            return TryGetPopupTarget(screenPoint, out var target) && TryOpenPopup(target, screenPoint, openMode);
        }

        private bool TryOpenPopup(PopupTarget target, Vector2 screenPoint, InventoryPopupMode openMode)
        {
            if (target == null || popupController == null)
            {
                return false;
            }

            var itemConfig = GetPopupItemConfig(target);
            var itemStack = GetPopupItemStack(target);
            if (itemConfig == null)
            {
                return false;
            }

            return popupController.Open(screenPoint, openMode, "Trade Popup", (popupRoot, popupContent) =>
            {
                if (openMode == InventoryPopupMode.Hover)
                {
                    PageUiUtilities.FillInventoryHoverPopup(
                        popupRoot,
                        popupContent,
                        uiConfig,
                        localizationConfig,
                        statIconsConfig,
                        statsController,
                        playerInventory,
                        resolver,
                        itemConfig,
                        itemStack,
                        target.SlotModel?.ItemConfig == itemConfig,
                        Color.white,
                        Color.green,
                        Color.red);
                }
                else
                {
                    CreatePopupButtons(target);
                }
            });
        }

        private void CreatePopupButtons(PopupTarget target)
        {
            if (target?.InventoryItem?.ItemStack?.ItemConfig != null && IsSellInventory(target.SourceInventory))
            {
                CreatePopupButton(localizationConfig.PopupActionRemoveFromSale, () => ExecutePopupAction(() => TryReturnSellItem(target.SourceInventory, target.InventoryItem)));
                return;
            }

            var itemCount = GetPopupItemStack(target)?.Count ?? 0;
            if (itemCount <= 0)
            {
                return;
            }

            CreatePopupButton(localizationConfig.PopupActionPutUpForSale, () => ExecutePopupAction(() => TryMoveTargetToSell(target, itemCount)));

            var halfCount = GetHalfCount(itemCount);
            if (halfCount > 0)
            {
                CreatePopupButton(localizationConfig.PopupActionPutHalfUpForSale, () => ExecutePopupAction(() => TryMoveTargetToSell(target, halfCount)));
            }
        }

        private void ExecutePopupAction(Action action)
        {
            popupController?.Close();
            ResetHoverPopupState();
            action?.Invoke();
        }

        private void CreatePopupButton(LocalizedString label, UnityEngine.Events.UnityAction onClick, bool interactable = true)
        {
            popupController?.CreateButton(label, onClick, interactable);
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
            return tradeOfferService.TryMovePlayerSlotToOffer(tradeSession, slotModel, count);
        }

        private bool TryMoveInventoryItemToSell(IInventory sourceInventory, ItemInInventory itemInInventory, int count)
        {
            return tradeOfferService.TryMoveInventoryItemToOffer(tradeSession, sourceInventory, itemInInventory, count);
        }

        private bool TryReturnSellItem(IInventory sellInventory, ItemInInventory itemInInventory)
        {
            return tradeOfferService.TryReturnOfferItem(tradeSession, sellInventory, itemInInventory, GetSlotSizeOrNull);
        }

        private Vector2? GetSlotSizeOrNull(SlotModel slotModel)
        {
            return TryGetSlotSize(slotModel, out var slotSize) ? slotSize : null;
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

            if (TryGetGridPopupTarget(screenPoint, out target))
            {
                return true;
            }

            target = null;
            return false;
        }

        private bool TryGetGridPopupTarget(Vector2 screenPoint, out PopupTarget target)
        {
            target = null;
            if (!interactionPresenter.TryGetInventoryItemAt(screenPoint, out var hoveredInventory, out var itemInInventory))
            {
                return false;
            }

            target = new PopupTarget
            {
                SourceInventory = hoveredInventory,
                InventoryItem = itemInInventory
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

        private void ReturnItemsFromSellInventories()
        {
            tradeOfferService.ReturnAllOfferItems(tradeSession, GetSlotSizeOrNull);
        }

        private void CompletePlayerSell()
        {
            if (!CanTargetAcceptPlayerSell())
            {
                return;
            }

            tradeSettlementService.Settle(
                tradeSession?.PlayerOffer,
                dialogueContext.CurrentTargetInventory,
                playerMoneyStorage,
                dialogueContext.CurrentTargetMoneyStorage);
        }

        private void CompleteTargetSell()
        {
            if (!CanPlayerAcceptTargetSell())
            {
                return;
            }

            tradeSettlementService.Settle(
                tradeSession?.TargetOffer,
                playerInventory,
                dialogueContext.CurrentTargetMoneyStorage,
                playerMoneyStorage);
        }

        private bool CanTargetAcceptPlayerSell()
        {
            return CanAcceptTradeResult(
                dialogueContext.CurrentTargetInventory,
                tradeSession?.PlayerOffer.Inventory,
                tradeSession?.TargetOffer.Inventory,
                TradeSide.Target);
        }

        private bool CanPlayerAcceptTargetSell()
        {
            return CanAcceptTradeResult(
                playerInventory,
                tradeSession?.TargetOffer.Inventory,
                tradeSession?.PlayerOffer.Inventory,
                TradeSide.Player);
        }

        private bool CanAcceptTradeResult(
            IInventory destinationInventory,
            TradeSellInventory incomingSellInventory,
            TradeSellInventory reservedSellInventory,
            TradeSide reservedHandSide)
        {
            if (destinationInventory == null)
            {
                return false;
            }

            return tradeSettlementService.CanAccept(
                destinationInventory,
                incomingSellInventory?.Items.Select(item => item?.ItemStack),
                reservedSellInventory?.Items.Select(item => item?.ItemStack),
                GetReservedHandStacks(reservedHandSide));
        }

        private IEnumerable<ItemStack> GetReservedHandStacks(TradeSide side)
        {
            var handStack = playerInventory.HandSlot.Value?.ItemStack;
            var handSourceInventory = playerInventory.HandSourceInventory.Value;
            if (handStack?.ItemConfig == null || !IsReservedHandSide(handSourceInventory, side))
            {
                return Array.Empty<ItemStack>();
            }

            return new[] { handStack };
        }

        private bool IsReservedHandSide(IInventory inventory, TradeSide side)
        {
            return side switch
            {
                TradeSide.Player => inventory == playerInventory || inventory == tradeSession?.PlayerOffer.Inventory,
                TradeSide.Target => inventory == dialogueContext.CurrentTargetInventory || inventory == tradeSession?.TargetOffer.Inventory,
                _ => false
            };
        }

        private void ReturnToDialogue()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }

        private Camera GetEventCamera() => PageUiUtilities.GetEventCamera(canvas);
    }
}
