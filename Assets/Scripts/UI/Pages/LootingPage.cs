using System;
using System.Collections.Generic;
using System.Linq;
using Colors;
using Factions;
using Inventory;
using Inventory.Grid;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Looting;
using Inventory.Slot;
using Input;
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
using UnityEngine.Localization;
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

        public override PageType Type { get; } = PageType.Looting;
        private readonly UIConfig uiConfig;
        private readonly StatsConfig statsConfig;
        private readonly StatFiller hpFiller;
        private readonly LocalizationConfig localizationConfig;
        private readonly ColorsConfig colorsConfig;
        private readonly StatIconsConfig statIconsConfig;
        private readonly PlayerInventory playerInventory;
        private readonly InputConfig inputConfig;
        private readonly LootTransferService lootTransferService;
        private readonly InventoryItemActionService inventoryItemActionService;
        private readonly MoneyStorage playerMoneyStorage;
        private readonly Character.CharacterInfo playerCharacterInfo;
        private readonly FactionConfig playerFaction;
        private readonly LootingContext lootingContext;
        private readonly StatsController statsController;
        private readonly Canvas canvas;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly InventoryInteractionContext interactionContext;

        private RectTransform contentRect = null!;
        private RectTransform sectionsLayoutRect = null!;
        private RectTransform rightRect = null!;
        private RightPlayerInventory rightPlayerInventory = null!;
        private RectTransform leftRect = null!;
        private LeftAnotherInventory leftAnotherInventory = null!;
        private InfoAboutPlayer rightInfoAboutPlayer = null!;
        private InfoAboutPlayer leftInfoAboutPlayer = null!;
        private InfoAboutInventory rightInfoAboutInventory = null!;
        private InfoAboutInventory leftInfoAboutInventory = null!;
        private SlotsViewContainer slotsViewContainer = null!;
        private PlayerInventorySlotsView playerSlotsView;
        private InventoryView playerInventoryView = null!;
        private InventoryView targetInventoryView = null!;
        private InventoryItemsView itemsView;
        private InventoryHandPresenter handPresenter;
        private readonly CompositeDisposable redrawDisposables = new();
        private readonly List<RectTransform> itemRects = new();
        private readonly List<RectTransform> itemGrabRects = new();
        private readonly List<PopupTarget> popupTargets = new();
        private readonly InventoryGridCollectionView inventoryGrids = new();
        private InventoryInteractionPresenter interactionPresenter;
        private readonly PlayerInventorySlotStateTracker slotStateTracker;
        private Vector2 handGrabOffset => interactionPresenter?.GrabOffset ?? Vector2.zero;
        private HeartbeatPulse heartbeatPulse;
        private BloodScreenController bloodScreenController;
        private Image bloodScreen;
        private InventoryPopupController popupController;
        private InventoryTooltipPresenter<PopupTarget> tooltipPresenter;


        public LootingPage(
            UIConfig uiConfig,
            StatsConfig statsConfig,
            StatFillers statFillers,
            LocalizationConfig localizationConfig,
            ColorsConfig colorsConfig,
            StatIconsConfig statIconsConfig,
            Canvas canvas,
            InputConfig inputConfig,
            PlayerInventory playerInventory,
            InventoryHandController inventoryHandController,
            MoneyStorage playerMoneyStorage,
            Character.CharacterInfo playerCharacterInfo,
            LootingContext lootingContext,
            StatsController statsController,
            IObjectResolver resolver,
            UI.Inventory.InventoryInteractionContext interactionContext)
        {
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            hpFiller = statFillers.Get(StatType.Hp);
            this.localizationConfig = localizationConfig;
            this.colorsConfig = colorsConfig;
            this.statIconsConfig = statIconsConfig;
            this.canvas = canvas;
            this.inputConfig = inputConfig;
            this.playerInventory = playerInventory;
            slotStateTracker = new PlayerInventorySlotStateTracker(playerInventory);
            lootTransferService = new LootTransferService(playerInventory, itemStack => inventoryHandController?.Drop(itemStack));
            inventoryItemActionService = new InventoryItemActionService(playerInventory, inventoryHandController);
            this.playerCharacterInfo = playerCharacterInfo;
            playerFaction = resolver.TryResolve<FactionConfig>(out var resolvedFaction) ? resolvedFaction : null;
            this.playerMoneyStorage = playerMoneyStorage;
            this.lootingContext = lootingContext;
            this.statsController = statsController;
            this.resolver = resolver;
            this.interactionContext = interactionContext;

            canvasRect = canvas.GetComponent<RectTransform>();
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

        public override void Draw()
        {
            var targetInventory = lootingContext.CurrentTargetInventory;
            if (targetInventory == null)
            {
                return;
            }

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
                inventory => inventoryGrids.TryGetInventoryView(inventory, out var view) ? view : null,
                GetEventCamera);

            sectionsLayoutRect = PageUiUtilities.CreateSectionsLayout(contentRect, Type.ToString());
            leftAnotherInventory = resolver.Instantiate(uiConfig.LeftAnotherInventory, sectionsLayoutRect);
            leftRect = leftAnotherInventory.GetComponent<RectTransform>();
            slotsViewContainer = resolver.Instantiate(uiConfig.CenterSection, sectionsLayoutRect);
            playerSlotsView = new PlayerInventorySlotsView(slotsViewContainer, playerInventory, inputConfig, uiConfig.FastSlotLabelFont, itemRects, itemGrabRects);
            PageUiUtilities.FillSlotsViewContainerStats(slotsViewContainer, statsController);
            rightPlayerInventory = resolver.Instantiate(uiConfig.RightPlayerInventory, sectionsLayoutRect);
            rightRect = rightPlayerInventory.GetComponent<RectTransform>();
            PageUiUtilities.RegisterSectionInLayout(leftRect);
            PageUiUtilities.RegisterSectionInLayout(slotsViewContainer.GetComponent<RectTransform>());
            PageUiUtilities.RegisterSectionInLayout(rightRect);

            rightInfoAboutPlayer = rightPlayerInventory.InfoAboutPlayer;
            rightInfoAboutInventory = rightPlayerInventory.InfoAboutInventory;
            playerInventoryView = rightPlayerInventory.InventoryView;
            itemsView = new InventoryItemsView(uiConfig.FastSlotLabelFont, itemRects, itemGrabRects);
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo, playerMoneyStorage, playerFaction);

            leftInfoAboutPlayer = leftAnotherInventory.InfoAboutPlayer;
            leftInfoAboutInventory = leftAnotherInventory.InfoAboutInventory;
            targetInventoryView = leftAnotherInventory.InventoryView;
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, lootingContext.CurrentTargetCharacterInfo, lootingContext.CurrentTargetMoneyStorage, lootingContext.CurrentTargetFaction);

            inventoryGrids.Configure(new[]
            {
                new KeyValuePair<IInventory, InventoryView>(playerInventory, playerInventoryView),
                new KeyValuePair<IInventory, InventoryView>(targetInventory, targetInventoryView)
            });
            interactionPresenter = new InventoryInteractionPresenter(
                playerInventory,
                () => slotsViewContainer,
                inventory => inventoryGrids.TryGetGridView(inventory, out var gridView) ? gridView : null,
                screenPoint => inventoryGrids.TryGetInventoryAt(GetEventCamera(), screenPoint, out var inventory) ? inventory : null,
                GetEventCamera,
                () => playerInventory.HandSourceInventory.Value == playerInventory,
                itemRects,
                itemGrabRects);
            inventoryGrids.EnsureTiles(CreateTile);
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
            handPresenter?.UpdatePosition(handGrabOffset, TryGetSnappedHandPosition);

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
            UpdatePlayersInfo();
            PageUiUtilities.FillSlotsViewContainerStats(slotsViewContainer, statsController);
            CacheSlotItems();
        }

        private void UpdatePlayersInfo()
        {
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo, playerMoneyStorage, playerFaction);
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, lootingContext.CurrentTargetCharacterInfo, lootingContext.CurrentTargetMoneyStorage, lootingContext.CurrentTargetFaction);
        }

        private void UpdateInventoryInfo()
        {
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

        private bool HaveGridChanged()
        {
            return inventoryGrids.HasGridSizeChanged();
        }

        private void CacheSlotItems()
        {
            slotStateTracker.Capture();
        }

        private bool TryGetSnappedPositionInInventoryGridLocal(Vector2 screenPoint, IInventory inventory, out Vector3 snappedLocalPosition)
        {
            return interactionPresenter.TryGetSnappedGridPosition(screenPoint, inventory, out snappedLocalPosition);
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

        private void CreateTile(IInventory inventory, RectTransform parent, Tile gridTile)
        {
            var tile = resolver.Instantiate(uiConfig.Tile, parent);
            tile.Initialize(inventory, gridTile);
            tile.GetComponentInChildren<TMP_Text>().text = $"{gridTile.Index.x}:{gridTile.Index.y}";
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

            return popupController.Open(screenPoint, openMode, "Looting Popup", (popupRoot, popupContent) =>
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

            CreatePopupButton(localizationConfig.PopupActionMove, () => ExecutePopupAction(() => MoveSlotItem(slotModel, slotModel.ItemStack?.Count ?? 0)), CanMoveSlotItem(slotModel));
        }

        private void CreateInventoryPopupButtons(PopupTarget target)
        {
            var itemStack = target.InventoryItem?.ItemStack;
            var itemCount = itemStack?.Count ?? 0;
            var moveHalfCount = GetDropHalfCount(itemCount);

            CreatePopupButton(localizationConfig.PopupActionUse, () => ExecutePopupAction(() => UseTarget(target)), CanUseInventoryItem(itemStack?.ItemConfig));

            if (moveHalfCount > 0)
            {
                CreatePopupButton(localizationConfig.PopupActionMoveHalf, () => ExecutePopupAction(() => MoveTarget(target, moveHalfCount)), CanMoveInventoryItem(target, moveHalfCount));
            }

            CreatePopupButton(localizationConfig.PopupActionMove, () => ExecutePopupAction(() => MoveTarget(target, itemCount)), CanMoveInventoryItem(target, itemCount));
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

        private bool CanUseInventoryItem(ItemConfig itemConfig)
        {
            return itemConfig != null && itemConfig.ItemType is ItemType.Usable
                or ItemType.Helm
                or ItemType.Face
                or ItemType.Body
                or ItemType.Hands
                or ItemType.Arms
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
            inventoryItemActionService.TryUseInventoryItem(sourceInventory, itemInInventory, TryStoreAcrossLootingInventories);
        }

        private ItemStack TryStoreAcrossLootingInventories(ItemStack itemStack)
        {
            return lootTransferService.TryStoreAcrossInventories(lootingContext.CurrentTargetInventory, itemStack);
        }

        private bool CanMoveSlotItem(SlotModel slotModel)
        {
            return slotModel?.ItemStack?.ItemConfig != null
                   && lootTransferService.CanMove(lootingContext.CurrentTargetInventory, playerInventory, slotModel.ItemStack);
        }

        private bool CanMoveInventoryItem(PopupTarget target, int count)
        {
            return count > 0
                   && target?.InventoryItem?.ItemStack?.ItemConfig != null
                   && lootTransferService.CanMove(lootingContext.CurrentTargetInventory, target.SourceInventory, target.InventoryItem.ItemStack);
        }

        private static int GetDropHalfCount(int totalCount)
        {
            return totalCount > 1 ? totalCount / 2 : 0;
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
            lootTransferService.TryMovePlayerSlot(lootingContext.CurrentTargetInventory, slotModel, count);
        }

        private void MoveInventoryItem(IInventory sourceInventory, ItemInInventory itemInInventory, int count)
        {
            lootTransferService.TryMoveInventoryItem(lootingContext.CurrentTargetInventory, sourceInventory, itemInInventory, count);
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

        private Camera GetEventCamera() => PageUiUtilities.GetEventCamera(canvas);

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
            inventoryGrids.Clear();
            popupController?.Close();
            ResetHoverPopupState();

            if (contentRect)
            {
                UnityEngine.Object.Destroy(contentRect.gameObject);
            }

            contentRect = null;
            sectionsLayoutRect = null;
            rightRect = null;
            rightPlayerInventory = null;
            leftRect = null;
            leftAnotherInventory = null;
            rightInfoAboutPlayer = null;
            leftInfoAboutPlayer = null;
            rightInfoAboutInventory = null;
            leftInfoAboutInventory = null;
            slotsViewContainer = null;
            playerSlotsView = null;
            bloodScreen = null;
            playerInventoryView = null;
            targetInventoryView = null;
            itemsView = null;
            handPresenter = null;
            popupController = null;
            interactionContext.ClearActivePage(this);
        }
    }
}
