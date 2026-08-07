using System.Collections.Generic;
using Colors;
using System;
using Factions;
using Inventory;
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
using Messages;
using Money;
using Stats;
using UI.UIElements;
using CharacterInfo = Character.CharacterInfo;
using Object = UnityEngine.Object;
using UnityEngine.InputSystem;

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

        private enum PopupOpenMode
        {
            None,
            Hover,
            RightClick
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
        private readonly InventoryHandController inventoryHandController;
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
        private Inventory.InventoryView inventoryView = null!;
        private RectTransform handSlotRect = null!;
        private readonly CompositeDisposable redrawDisposables = new();
        private ScrollRect inventoryScrollRect = null!;
        private readonly List<RectTransform> itemRects = new();
        private readonly List<RectTransform> itemGrabRects = new();
        private readonly List<PopupTarget> popupTargets = new();
        private Vector2 handGrabOffset;
        private BeatingHeart beatingHeart;
        private HeartbeatPulse heartbeatPulse;
        private BloodScreenController bloodScreenController;
        private Image bloodScreen;
        private RectTransform popupRect;
        private RectTransform popupContentRect;
        private RectTransform popupParentRect;
        private PopupTarget hoverPopupTarget;
        private float hoverPopupElapsed;
        private PopupOpenMode popupOpenMode;

        private ItemConfig lastHelmItemConfig;
        private ItemConfig lastFaceItemConfig;
        private ItemConfig lastBodyItemConfig;
        private ItemConfig lastHandsItemConfig;
        private ItemConfig lastArmsSlotItemConfig;
        private ItemConfig lastLegsItemConfig;
        private ItemConfig lastHipsItemConfig;
        private ItemConfig lastBackpackItemConfig;
        private ItemConfig lastLeftWeaponItemConfig;
        private ItemConfig lastRightWeaponItemConfig;
        private ItemConfig lastFastSlot1ItemConfig;
        private ItemConfig lastFastSlot2ItemConfig;
        private ItemConfig lastFastSlot3ItemConfig;
        private ItemConfig lastFastSlot4ItemConfig;
        private Vector2Int lastGridSize = new(-1, -1);
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
                PlayerInventory playerInventory,
                InventoryHandController inventoryHandController,
                MoneyStorage playerMoneyStorage,
                CharacterInfo characterInfo,
                FactionConfig playerFaction,
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
            this.playerInventory = playerInventory;
            this.inventoryHandController = inventoryHandController;
            this.playerMoneyStorage = playerMoneyStorage;
            this.characterInfo = characterInfo;
            this.playerFaction = playerFaction;
            this.resolver = resolver;
            this.interactionContext = interactionContext;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            interactionContext.SetActivePage(this);
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";
            popupParentRect = contentRect;
            statsHolder = resolver.Instantiate(uiConfig.StatsHolder, contentRect);
            statsHolder.name = $"{uiConfig.StatsHolder.name} | {Type}";

            sectionsLayoutRect = PageUiUtilities.CreateSectionsLayout(contentRect, Type.ToString());
            PageUiUtilities.CreateSectionPlaceholder(sectionsLayoutRect, "Left");
            slotsViewContainer = resolver.Instantiate(uiConfig.CenterSection, sectionsLayoutRect);
            rightPlayerInventory = resolver.Instantiate(uiConfig.RightPlayerInventory, sectionsLayoutRect);
            rightRect = rightPlayerInventory.GetComponent<RectTransform>();
            PageUiUtilities.RegisterSectionInLayout(slotsViewContainer.GetComponent<RectTransform>());
            PageUiUtilities.RegisterSectionInLayout(rightRect);
            PageUiUtilities.FillSlotsViewContainerStats(slotsViewContainer, statsController);
            
            infoAboutPlayer = rightPlayerInventory.InfoAboutPlayer;
            infoAboutInventory = rightPlayerInventory.InfoAboutInventory;
            inventoryView = rightPlayerInventory.InventoryView;
            inventoryScrollRect = inventoryView.GetComponent<ScrollRect>();
            PageUiUtilities.FillInfoAboutPlayer(infoAboutPlayer, characterInfo, playerMoneyStorage, playerFaction);
            
            DrawTiles();

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
            if (handSlotRect)
            {
                UpdateHandSlotPosition();
            }

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

            ClosePopup();
            ResetHoverPopupState();
            EnsureTilesMatchInventorySize();
            UpdateInventoryScrollState();
            itemRects.Clear();
            itemGrabRects.Clear();
            popupTargets.Clear();
            PageUiUtilities.ClearChildren(inventoryView.ContentForItems);
            DrawItems(inventoryView);
            DrawSlotItems();
            DrawHandSlot();
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
            var eventCamera = GetEventCamera();
            var captured = PageUiUtilities.TryCaptureGrabOffset(itemRects, itemGrabRects, screenPoint, eventCamera, out handGrabOffset);
            this.handGrabOffset = handGrabOffset;
            return captured;
        }

        public void SetGrabOffset(Vector2 handGrabOffset)
        {
            this.handGrabOffset = handGrabOffset;
        }

        public bool TryHandleMouseDown(MouseButtonType button, Vector2 screenPoint)
        {
            if (interactionContext.ActivePage != this || contentRect == null || playerInventory.HandSlot.Value?.ItemStack != null)
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
                : PageUiUtilities.GetItemGrabSize(gridLayoutGroup, handItemStack.Size);
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

            if (TryGetSlotUnderPointer(slotsViewContainer.ArmsSlot, playerInventory.ArmsSlot, screenPoint, handItemType, out slotModel))
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
            if (!slotsViewContainer || playerInventory.HandSlot.Value?.ItemConfig?.ItemType != ItemType.Usable)
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
            DrawSlotItem(slotsViewContainer.FaceSlot, playerInventory.FaceSlot);
            DrawSlotItem(slotsViewContainer.BodySlot, playerInventory.BodySlot);
            DrawSlotItem(slotsViewContainer.HandsSlot, playerInventory.HandsSlot);
            DrawSlotItem(slotsViewContainer.ArmsSlot, playerInventory.ArmsSlot);
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
                SlotModel = slotModel
            });
        }

        private void DrawFastSlotItem(SlotView slotView, FastSlotModel fastSlotModel)
        {
            PageUiUtilities.DrawFastSlotItem(slotView, fastSlotModel, playerInventory.HasAnyInventoryItem(fastSlotModel?.ItemConfig));
        }

        private void DrawStatsHolderFastSlots()
        {
            if (statsHolder == null)
            {
                return;
            }

            PageUiUtilities.DrawFastSlotItem(statsHolder.FastSlot1, playerInventory.FastSlot1, playerInventory.HasAnyInventoryItem(playerInventory.FastSlot1.ItemConfig));
            PageUiUtilities.DrawFastSlotItem(statsHolder.FastSlot2, playerInventory.FastSlot2, playerInventory.HasAnyInventoryItem(playerInventory.FastSlot2.ItemConfig));
            PageUiUtilities.DrawFastSlotItem(statsHolder.FastSlot3, playerInventory.FastSlot3, playerInventory.HasAnyInventoryItem(playerInventory.FastSlot3.ItemConfig));
            PageUiUtilities.DrawFastSlotItem(statsHolder.FastSlot4, playerInventory.FastSlot4, playerInventory.HasAnyInventoryItem(playerInventory.FastSlot4.ItemConfig));
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
                || lastArmsSlotItemConfig != playerInventory.ArmsSlot.ItemConfig
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

        private void CacheSlotItems()
        {
            lastHelmItemConfig = playerInventory.HelmSlot.ItemConfig;
            lastFaceItemConfig = playerInventory.FaceSlot.ItemConfig;
            lastBodyItemConfig = playerInventory.BodySlot.ItemConfig;
            lastHandsItemConfig = playerInventory.HandsSlot.ItemConfig;
            lastArmsSlotItemConfig = playerInventory.ArmsSlot.ItemConfig;
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
        
        private bool TryGetPlacementCell(Vector2 screenPoint, out Vector2Int placementCell)
        {
            placementCell = default;
            if (!TryGetCursorCellAndLayout(screenPoint, out var cursorCell, out var gridLayoutGroup))
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

        private bool TryGetSnappedPositionInGridLocal(Vector2 screenPoint, out Vector3 snappedLocalPosition)
        {
            snappedLocalPosition = Vector3.zero;
            if (!TryGetPlacementCell(screenPoint, out var placementCell))
            {
                return false;
            }

            var handItemStack = playerInventory.HandSlot.Value?.ItemStack;
            var gridLayoutGroup = inventoryView.ContentForTiles.GetComponent<GridLayoutGroup>();
            if (handItemStack?.ItemConfig == null || gridLayoutGroup == null)
            {
                return false;
            }
            
            var gridWidth = playerInventory.Tiles.tiles.GetLength(0);
            var gridHeight = playerInventory.Tiles.tiles.GetLength(1);
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
                var itemGrabSize = PageUiUtilities.GetItemGrabSize(gridLayoutGroup, item.ItemStack.Size);
                var itemImageRect = PageUiUtilities.CreateItemImage(inventory.ContentForItems, item.ItemStack, "Item", itemGrabSize);
                itemRects.Add(itemImageRect);
                popupTargets.Add(new PopupTarget
                {
                    Rect = itemImageRect,
                    InventoryItem = item
                });
                
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
            ClosePopup();
            ResetHoverPopupState();
            
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
            bloodScreen = null;
            sectionsLayoutRect = null;
            rightRect = null;
            rightPlayerInventory = null;
            infoAboutPlayer = null;
            infoAboutInventory = null;
            slotsViewContainer = null;
            inventoryView = null;
            inventoryScrollRect = null;
            handSlotRect = null;
            popupRect = null;
            popupContentRect = null;
            popupParentRect = null;
            interactionContext.ClearActivePage(this);
        }

        private void HandleHoverPopup()
        {
            if (interactionContext.ActivePage != this || contentRect == null)
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

            popupRect = PageUiUtilities.CreatePopupRoot(
                popupParentRect,
                uiConfig,
                resolver,
                openMode == PopupOpenMode.Hover,
                "Inventory Popup");
            PageUiUtilities.SetPopupRaycastState(popupRect, openMode == PopupOpenMode.RightClick);

            var itemConfig = GetPopupItemConfig(target);
            var itemStack = GetPopupItemStack(target);
            if (itemConfig == null)
            {
                ClosePopup();
                return false;
            }

            popupContentRect = PageUiUtilities.CreatePopupContent(popupRect, uiConfig, resolver, openMode == PopupOpenMode.RightClick);
            if (popupContentRect == null)
            {
                ClosePopup();
                return false;
            }

            if (openMode == PopupOpenMode.Hover)
            {
                PageUiUtilities.FillInventoryHoverPopup(
                    popupRect,
                    popupContentRect,
                    uiConfig,
                    localizationConfig,
                    statIconsConfig,
                    statsController,
                    playerInventory,
                    resolver,
                    itemConfig,
                    itemStack,
                    target?.SlotModel?.ItemConfig == itemConfig,
                    statsConfig.HpFullColor,
                    statsConfig.HpRecoveryColor,
                    statsConfig.HpDecreaseColor);
            }
            else if (openMode == PopupOpenMode.RightClick)
            {
                CreatePopupButtons(target);
            }

            PageUiUtilities.RecalculatePopupLayout(popupRect, popupContentRect);
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
                CreatePopupButton("Move in Inventory", () => ExecutePopupAction(() => MoveSlotItemToInventory(slotModel)));
            }

            CreatePopupButton("Drop", () => ExecutePopupAction(() => DropSlotItem(slotModel, slotModel.ItemStack?.Count ?? 0)));
        }

        private void CreateInventoryPopupButtons(PopupTarget target)
        {
            CreatePopupButton("Use", () => ExecutePopupAction(() => UseTarget(target)));

            var itemStack = target.InventoryItem?.ItemStack;
            var dropHalfCount = GetDropHalfCount(itemStack?.Count ?? 0);
            if (dropHalfCount > 0)
            {
                CreatePopupButton("Drop Half", () => ExecutePopupAction(() => DropTarget(target, dropHalfCount)));
            }

            CreatePopupButton("Drop", () => ExecutePopupAction(() => DropTarget(target, itemStack?.Count ?? 0)));
        }

        private void ExecutePopupAction(Action action)
        {
            ClosePopup();
            ResetHoverPopupState();
            action?.Invoke();
        }

        private void CreatePopupButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            PageUiUtilities.CreatePopupButton(popupContentRect, uiConfig, resolver, label, onClick);
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
            if (!TryGetCursorCellAndLayout(screenPoint, out var cursorCell, out _)
             || !playerInventory.Tiles.TryGetTile(cursorCell.x, cursorCell.y, out var tile)
             || (tile.ItemInInventory is not { } itemInInventory)
             || itemInInventory?.ItemStack?.ItemConfig == null)
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
            return slotModel?.ItemStack?.ItemConfig != null && playerInventory.CanMoveSlotItemToGrid(slotModel);
        }

        private void MoveSlotItemToInventory(SlotModel slotModel)
        {
            if (slotModel?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            playerInventory.TryMoveSlotItemToGrid(slotModel);
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
            if (itemInInventory?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            if (itemInInventory.ItemStack.ItemConfig.ItemType == ItemType.Usable)
            {
                UseUsableInventoryItem(itemInInventory);
                return;
            }

            var itemStack = itemInInventory.ItemStack.Clone();
            var originalPosition = itemInInventory.Position;
            playerInventory.Remove(itemInInventory);

            if (!inventoryHandController.TryUseFromInventory(itemStack))
            {
                playerInventory.Add(itemStack, originalPosition);
            }
        }

        private void UseUsableInventoryItem(ItemInInventory itemInInventory)
        {
            var sourceStack = itemInInventory?.ItemStack;
            if (sourceStack?.ItemConfig == null)
            {
                return;
            }

            var itemConfig = sourceStack.ItemConfig;
            var isRotated = sourceStack.IsRotated;
            var originalPosition = itemInInventory.Position;
            var originalCount = sourceStack.Count;
            var usedStack = new ItemStack(itemConfig, 1, isRotated);

            playerInventory.Remove(itemInInventory);
            if (!inventoryHandController.TryUseFromInventory(usedStack))
            {
                playerInventory.Add(new ItemStack(itemConfig, originalCount, isRotated), originalPosition);
                return;
            }

            if (originalCount > 1)
            {
                playerInventory.Add(new ItemStack(itemConfig, originalCount - 1, isRotated), originalPosition);
            }
        }

        private void UseSlotItem(SlotModel slotModel)
        {
            if (slotModel?.ItemStack?.ItemConfig == null || !playerInventory.TryTakeFromSlot(slotModel, out var itemStack))
            {
                return;
            }

            if (inventoryHandController.TryUseFromInventory(itemStack))
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
            if (itemInInventory?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            var itemConfig = itemInInventory.ItemConfig;
            var isRotated = itemInInventory.ItemStack.IsRotated;
            var originalPosition = itemInInventory.Position;
            var currentCount = itemInInventory.Count;
            var dropCount = Mathf.Clamp(count, 1, currentCount);
            var remainingCount = currentCount - dropCount;

            playerInventory.Remove(itemInInventory);
            if (remainingCount > 0)
            {
                playerInventory.Add(new ItemStack(itemConfig, remainingCount, isRotated), originalPosition);
            }

            inventoryHandController.Drop(new ItemStack(itemConfig, dropCount, isRotated));
        }

        private void DropSlotItem(SlotModel slotModel, int count)
        {
            if (slotModel?.ItemStack?.ItemConfig == null)
            {
                return;
            }

            var dropCount = Mathf.Clamp(count, 1, slotModel.ItemStack.Count);
            if (playerInventory.TryTakeFromSlot(slotModel, dropCount, out var itemStack))
            {
                if (slotModel.ItemType == ItemType.Backpack)
                {
                    var droppedItems = playerInventory.RebuildInventoryFromCurrentBackpack();
                    foreach (var droppedItem in droppedItems)
                    {
                        inventoryHandController.Drop(droppedItem);
                    }
                }

                inventoryHandController.Drop(itemStack);
            }
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
            popupContentRect = null;
            popupOpenMode = PopupOpenMode.None;
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
