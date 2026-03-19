using Inventory;
using TMPro;
using UI.Configs;
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

            var rightRect = resolver.Instantiate(uiConfig.RightSection, contentRect);
            var infoAboutPlayer = resolver.Instantiate(uiConfig.InfoAboutPlayer, rightRect);
            var infoAboutInventory = resolver.Instantiate(uiConfig.InfoAboutInventory, rightRect);
            var inventory = resolver.Instantiate(uiConfig.InventoryView, rightRect);

            for (var y = 0; y < playerInventory.Tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < playerInventory.Tiles.tiles.GetLength(0); x++)
            {
                var tile = resolver.Instantiate(uiConfig.Tile, inventory.ContentForTiles);
                tile.Initialize(playerInventory, playerInventory.Tiles.GetTile(x, y));
                tile.GetComponentInChildren<TMP_Text>().text = $"{x}:{y}";
            }
            
            DrawItems(inventory);
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
            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }
    }
}