using System.Collections.Generic;
using Inventory;
using TMPro;
using UI.Configs;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class InventoryPage : BasePage
    {
        public override PageType Type { get; } = PageType.Inventory;

        private readonly UIConfig uiConfig;
        private readonly PlayerInventory playerInventory;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;

        private RectTransform contentRect = null!;
        private RectTransform rightRect = null!;
        private Inventory.InventoryView inventoryView = null!;
        private RectTransform handSlotRect = null!;
        private readonly CompositeDisposable redrawDisposables = new();

        public InventoryPage
            (
                UIConfig uiConfig,
                Canvas canvas,
                PlayerInventory playerInventory,
                IObjectResolver resolver
            )   
        {
            this.uiConfig = uiConfig;
            this.playerInventory = playerInventory;
            this.resolver = resolver;
            
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            rightRect = resolver.Instantiate(uiConfig.RightSection, contentRect);
            var infoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            var infoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            inventoryView  = resolver.Instantiate(uiConfig.InventoryView, rightRect);

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
        
        public void ReDraw()
        {
            if (!inventoryView)
            {
                return;
            }

            ClearChildren(inventoryView.ContentForItems);
            DrawItems(inventoryView);
            DrawHandSlot();
        }
private void DrawHandSlot()
        {
            if (handSlotRect)
            {
                Object.Destroy(handSlotRect.gameObject);
            }

            var handSlotObject = new GameObject("Hand Slot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handSlotRect = handSlotObject.GetComponent<RectTransform>();
            handSlotRect.SetParent(rightRect, false);
            handSlotRect.anchorMin = new Vector2(1, 1);
            handSlotRect.anchorMax = new Vector2(1, 1);
            handSlotRect.pivot = new Vector2(1, 1);
            handSlotRect.anchoredPosition = new Vector2(-20, -20);
            handSlotRect.sizeDelta = new Vector2(96, 96);

            var backgroundImage = handSlotObject.GetComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.4f);

            var handItemConfig = playerInventory.HandSlot.Value?.ItemConfig;
            if (handItemConfig == null)
            {
                return;
            }

            var handItemObject = new GameObject($"Hand Item [{handItemConfig.Id}]", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var handItemRect = handItemObject.GetComponent<RectTransform>();
            handItemRect.SetParent(handSlotRect, false);
            handItemRect.anchorMin = Vector2.zero;
            handItemRect.anchorMax = Vector2.one;
            handItemRect.offsetMin = new Vector2(8, 8);
            handItemRect.offsetMax = new Vector2(-8, -8);

            var handItemImage = handItemObject.GetComponent<Image>();
            handItemImage.sprite = handItemConfig.Icon;
            handItemImage.preserveAspect = true;
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

                var itemCenterPosition = item.Position.GetColumn(3);
                itemImageRect.anchoredPosition = new Vector2(
                                                             gridLayoutGroup.padding.left
                                                           + (itemCenterPosition.x + 0.5f) * gridLayoutGroup.cellSize.x
                                                           + itemCenterPosition.x * gridLayoutGroup.spacing.x,
                                                             -(gridLayoutGroup.padding.top
                                                             + (itemCenterPosition.y + 0.5f) *
                                                               gridLayoutGroup.cellSize.y
                                                             + itemCenterPosition.y * gridLayoutGroup.spacing.y));

                var itemImage = itemImageObject.GetComponent<Image>();
                itemImage.sprite = item.ItemConfig.Icon;
                itemImage.preserveAspect = true;
            }
        }

        public override void Hide()
        {
            redrawDisposables.Clear();
            
            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }

            contentRect = null;
            rightRect = null;
            inventoryView = null;
            handSlotRect = null;
        }
    }
}