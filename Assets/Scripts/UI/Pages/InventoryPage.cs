using System.Collections.Generic;
using Inventory;
using Inventory.Item;
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
        public override PageType Type { get; } = PageType.Inventory;
        public static InventoryPage Current { get; private set; }
        
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
        private readonly List<RectTransform> itemRects = new();
        private Vector2 handGrabOffset;
        
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
            Current = this;
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
            itemRects.Clear();
            ClearChildren(inventoryView.ContentForItems);
            DrawItems(inventoryView);
            DrawHandSlot();
        }
        
        public bool TryCaptureGrabOffset(Vector2 screenPoint)
        {
            var eventCamera = GetEventCamera();

            for (var i = itemRects.Count - 1; i >= 0; i--)
            {
                var itemRect = itemRects[i];
                if (!itemRect || !RectTransformUtility.RectangleContainsScreenPoint(itemRect, screenPoint, eventCamera))
                {
                    continue;
                }

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(itemRect, screenPoint, eventCamera, out var localPoint))
                {
                    continue;
                }

                handGrabOffset = localPoint;
                return true;
            }

            return false;
        }

        public void ResetGrabOffset()
        {
            handGrabOffset = Vector2.zero;
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
                handSlotRect = null;
            }

            var handItemConfig = playerInventory.HandSlot.Value?.ItemConfig;
            if (handItemConfig == null)
            {
                return;
            }

            var handItemObject = new GameObject($"Hand Item [{handItemConfig.Id}]", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handSlotRect = handItemObject.GetComponent<RectTransform>();
            handSlotRect.SetParent(canvasRect, false);
            handSlotRect.anchorMin = new Vector2(0.5f, 0.5f);
            handSlotRect.anchorMax = new Vector2(0.5f, 0.5f);
            handSlotRect.pivot = new Vector2(0.5f, 0.5f);
            handSlotRect.sizeDelta = handItemConfig.SizeInInventory;

            var handItemImage = handItemObject.GetComponent<Image>();
            handItemImage.sprite = handItemConfig.Icon;
            handItemImage.preserveAspect = true;
            handItemImage.raycastTarget = false;

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
            var dragParentRect = handSlotRect.parent as RectTransform;
            if (dragParentRect == null)
            {
                return;
            }

            var eventCamera = GetEventCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(dragParentRect, pointerPosition, eventCamera, out var localPoint))
            {
                return;
            }

            handSlotRect.anchoredPosition = localPoint - handGrabOffset;
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
                itemImageRect.sizeDelta = item.ItemConfig.SizeInInventory;
                itemRects.Add(itemImageRect);
                
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
        
        private Camera GetEventCamera() => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        
        public override void Hide()
        {
            redrawDisposables.Clear();
            itemRects.Clear();
            
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
