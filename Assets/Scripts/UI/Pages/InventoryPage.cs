using Inventory;
using TMPro;
using UI.Configs;
using UnityEngine;
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
            Debug.Log($"Tiles: {playerInventory.Tiles.tiles.Length}");
            for (var y = 0; y < playerInventory.Tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < playerInventory.Tiles.tiles.GetLength(0); x++)
            {
                var tile = resolver.Instantiate(uiConfig.Tile, inventory.ContentForTiles);
                tile.GetComponentInChildren<TMP_Text>().text = $"{x}:{y}";
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