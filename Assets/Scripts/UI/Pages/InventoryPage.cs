using System.Collections.Generic;
using Colors;
using System;
using Factions;
using Inventory;
using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Slot;
using Input;
using TMPro;
using UI.Configs;
using UI.Inventory;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Inventory.Item;
using Localization;
using Messages;
using Money;
using Stats;
using UI.UIElements;
using CharacterInfo = Character.CharacterInfo;
using Object = UnityEngine.Object;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class InventoryPage : BasePage, ITickable, IInventoryInteractionPage
    {
        // Chill отвечает за сон. Пока нет механик дня/ночи и сна, инвентарь его не отображает,
        // но стат и prefab-связи остаются для будущего возврата.
        private static readonly StatType[] AdditionalStatTypes = { StatType.Water, StatType.Food, StatType.Stamina };

        private sealed class PopupTarget
        {
            public RectTransform Rect;
            public ItemInInventory InventoryItem;
            public SlotModel SlotModel;
        }

        private enum HpFillMode
        {
            Synced,
            FillAnimated,
            ChangedFillAnimated
        }

        public override PageType Type { get; } = PageType.Inventory;
        private readonly UIConfig uiConfig;
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly StatFillers statFillers;
        private readonly StatFiller hpFiller;
        private readonly global::Inventory.InventoryConfig inventoryConfig;
        private readonly LocalizationConfig localizationConfig;
        private readonly ColorsConfig colorsConfig;
        private readonly StatIconsConfig statIconsConfig;
        private readonly PlayerInventory playerInventory;
        private readonly InputConfig inputConfig;
        private readonly InventoryItemActionService inventoryItemActionService;
        private readonly MoneyStorage playerMoneyStorage;
        private readonly CharacterInfo characterInfo;
        private readonly FactionConfig playerFaction;
        private readonly Canvas canvas;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly UI.Inventory.InventoryInteractionContext interactionContext;

        private RectTransform contentRect = null!;
        private RectTransform sectionsLayoutRect = null!;
        private StatsHolder statsHolder = null!;
        private RectTransform rightRect = null!;
        private RightPlayerInventory rightPlayerInventory = null!;
        private InfoAboutPlayer infoAboutPlayer = null!;
        private InfoAboutInventory infoAboutInventory = null!;
        private SlotsViewContainer slotsViewContainer = null!;
        private PlayerInventorySlotsView playerSlotsView;
        private Inventory.InventoryView inventoryView = null!;
        private readonly InventoryGridCollectionView inventoryGrids = new();
        private InventoryInteractionPresenter interactionPresenter;
        private readonly PlayerInventorySlotStateTracker slotStateTracker;
        private InventoryItemsView itemsView;
        private InventoryHandPresenter handPresenter;
        private readonly CompositeDisposable redrawDisposables = new();
        private readonly List<RectTransform> itemRects = new();
        private readonly List<RectTransform> itemGrabRects = new();
        private readonly List<PopupTarget> popupTargets = new();
        private Vector2 handGrabOffset => interactionPresenter?.GrabOffset ?? Vector2.zero;
        private BeatingHeart beatingHeart;
        private HeartbeatPulse heartbeatPulse;
        private BloodScreenController bloodScreenController;
        private Image bloodScreen;
        private InventoryPopupController popupController;
        private InventoryTooltipPresenter<PopupTarget> tooltipPresenter;

        private float lastHpTarget;
        private HpFillMode hpFillMode;

        public InventoryPage
            (
                UIConfig uiConfig,
                StatsConfig statsConfig,
                StatsController statsController,
                StatFillers statFillers,
                global::Inventory.InventoryConfig inventoryConfig,
                LocalizationConfig localizationConfig,
                ColorsConfig colorsConfig,
                StatIconsConfig statIconsConfig,
                Canvas canvas,
                InputConfig inputConfig,
                PlayerInventory playerInventory,
                InventoryHandController inventoryHandController,
                MoneyStorage playerMoneyStorage,
                CharacterInfo characterInfo,
                IObjectResolver resolver,
                UI.Inventory.InventoryInteractionContext interactionContext
            )
        {
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            this.statFillers = statFillers;
            this.inventoryConfig = inventoryConfig;
            hpFiller = statFillers.Get(StatType.Hp);
            this.localizationConfig = localizationConfig;
            this.colorsConfig = colorsConfig;
            this.statIconsConfig = statIconsConfig;
            this.canvas = canvas;
            this.inputConfig = inputConfig;
            this.playerInventory = playerInventory;
            slotStateTracker = new PlayerInventorySlotStateTracker(playerInventory);
            inventoryItemActionService = new InventoryItemActionService(playerInventory, inventoryHandController);
            this.playerMoneyStorage = playerMoneyStorage;
            this.characterInfo = characterInfo;
            playerFaction = resolver.TryResolve<FactionConfig>(out var resolvedFaction) ? resolvedFaction : null;
            this.resolver = resolver;
            this.interactionContext = interactionContext;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            interactionContext.SetActivePage(this);
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";
            popupController = new InventoryPopupController(uiConfig, resolver, contentRect, GetEventCamera);
            tooltipPresenter = new InventoryTooltipPresenter<PopupTarget>(
                () => popupController?.Mode ?? InventoryPopupMode.None,
                () => popupController?.Close(),
                screenPoint => popupController?.UpdatePosition(screenPoint),
                IsSamePopupTarget);
            handPresenter = new InventoryHandPresenter(
                playerInventory,
                new InventoryHandView(canvasRect, uiConfig.FastSlotLabelFont, GetEventCamera),
                inventory => inventory == playerInventory ? inventoryView : null,
                GetEventCamera);
            statsHolder = resolver.Instantiate(uiConfig.StatsHolder, contentRect);
            statsHolder.name = $"{uiConfig.StatsHolder.name} | {Type}";

            sectionsLayoutRect = PageUiUtilities.CreateSectionsLayout(contentRect, Type.ToString());
            PageUiUtilities.CreateSectionPlaceholder(sectionsLayoutRect, "Left");
            slotsViewContainer = resolver.Instantiate(uiConfig.CenterSection, sectionsLayoutRect);
            playerSlotsView = new PlayerInventorySlotsView(slotsViewContainer, playerInventory, inputConfig, uiConfig.FastSlotLabelFont, itemRects, itemGrabRects);
            rightPlayerInventory = resolver.Instantiate(uiConfig.RightPlayerInventory, sectionsLayoutRect);
            rightRect = rightPlayerInventory.GetComponent<RectTransform>();
            PageUiUtilities.RegisterSectionInLayout(slotsViewContainer.GetComponent<RectTransform>());
            PageUiUtilities.RegisterSectionInLayout(rightRect);
            PageUiUtilities.FillSlotsViewContainerStats(slotsViewContainer, statsController);
            
            infoAboutPlayer = rightPlayerInventory.InfoAboutPlayer;
            infoAboutInventory = rightPlayerInventory.InfoAboutInventory;
            inventoryView = rightPlayerInventory.InventoryView;
            PageUiUtilities.FillInfoAboutPlayer(infoAboutPlayer, characterInfo, playerMoneyStorage, playerFaction);
            
            inventoryGrids.Configure(new[] { new KeyValuePair<IInventory, Inventory.InventoryView>(playerInventory, inventoryView) });
            interactionPresenter = new InventoryInteractionPresenter(
                playerInventory,
                () => slotsViewContainer,
                inventory => inventoryGrids.TryGetGridView(inventory, out var gridView) ? gridView : null,
                screenPoint => inventoryGrids.TryGetInventoryAt(GetEventCamera(), screenPoint, out var inventory) ? inventory : null,
                GetEventCamera,
                () => true,
                itemRects,
                itemGrabRects);
            itemsView = new InventoryItemsView(uiConfig.FastSlotLabelFont, itemRects, itemGrabRects);
            inventoryGrids.EnsureTiles((_, parent, tile) => CreateTile(parent, tile));

            playerInventory.Changed
                           .Subscribe(_ => ReDraw())
                           .AddTo(redrawDisposables);
            playerInventory.HandSlot
                           .Subscribe(_ => ReDraw())
                           .AddTo(redrawDisposables);

            lastHpTarget = statsController.Hp.Value.Value;
            hpFillMode = HpFillMode.Synced;

            hpFiller.Current
                    .Subscribe(_ => RefreshHpFill())
                    .AddTo(redrawDisposables);
            statsController.Hp.Value
                           .Subscribe(OnHpTargetChanged)
                           .AddTo(redrawDisposables);
            statsController.Changed
                           .Subscribe(_ => PageUiUtilities.FillSlotsViewContainerStats(slotsViewContainer, statsController))
                           .AddTo(redrawDisposables);

            foreach (var statType in AdditionalStatTypes)
            {
                var currentStatType = statType;
                var filler = statFillers.Get(currentStatType);
                filler.Current
                      .Subscribe(_ => RefreshStatFill(currentStatType))
                      .AddTo(redrawDisposables);
                statsController.GetStat(currentStatType).Value
                               .Subscribe(_ => RefreshStatFill(currentStatType))
                               .AddTo(redrawDisposables);
            }
            playerInventory.CurrentWeightReactive
                           .Subscribe(UpdateWeightIndicator)
                           .AddTo(redrawDisposables);

            RefreshHpFill();
            RefreshAdditionalStatFills();
            UpdateWeightIndicator(playerInventory.CurrentWeight);
            DrawStatsHolderFastSlots();
            bloodScreen = PageUiUtilities.CreateBloodScreen(uiConfig, resolver, contentRect, Type);
            heartbeatPulse = new HeartbeatPulse(statsConfig, statsController.Hp, hpFiller);
            beatingHeart = new BeatingHeart(statsConfig, heartbeatPulse, statsHolder.HPHolder);
            bloodScreenController = new BloodScreenController(statsConfig, statsController.Hp, hpFiller, heartbeatPulse, bloodScreen);

            ReDraw();
        }

        public void Tick()
        {
            handPresenter?.UpdatePosition(handGrabOffset, TryGetSnappedHandPosition);

            HandleHoverPopup();

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

            popupController?.Close();
            ResetHoverPopupState();
            inventoryGrids.EnsureTiles((_, parent, tile) => CreateTile(parent, tile));
            inventoryGrids.SetScrollEnabled(playerInventory.HandSlot.Value?.ItemStack == null);
            itemRects.Clear();
            itemGrabRects.Clear();
            popupTargets.Clear();
            interactionPresenter.ClearInventoryItems();
            inventoryGrids.ClearItems();
            inventoryGrids.RenderItems(itemsView, (_, itemRect, item) =>
            {
                interactionPresenter.RegisterInventoryItem(playerInventory, itemRect, item);
                popupTargets.Add(new PopupTarget
                {
                    Rect = itemRect,
                    InventoryItem = item
                });
            });
            DrawSlotItems();
            handPresenter?.RenderAndPosition(handGrabOffset, TryGetSnappedHandPosition);
            UpdateInventoryInfo();
            UpdatePlayerInfo();
            DrawStatsHolderFastSlots();
            PageUiUtilities.FillSlotsViewContainerStats(slotsViewContainer, statsController);
            CacheSlotItems();
        }

        private void UpdatePlayerInfo()
        {
            PageUiUtilities.FillInfoAboutPlayer(infoAboutPlayer, characterInfo, playerMoneyStorage, playerFaction);
        }

        private void UpdateInventoryInfo()
        {
            var currentWeight = PageUiUtilities.GetItemsWeight(playerInventory)
                              + PageUiUtilities.GetSlotsWeight(playerInventory.HelmSlot, 
                                                               playerInventory.FaceSlot,
                                                               playerInventory.BodySlot,
                                                               playerInventory.HandsSlot,
                                                               playerInventory.ArmsSlot,
                                                               playerInventory.LegsSlot,
                                                               playerInventory.HipsSlot,
                                                               playerInventory.BackpackSlot, 
                                                               playerInventory.LeftWeaponSlot,
                                                               playerInventory.RightWeaponSlot,
                                                               playerInventory.HandSlot.Value);
            PageUiUtilities.FillInfoAboutInventory(infoAboutInventory, localizationConfig, colorsConfig, currentWeight, playerInventory.MaxWeight);
        }
        
        public bool TryCaptureGrabOffset(Vector2 screenPoint, out Vector2 handGrabOffset)
        {
            return interactionPresenter.TryCaptureGrabOffset(screenPoint, out handGrabOffset);
        }

        public void SetGrabOffset(Vector2 handGrabOffset)
        {
            interactionPresenter.SetGrabOffset(handGrabOffset);
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

        public bool TryGetPlacementTile(Vector2 screenPoint, IInventory inventory, out Tile tile)
        {
            return interactionPresenter.TryGetPlacementTile(screenPoint, inventory, out tile);
        }

        public bool TryGetPlacementTile(Vector2 screenPoint, out Tile tile)
        {
            return interactionPresenter.TryGetPlacementTile(screenPoint, playerInventory, out tile);
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
            playerSlotsView.Render((slotRect, slotModel) => popupTargets.Add(new PopupTarget
            {
                Rect = slotRect,
                SlotModel = slotModel
            }));
        }

        private void DrawStatsHolderFastSlots()
        {
            if (statsHolder == null)
            {
                return;
            }

            PageUiUtilities.DrawFastSlotItem(statsHolder.FastSlot1, playerInventory.FastSlot1, playerInventory.HasAnyInventoryItem(playerInventory.FastSlot1.ItemConfig), inputConfig, uiConfig.FastSlotLabelFont);
            PageUiUtilities.DrawFastSlotItem(statsHolder.FastSlot2, playerInventory.FastSlot2, playerInventory.HasAnyInventoryItem(playerInventory.FastSlot2.ItemConfig), inputConfig, uiConfig.FastSlotLabelFont);
            PageUiUtilities.DrawFastSlotItem(statsHolder.FastSlot3, playerInventory.FastSlot3, playerInventory.HasAnyInventoryItem(playerInventory.FastSlot3.ItemConfig), inputConfig, uiConfig.FastSlotLabelFont);
            PageUiUtilities.DrawFastSlotItem(statsHolder.FastSlot4, playerInventory.FastSlot4, playerInventory.HasAnyInventoryItem(playerInventory.FastSlot4.ItemConfig), inputConfig, uiConfig.FastSlotLabelFont);
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

        private bool HaveSlotsChanged()
        {
            return slotStateTracker.HasChanged();
        }

        private void CacheSlotItems()
        {
            slotStateTracker.Capture();
        }
        
        private bool TryGetPlacementCell(Vector2 screenPoint, out Vector2Int placementCell)
        {
            return InventoryGridGeometry.TryGetPlacementCell(
                inventoryView.ContentForTiles,
                GetEventCamera(),
                screenPoint,
                playerInventory.HandSlot.Value?.ItemStack,
                handGrabOffset,
                out placementCell);
        }

        private bool TryGetSnappedPositionInGridLocal(Vector2 screenPoint, out Vector3 snappedLocalPosition)
        {
            return interactionPresenter.TryGetSnappedGridPosition(screenPoint, playerInventory, out snappedLocalPosition);
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

        private void CreateTile(RectTransform parent, Tile gridTile)
        {
            var tile = resolver.Instantiate(uiConfig.Tile, parent);
            tile.Initialize(playerInventory, gridTile);
            tile.GetComponentInChildren<TMP_Text>().text = $"{gridTile.Index.x}:{gridTile.Index.y}";
        }

        private Camera GetEventCamera() => PageUiUtilities.GetEventCamera(canvas);
        
        public override void Hide()
        {
            bloodScreenController?.Dispose();
            bloodScreenController = null;

            beatingHeart?.Dispose();
            beatingHeart = null;

            heartbeatPulse?.Dispose();
            heartbeatPulse = null;

            redrawDisposables.Clear();
            itemRects.Clear();
            itemGrabRects.Clear();
            popupTargets.Clear();
            inventoryGrids.Clear();
            popupController?.Close();
            ResetHoverPopupState();
            
            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }

            contentRect = null;
            statsHolder = null;
            bloodScreen = null;
            sectionsLayoutRect = null;
            rightRect = null;
            rightPlayerInventory = null;
            infoAboutPlayer = null;
            infoAboutInventory = null;
            slotsViewContainer = null;
            playerSlotsView = null;
            inventoryView = null;
            itemsView = null;
            handPresenter = null;
            popupController = null;
            interactionContext.ClearActivePage(this);
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

            return popupController.Open(screenPoint, openMode, "Inventory Popup", (popupRoot, popupContent) =>
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
                        statsConfig.HpFullColor,
                        statsConfig.HpRecoveryColor,
                        statsConfig.HpDecreaseColor);
                }
                else
                {
                    CreatePopupButtons(target);
                }
            });
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
                && ReferenceEquals(first.SlotModel, second.SlotModel);
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

            if (CanMoveSlotItemToInventory(slotModel))
            {
                CreatePopupButton(localizationConfig.PopupActionMoveToInventory, () => ExecutePopupAction(() => MoveSlotItemToInventory(slotModel)));
            }

            CreatePopupButton(localizationConfig.PopupActionDrop, () => ExecutePopupAction(() => DropSlotItem(slotModel, slotModel.ItemStack?.Count ?? 0)));
        }

        private void CreateInventoryPopupButtons(PopupTarget target)
        {
            CreatePopupButton(localizationConfig.PopupActionUse, () => ExecutePopupAction(() => UseTarget(target)));

            var itemStack = target.InventoryItem?.ItemStack;
            var dropHalfCount = GetDropHalfCount(itemStack?.Count ?? 0);
            if (dropHalfCount > 0)
            {
                CreatePopupButton(localizationConfig.PopupActionDropHalf, () => ExecutePopupAction(() => DropTarget(target, dropHalfCount)));
            }

            CreatePopupButton(localizationConfig.PopupActionDrop, () => ExecutePopupAction(() => DropTarget(target, itemStack?.Count ?? 0)));
        }

        private void ExecutePopupAction(Action action)
        {
            popupController?.Close();
            ResetHoverPopupState();
            action?.Invoke();
        }

        private void CreatePopupButton(LocalizedString label, UnityEngine.Events.UnityAction onClick)
        {
            popupController?.CreateButton(label, onClick);
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
            if (!interactionPresenter.TryGetInventoryItemAt(screenPoint, out _, out var itemInInventory))
            {
                return false;
            }

            target = new PopupTarget
            {
                InventoryItem = itemInInventory
            };
            return true;
        }

        private static int GetDropHalfCount(int totalCount)
        {
            return totalCount > 1 ? totalCount / 2 : 0;
        }

        private bool CanMoveSlotItemToInventory(SlotModel slotModel)
        {
            return inventoryItemActionService.CanMoveSlotToGrid(slotModel);
        }

        private void MoveSlotItemToInventory(SlotModel slotModel)
        {
            inventoryItemActionService.TryMoveSlotToGrid(slotModel);
        }

        private void UseTarget(PopupTarget target)
        {
            if (target?.SlotModel?.ItemStack?.ItemConfig != null)
            {
                UseSlotItem(target.SlotModel);
                return;
            }

            if (target?.InventoryItem?.ItemStack?.ItemConfig != null)
            {
                UseInventoryItem(target.InventoryItem);
            }
        }

        private void UseInventoryItem(ItemInInventory itemInInventory)
        {
            inventoryItemActionService.TryUseInventoryItem(playerInventory, itemInInventory);
        }

        private void UseSlotItem(SlotModel slotModel)
        {
            inventoryItemActionService.TryUseSlotItem(slotModel);
        }

        private void DropTarget(PopupTarget target, int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (target?.SlotModel?.ItemStack?.ItemConfig != null)
            {
                DropSlotItem(target.SlotModel, count);
                return;
            }

            if (target?.InventoryItem?.ItemStack?.ItemConfig != null)
            {
                DropInventoryItem(target.InventoryItem, count);
            }
        }

        private void DropInventoryItem(ItemInInventory itemInInventory, int count)
        {
            inventoryItemActionService.TryDropInventoryItem(itemInInventory, count);
        }

        private void DropSlotItem(SlotModel slotModel, int count)
        {
            inventoryItemActionService.TryDropSlotItem(slotModel, count);
        }

        private void RefreshHpFill()
        {
            var hpHolder = statsHolder?.HPHolder;
            if (hpHolder == null || hpHolder.Fill == null || hpHolder.ChangedFill == null)
            {
                return;
            }

            var normalizedCurrent = GetNormalizedHp(hpFiller.Current.Value);
            var normalizedTarget = GetNormalizedHp(statsController.Hp.Value.Value);

            switch (ResolveHpFillDirection())
            {
                case HpFillMode.FillAnimated:
                    hpHolder.Fill.fillAmount = normalizedCurrent;
                    hpHolder.ChangedFill.fillAmount = normalizedTarget;
                    hpHolder.ChangedFill.color = statsConfig.HpRecoveryColor;
                    break;
                case HpFillMode.ChangedFillAnimated:
                    hpHolder.Fill.fillAmount = normalizedTarget;
                    hpHolder.ChangedFill.fillAmount = normalizedCurrent;
                    hpHolder.ChangedFill.color = statsConfig.HpDecreaseColor;
                    break;
                default:
                    hpHolder.Fill.fillAmount = normalizedTarget;
                    hpHolder.ChangedFill.fillAmount = normalizedTarget;
                    hpHolder.ChangedFill.color = statsConfig.HpFullColor;
                    break;
            }

            ApplyCriticalColor(hpHolder, statsController.Hp, normalizedTarget);
        }

        private void RefreshAdditionalStatFills()
        {
            foreach (var statType in AdditionalStatTypes)
            {
                RefreshStatFill(statType);
            }
        }

        private void RefreshStatFill(StatType statType)
        {
            var statHolder = statsHolder?.GetHolder(statType);
            if (statHolder == null || statHolder.Fill == null || statHolder.ChangedFill == null)
            {
                return;
            }

            var stat = statsController.GetStat(statType);
            var filler = statFillers.Get(statType);
            var normalizedCurrent = GetNormalizedStat(stat, filler.Current.Value);
            var normalizedTarget = GetNormalizedStat(stat, stat.Value.Value);

            if (normalizedTarget > normalizedCurrent)
            {
                statHolder.Fill.fillAmount = normalizedCurrent;
                statHolder.ChangedFill.fillAmount = normalizedTarget;
                statHolder.ChangedFill.color = statsConfig.HpRecoveryColor;
                ApplyCriticalColor(statHolder, stat, normalizedTarget);
                return;
            }

            if (normalizedTarget < normalizedCurrent)
            {
                statHolder.Fill.fillAmount = normalizedTarget;
                statHolder.ChangedFill.fillAmount = normalizedCurrent;
                statHolder.ChangedFill.color = statsConfig.HpDecreaseColor;
                ApplyCriticalColor(statHolder, stat, normalizedTarget);
                return;
            }

            statHolder.Fill.fillAmount = normalizedTarget;
            statHolder.ChangedFill.fillAmount = normalizedTarget;
            statHolder.ChangedFill.color = statsConfig.HpFullColor;
            ApplyCriticalColor(statHolder, stat, normalizedTarget);
        }

        private float GetNormalizedHp(float current)
        {
            var maxHp = statsController.Hp.Max;
            return Mathf.Approximately(maxHp, 0f) ? 0f : current / maxHp;
        }

        private static float GetNormalizedStat(Stat stat, float current)
        {
            return Mathf.Approximately(stat.Max, 0f) ? 0f : current / stat.Max;
        }

        private void ApplyCriticalColor(StatHolder statHolder, Stat stat, float normalizedTarget)
        {
            var safeThreshold = stat is SafeStat safeStat
                ? Mathf.Clamp01(safeStat.MinSafePercent)
                : 0f;
            var fillColor = normalizedTarget >= safeThreshold
                ? statsConfig.HpFullColor
                : Color.Lerp(statsConfig.HpDecreaseColor, statsConfig.HpFullColor, safeThreshold <= 0f ? 0f : normalizedTarget / safeThreshold);

            statHolder.Fill.color = fillColor;

            if (statHolder.Icon != null)
            {
                statHolder.Icon.color = fillColor;
            }
        }

        private void OnHpTargetChanged(float newTarget)
        {
            SelectHpFillMode(newTarget);
            lastHpTarget = newTarget;
            RefreshHpFill();
        }

        private HpFillMode ResolveHpFillDirection()
        {
            if (!Mathf.Approximately(hpFiller.Current.Value, statsController.Hp.Value.Value))
            {
                return hpFillMode;
            }

            return HpFillMode.Synced;
        }

        private void SelectHpFillMode(float newTarget)
        {
            var hpHolder = statsHolder?.HPHolder;
            if (hpHolder == null || hpHolder.Fill == null || hpHolder.ChangedFill == null)
            {
                return;
            }

            var target = GetNormalizedHp(newTarget);
            var fill = hpHolder.Fill.fillAmount;
            var changedFill = hpHolder.ChangedFill.fillAmount;

            var shouldAnimateFill = target > fill;
            var shouldAnimateChangedFill = target < changedFill;

            var nextMode = hpFillMode;
            if (shouldAnimateFill && shouldAnimateChangedFill)
            {
                nextMode = hpFillMode == HpFillMode.Synced
                    ? newTarget >= lastHpTarget
                        ? HpFillMode.FillAnimated
                        : HpFillMode.ChangedFillAnimated
                    : hpFillMode;
            }
            else if (shouldAnimateFill)
            {
                nextMode = HpFillMode.FillAnimated;
            }
            else if (shouldAnimateChangedFill)
            {
                nextMode = HpFillMode.ChangedFillAnimated;
            }
            else
            {
                nextMode = HpFillMode.Synced;
            }

            RebaseAnimatedFill(nextMode, fill, changedFill, target);
            hpFillMode = nextMode;
        }

        private void RebaseAnimatedFill(HpFillMode nextMode, float fill, float changedFill, float target)
        {
            var currentVisual = nextMode switch
            {
                HpFillMode.FillAnimated => fill,
                HpFillMode.ChangedFillAnimated => changedFill,
                _ => target
            };

            hpFiller.Current.Value = currentVisual * statsController.Hp.Max;
        }

        private void UpdateWeightIndicator(float currentWeight)
        {
            var indicator = statsHolder?.WeightIndicator;
            if (indicator == null)
            {
                return;
            }

            var currentPercent = playerInventory.CurrentWeightPercent;

            if (currentPercent >= inventoryConfig.WeightBlocksMovementPercent)
            {
                indicator.enabled = true;
                indicator.color = statsConfig.HpDecreaseColor;
                return;
            }

            if (currentPercent > inventoryConfig.WeightAffectsMovementPercent)
            {
                indicator.enabled = true;
                indicator.color = statsConfig.Warning;
                return;
            }

            indicator.enabled = false;
        }
    }
}
