using System.Collections.Generic;
using Dialogue;
using GameModes;
using Inventory.Inventories;
using Inventory.Slot;
using MessagePipe;
using Messages;
using UI.Configs;
using UI.Inventory;
using UI.UIElements;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using TMPro;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class TradePage : BasePage
    {
        public override PageType Type { get; } = PageType.Trade;

        private readonly UIConfig uiConfig;
        private readonly PlayerInventory playerInventory;
        private readonly Character.CharacterInfo playerCharacterInfo;
        private readonly DialogueContext dialogueContext;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect;
        private RectTransform leftRect;
        private RectTransform rightRect;
        private SlotsViewContainer centerSection;
        private InventoryView playerInventoryView;
        private InventoryView targetInventoryView;
        private readonly CompositeDisposable redrawDisposables = new();
        private Button tradingExitButton;
        private Vector2Int lastPlayerGridSize = new(-1, -1);

        public TradePage
            (
                UIConfig uiConfig,
                PlayerInventory playerInventory,
                Character.CharacterInfo playerCharacterInfo,
                DialogueContext dialogueContext,
                Canvas canvas,
                IObjectResolver resolver,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher
            )
        {
            this.uiConfig = uiConfig;
            this.playerInventory = playerInventory;
            this.playerCharacterInfo = playerCharacterInfo;
            this.dialogueContext = dialogueContext;
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

            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            leftRect = resolver.Instantiate(uiConfig.LeftSection, contentRect);
            centerSection = resolver.Instantiate(uiConfig.CenterSection, contentRect);
            rightRect = resolver.Instantiate(uiConfig.RightSection, contentRect);

            var leftInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, leftRect);
            resolver.Instantiate(uiConfig.InfoAboutInventory, leftRect);
            resolver.Instantiate(uiConfig.SellInfo, leftRect);
            targetInventoryView = resolver.Instantiate(uiConfig.InventoryInTrading, leftRect).GetComponent<InventoryView>();
            PageUiUtilities.FillInfoAboutPlayer(leftInfoAboutPlayer, dialogueContext.CurrentTargetCharacterInfo);

            var rightInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            resolver.Instantiate(uiConfig.SellInfo, rightRect);
            playerInventoryView = resolver.Instantiate(uiConfig.InventoryInTrading, rightRect);
            PageUiUtilities.FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo);

            DrawTiles(playerInventory, playerInventoryView);
            DrawInventory(playerInventory, playerInventoryView);
            DrawInventory(targetInventory, targetInventoryView);
            DrawSlotItems();

            playerInventory.Items.ObserveCountChanged().Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            targetInventory.Items.ObserveCountChanged().Subscribe(_ => ReDraw()).AddTo(redrawDisposables);

            tradingExitButton = resolver.Instantiate(uiConfig.TradingExitButton, centerSection.transform);
            tradingExitButton.onClick.AddListener(ReturnToDialogue);
        }

        public override void Hide()
        {
            redrawDisposables.Clear();

            if (tradingExitButton)
            {
                tradingExitButton.onClick.RemoveListener(ReturnToDialogue);
                tradingExitButton = null;
            }

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }

            playerInventoryView = null;
            targetInventoryView = null;
        }
        
        private void ReDraw()
        {
            EnsurePlayerTilesMatchInventorySize();
            DrawInventory(playerInventory, playerInventoryView);
            DrawInventory(dialogueContext.CurrentTargetInventory, targetInventoryView);
            DrawSlotItems();
        }
        
        private void DrawSlotItems()
        {
            DrawSlotItem(centerSection.HeadSlot, playerInventory.HelmSlot);
            DrawSlotItem(centerSection.BodySlot, playerInventory.BodySlot);
            DrawSlotItem(centerSection.BackpackSlot, playerInventory.BackpackSlot);
        }

        private static void DrawSlotItem(SlotView slotView, SlotModel slotModel)
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
            PageUiUtilities.ClearChildren(slotRect);
            if (slotModel?.ItemConfig == null)
            {
                return;
            }

            var itemImageRect = PageUiUtilities.CreateItemImage(slotRect, slotModel.ItemConfig, "Slot Item");
            itemImageRect.anchorMin = new Vector2(0.5f, 0.5f);
            itemImageRect.anchorMax = new Vector2(0.5f, 0.5f);
            itemImageRect.anchoredPosition = Vector2.zero;
        }
        
        private void DrawTiles(PlayerInventory inventory, InventoryView inventoryView)
        {
            if (inventory == null || inventoryView == null)
            {
                return;
            }
            PageUiUtilities.ClearChildren(inventoryView.ContentForTiles);
            var gridWidth = inventory.Tiles.tiles.GetLength(0);
            var gridHeight = inventory.Tiles.tiles.GetLength(1);

            for (var y = 0; y < gridHeight; y++)
            for (var x = 0; x < gridWidth; x++)
            {
                var tile = resolver.Instantiate(uiConfig.Tile, inventoryView.ContentForTiles);
                tile.Initialize(inventory, inventory.Tiles.GetTile(x, y));
                tile.GetComponentInChildren<TMP_Text>().text = $"{x}:{y}";
            }

            lastPlayerGridSize = new Vector2Int(gridWidth, gridHeight);
        }

        private void EnsurePlayerTilesMatchInventorySize()
        {
            if (playerInventoryView == null)
            {
                return;
            }

            var currentSize = new Vector2Int(playerInventory.Tiles.tiles.GetLength(0), playerInventory.Tiles.tiles.GetLength(1));
            if (currentSize == lastPlayerGridSize)
            {
                return;
            }

            DrawTiles(playerInventory, playerInventoryView);
        }
        
        private void DrawInventory(IInventory inventory, InventoryView inventoryView)
        {
            if (inventory == null || inventoryView == null)
            {
                return;
            }

            PageUiUtilities.ClearChildren(inventoryView.ContentForItems);
            DrawItems(inventory, inventoryView);
        }

        private static void DrawItems(IInventory inventory, InventoryView inventoryView)
        {
            var gridLayoutGroup = inventoryView.ContentForTiles.GetComponent<GridLayoutGroup>();
            if (gridLayoutGroup == null)
            {
                return;
            }

            foreach (var item in inventory.Items)
            {
                var itemImageRect = PageUiUtilities.CreateItemImage(inventoryView.ContentForItems, item.ItemConfig, "Item");

                var itemCenterPosition = item.Position.GetColumn(3);
                itemImageRect.anchoredPosition = PageUiUtilities.GetItemAnchoredPosition(gridLayoutGroup, itemCenterPosition);
            }
        }
        
        private void ReturnToDialogue()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }
    }
}