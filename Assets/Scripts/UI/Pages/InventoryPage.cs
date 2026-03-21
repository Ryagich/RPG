using System.Collections.Generic;
using Inventory;
using TMPro;
using UI.Configs;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using UnityEngine.InputSystem;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class InventoryPage : BasePage, ITickable
    {
        private static readonly Vector2 HandSlotSize = new(96f, 96f);
        private static readonly Vector2 HandSlotPadding = new(8f, 8f);
        private static readonly Vector2 CursorOffset = new(24f, -24f);

        public override PageType Type { get; } = PageType.Inventory;

        private readonly UIConfig uiConfig;
        private readonly PlayerInventory playerInventory;
        private readonly Canvas canvas;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;

        private RectTransform contentRect = null!;
        private RectTransform rightRect = null!;
        private Inventory.InventoryView inventoryView = null!;
        private RectTransform handSlotRect = null!;
        private readonly CompositeDisposable redrawDisposables = new();
        private ScrollRect inventoryScrollRect = null!;
        
        public InventoryPage
            (
                UIConfig uiConfig,
                Canvas canvas,
                PlayerInventory playerInventory,
                IObjectResolver resolver
            )
        {
            this.uiConfig = uiConfig;
            this.canvas = canvas;
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
            inventoryView = resolver.Instantiate(uiConfig.InventoryView, rightRect);
            inventoryScrollRect = inventoryView.GetComponent<ScrollRect>();
            
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

        public void Tick()
        {
            if (!handSlotRect)
            {
                return;
            }

            UpdateHandSlotPosition();
        }

        public void ReDraw()
        {
            if (!inventoryView)
            {
                return;
            }

            UpdateInventoryScrollState();
            ClearChildren(inventoryView.ContentForItems);
            DrawItems(inventoryView);
            DrawHandSlot();
        }
        
        private void UpdateInventoryScrollState()
        {
            if (!inventoryScrollRect)
            {
                return;
            }

            inventoryScrollRect.enabled = playerInventory.HandSlot.Value?.ItemConfig == null;
        }

        private void DrawHandSlot()
        {
            if (handSlotRect)
            {
                Object.Destroy(handSlotRect.gameObject);
            }

            var handSlotObject = new GameObject("Hand Slot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handSlotRect = handSlotObject.GetComponent<RectTransform>();
            handSlotRect.SetParent(canvasRect, false);
            handSlotRect.anchorMin = new Vector2(0.5f, 0.5f);
            handSlotRect.anchorMax = new Vector2(0.5f, 0.5f);
            handSlotRect.pivot = new Vector2(0.5f, 0.5f);
            handSlotRect.sizeDelta = HandSlotSize;

            var backgroundImage = handSlotObject.GetComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.4f);
            backgroundImage.raycastTarget = false;

            var handItemConfig = playerInventory.HandSlot.Value?.ItemConfig;
            if (handItemConfig != null)
            {
                var handItemObject = new GameObject($"Hand Item [{handItemConfig.Id}]", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var handItemRect = handItemObject.GetComponent<RectTransform>();
                handItemRect.SetParent(handSlotRect, false);
                handItemRect.anchorMin = Vector2.zero;
                handItemRect.anchorMax = Vector2.one;
                handItemRect.offsetMin = HandSlotPadding;
                handItemRect.offsetMax = -HandSlotPadding;

                var handItemImage = handItemObject.GetComponent<Image>();
                handItemImage.sprite = handItemConfig.Icon;
                handItemImage.preserveAspect = true;
                handItemImage.raycastTarget = false;
            }

            UpdateHandSlotPosition();
        }

        private void UpdateHandSlotPosition()
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            var pointerPosition = pointer.position.ReadValue();
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointerPosition, eventCamera, out var localPoint))
            {
                return;
            }

            handSlotRect.anchoredPosition = localPoint + CursorOffset;
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
                    + (itemCenterPosition.y + 0.5f) * gridLayoutGroup.cellSize.y
                    + itemCenterPosition.y * gridLayoutGroup.spacing.y));

                var itemImage = itemImageObject.GetComponent<Image>();
                itemImage.sprite = item.ItemConfig.Icon;
                itemImage.preserveAspect = true;
                itemImage.raycastTarget = false;
            }
        }

        public override void Hide()
        {
            redrawDisposables.Clear();

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }

            if (handSlotRect)
            {
                Object.Destroy(handSlotRect.gameObject);
            }

            contentRect = null;
            rightRect = null;
            inventoryView = null;
            inventoryScrollRect = null;
            handSlotRect = null;
        }
    }
}
