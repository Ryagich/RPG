using System.Collections.Generic;
using Inventory.Item;
using Inventory.Inventories;
using Inventory.Slot;
using Localization;
using UI.UIElements;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Pages
{
    internal static class PageUiUtilities
    {
        public static void FillInfoAboutPlayer(InfoAboutPlayer infoAboutPlayer, Character.CharacterInfo currentCharacterInfo)
        {
            if (infoAboutPlayer == null || currentCharacterInfo == null)
            {
                return;
            }

            infoAboutPlayer.Photo.sprite = currentCharacterInfo.Photo;
            infoAboutPlayer.Name.text = currentCharacterInfo.Name.GetLocalizedStringCached();
            infoAboutPlayer.Group.text = currentCharacterInfo.Fraction.GetLocalizedStringCached();
        }

        public static void ClearChildren(Transform parent)
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

        public static Vector2 GetItemGrabSize(GridLayoutGroup gridLayoutGroup, Vector2Int itemSize)
        {
            return new Vector2(
                itemSize.x * gridLayoutGroup.cellSize.x + (itemSize.x - 1) * gridLayoutGroup.spacing.x,
                itemSize.y * gridLayoutGroup.cellSize.y + (itemSize.y - 1) * gridLayoutGroup.spacing.y);
        }

        public static Vector2 GetItemAnchoredPosition(GridLayoutGroup gridLayoutGroup, Vector3 itemCenterPosition)
        {
            return new Vector2(
                gridLayoutGroup.padding.left
                + (itemCenterPosition.x + 0.5f) * gridLayoutGroup.cellSize.x
                + itemCenterPosition.x * gridLayoutGroup.spacing.x,
                -(gridLayoutGroup.padding.top
                  + (itemCenterPosition.y + 0.5f) * gridLayoutGroup.cellSize.y
                  + itemCenterPosition.y * gridLayoutGroup.spacing.y));
        }

        public static RectTransform CreateItemImage(Transform parent, ItemConfig itemConfig, string namePrefix)
        {
            var itemImageObject = new GameObject($"{namePrefix} [{itemConfig.Id}]", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var itemImageRect = itemImageObject.GetComponent<RectTransform>();
            itemImageRect.SetParent(parent, false);
            itemImageRect.anchorMin = new Vector2(0, 1);
            itemImageRect.anchorMax = new Vector2(0, 1);
            itemImageRect.pivot = new Vector2(0.5f, 0.5f);
            itemImageRect.sizeDelta = itemConfig.SizeInInventory;

            var itemImage = itemImageObject.GetComponent<Image>();
            itemImage.sprite = itemConfig.Icon;
            itemImage.preserveAspect = true;
            itemImage.raycastTarget = false;

            return itemImageRect;
        }

        public static bool TryGetSlotUnderPointer(
            SlotView slotView,
            SlotModel slotModel,
            Vector2 screenPoint,
            ItemType? requiredType,
            Camera eventCamera,
            out SlotModel hoveredSlotModel)
        {
            hoveredSlotModel = null;
            if (!slotView)
            {
                return false;
            }

            var slotRect = slotView.GetComponent<RectTransform>();
            if (!slotRect)
            {
                return false;
            }

            if (requiredType.HasValue && slotModel.ItemType != requiredType.Value)
            {
                return false;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPoint, eventCamera))
            {
                return false;
            }

            hoveredSlotModel = slotModel;
            return true;
        }

        public static bool TryGetSlotRect
            (
                SlotsViewContainer slotsViewContainer,
                PlayerInventory playerInventory,
                SlotModel slotModel,
                out RectTransform slotRect
            )
        {
            slotRect = null;
            if (slotModel == playerInventory.HelmSlot && slotsViewContainer.HeadSlot)
            {
                slotRect = slotsViewContainer.HeadSlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.BodySlot && slotsViewContainer.BodySlot)
            {
                slotRect = slotsViewContainer.BodySlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.BackpackSlot && slotsViewContainer.BackpackSlot)
            {
                slotRect = slotsViewContainer.BackpackSlot.GetComponent<RectTransform>();
            }

            return slotRect;
        }
    }
}