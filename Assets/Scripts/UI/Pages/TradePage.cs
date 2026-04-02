using System.Collections.Generic;
using Dialogue;
using GameModes;
using Inventory.Inventories;
using Inventory.Slot;
using Localization;
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
            FillInfoAboutPlayer(leftInfoAboutPlayer, dialogueContext.CurrentTargetCharacterInfo);

            var rightInfoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            resolver.Instantiate(uiConfig.SellInfo, rightRect);
            playerInventoryView = resolver.Instantiate(uiConfig.InventoryInTrading, rightRect);
            FillInfoAboutPlayer(rightInfoAboutPlayer, playerCharacterInfo);

            DrawTiles(playerInventory, playerInventoryView);
            DrawInventory(playerInventory, playerInventoryView);
            DrawInventory(targetInventory, targetInventoryView);
            DrawSlotItems();

            playerInventory.Items.ObserveCountChanged().Subscribe(_ => ReDraw()).AddTo(redrawDisposables);
            targetInventory.Items.ObserveCountChanged().Subscribe(_ => ReDraw()).AddTo(redrawDisposables);

            tradingExitButton = resolver.Instantiate(uiConfig.TradingExitButton, centerSection.transform);
            tradingExitButton.onClick.AddListener(ReturnToDialogue);
        }

        private static void FillInfoAboutPlayer(InfoAboutPlayer infoAboutPlayer, Character.CharacterInfo currentCharacterInfo)
        {
            if (infoAboutPlayer == null || currentCharacterInfo == null)
            {
                return;
            }

            infoAboutPlayer.Photo.sprite = currentCharacterInfo.Photo;
            infoAboutPlayer.Name.text = currentCharacterInfo.Name.GetLocalizedStringCached();
            infoAboutPlayer.Group.text = currentCharacterInfo.Fraction.GetLocalizedStringCached();
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

            var itemImage = itemImageObject.GetComponent<Image>();
            itemImage.sprite = slotModel.ItemConfig.Icon;
            itemImage.preserveAspect = true;
            itemImage.raycastTarget = false;
        }
        
        private void DrawTiles(PlayerInventory inventory, InventoryView inventoryView)
        {
            if (inventory == null || inventoryView == null)
            {
                return;
            }

            ClearChildren(inventoryView.ContentForTiles);
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

            ClearChildren(inventoryView.ContentForItems);
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
                var itemImageObject = new GameObject($"Item [{item.ItemConfig.Id}]", typeof(RectTransform),
                                                     typeof(CanvasRenderer), typeof(Image));
                var itemImageRect = itemImageObject.GetComponent<RectTransform>();
                itemImageRect.SetParent(inventoryView.ContentForItems, false);
                itemImageRect.anchorMin = new Vector2(0, 1);
                itemImageRect.anchorMax = new Vector2(0, 1);
                itemImageRect.pivot = new Vector2(0.5f, 0.5f);
                itemImageRect.sizeDelta = item.ItemConfig.SizeInInventory;

                var itemCenterPosition = item.Position.GetColumn(3);
                itemImageRect.anchoredPosition = new Vector2(
                                                       gridLayoutGroup.padding.left
                                                     + (itemCenterPosition.x + 0.5f) * gridLayoutGroup.cellSize.x
                                                     + itemCenterPosition.x * gridLayoutGroup.spacing.x,
                                                       -(gridLayoutGroup.padding.top
                                                       + (itemCenterPosition.y + 0.5f) * gridLayoutGroup.cellSize.y
                                                       + itemCenterPosition.y * gridLayoutGroup.spacing.y));

                var itemImage = itemImageObject.GetComponent<Image>();
                itemImage.sprite = item.ItemConfig.Icon;
                itemImage.preserveAspect = true;
                itemImage.raycastTarget = false;
            }
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
        private void ReturnToDialogue()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }
    }
}