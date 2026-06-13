using System.Collections.Generic;
using System.Globalization;
using Colors;
using Inventory.Item;
using Inventory.Inventories;
using Inventory.Slot;
using Localization;
using TMPro;
using Stats;
using UI.Configs;
using UI.UIElements;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;
using Money;

namespace UI.Pages
{
    internal static class PageUiUtilities
    {
        private static readonly StatType[] UsablePopupStatTypes = { StatType.Hp, StatType.Water, StatType.Food, StatType.Chill };
        private static readonly StatType[] DefensePopupStatTypes = { StatType.PhysicalDefense, StatType.TemperatureDefense, StatType.PsiDefense, StatType.MagicDefense };

        public static Image CreateBloodScreen(
            UIConfig uiConfig,
            IObjectResolver resolver,
            RectTransform parent,
            PageType pageType)
        {
            if (uiConfig == null || resolver == null || parent == null || uiConfig.BloodScreen == null)
            {
                return null;
            }

            var bloodScreen = resolver.Instantiate(uiConfig.BloodScreen, parent);
            bloodScreen.name = $"{uiConfig.BloodScreen.name} | {pageType}";
            bloodScreen.transform.SetAsLastSibling();
            return bloodScreen;
        }

        public static void FillSlotsViewContainerStats(SlotsViewContainer slotsViewContainer, StatsController statsController)
        {
            if (slotsViewContainer == null || statsController == null)
            {
                return;
            }

            FillStatHolder(slotsViewContainer.PhysicalDefenseStat, statsController.GetStat(StatType.PhysicalDefense));
            FillStatHolder(slotsViewContainer.TemperatureDefenseStat, statsController.GetStat(StatType.TemperatureDefense));
            FillStatHolder(slotsViewContainer.PsiDefenseStat, statsController.GetStat(StatType.PsiDefense));
            FillStatHolder(slotsViewContainer.MagicDefenseStat, statsController.GetStat(StatType.MagicDefense));
        }

        public static void FillInfoAboutPlayer(InfoAboutPlayer infoAboutPlayer, Character.CharacterInfo currentCharacterInfo, MoneyStorage moneyStorage)
        {
            if (infoAboutPlayer == null || currentCharacterInfo == null)
            {
                return;
            }

            infoAboutPlayer.Photo.sprite = currentCharacterInfo.Photo;
            infoAboutPlayer.Name.text = currentCharacterInfo.Name.GetLocalizedStringCached();
            infoAboutPlayer.Group.text = currentCharacterInfo.Fraction.GetLocalizedStringCached();
            if (infoAboutPlayer.Money != null)
            {
                infoAboutPlayer.Money.text = moneyStorage == null
                                                 ? "---"
                                                 : $"{moneyStorage.CurrentMoney.Value} RU";
            }
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
        
        public static void FillInfoAboutInventory(InfoAboutInventory infoAboutInventory, LocalizationConfig localizationConfig, ColorsConfig colorsConfig, float currentWeight, float? maxWeight)
        {
            if (infoAboutInventory == null || infoAboutInventory.Weight == null || localizationConfig == null || colorsConfig == null)
            {
                return;
            }

            var currentWeightText = currentWeight.ToString("F1", CultureInfo.InvariantCulture);
            var currentWeightLabel = localizationConfig.InventoryCurrentWeight.GetLocalizedStringCached();
            var kgLabel = localizationConfig.kg.GetLocalizedStringCached();
            var maxLabel = localizationConfig.max.GetLocalizedStringCached();
            var maxText = maxWeight.HasValue && maxWeight.Value >= 0f
                ? $"{maxLabel} {maxWeight.Value.ToString("F1", CultureInfo.InvariantCulture)} {kgLabel}"
                : $"{maxLabel} ...";

            var grayColor = ColorUtility.ToHtmlStringRGB(colorsConfig.Gray);
            var whiteColor = ColorUtility.ToHtmlStringRGB(colorsConfig.White);
            
            infoAboutInventory.Weight.text =
                $"<color=#{grayColor}>{currentWeightLabel}</color> " +
                $"<color=#{whiteColor}>{currentWeightText}</color> " +
                $"<color=#{whiteColor}>{kgLabel}</color> " +
                $"<color=#{grayColor}>({maxText})</color>";
        }

        public static void FillSellInventoryInfoText(TMP_Text infoText, LocalizationConfig localizationConfig, ColorsConfig colorsConfig, int totalPrice, float totalWeight)
        {
            if (infoText == null || localizationConfig == null || colorsConfig == null)
            {
                return;
            }

            var currentWeightText = totalWeight.ToString("F1", CultureInfo.InvariantCulture);

            var kgLabel = localizationConfig.kg.GetLocalizedStringCached();
            var grayColor = ColorUtility.ToHtmlStringRGB(colorsConfig.Gray);
            var whiteColor = ColorUtility.ToHtmlStringRGB(colorsConfig.White);

            infoText.text = totalPrice is 0 ? "" :
                $"<color=#{whiteColor}>{totalPrice} RU</color> " +
                $"<color=#{grayColor}>(</color>" +
                $"<color=#{whiteColor}>{currentWeightText}</color> " +
                $"<color=#{whiteColor}>{kgLabel}</color>" +
                $"<color=#{grayColor}>)</color>";
        }

        public static float GetItemsWeight(IInventory inventory)
        {
            if (inventory == null)
            {
                return 0f;
            }

            var weight = 0f;
            foreach (var item in inventory.Items)
            {
                if (item?.ItemStack != null)
                {
                    weight += item.ItemStack.TotalWeight;
                }
            }

            return weight;
        }

        public static float GetSlotsWeight(params SlotModel[] slots)
        {
            var weight = 0f;
            foreach (var slot in slots)
            {
                if (slot?.ItemStack != null)
                {
                    weight += slot.ItemStack.TotalWeight;
                }
            }

            return weight;
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

        public static RectTransform CreateItemImage(Transform parent, ItemStack itemStack, string namePrefix, Vector2? stackAnchorAreaSize = null)
        {
            var itemConfig = itemStack.ItemConfig;
            var itemImageObject = new GameObject($"{namePrefix} [{itemConfig.Id}]", typeof(RectTransform));
            var itemImageRect = itemImageObject.GetComponent<RectTransform>();
            itemImageRect.SetParent(parent, false);
            itemImageRect.anchorMin = new Vector2(0, 1);
            itemImageRect.anchorMax = new Vector2(0, 1);
            itemImageRect.pivot = new Vector2(0.5f, 0.5f);
            itemImageRect.sizeDelta = itemStack.SizeInInventory;

            CreateItemIcon(itemImageRect, itemConfig.Icon, itemStack.SizeInInventory, itemStack.IsRotated);

            if (itemStack.Count > 1)
            {
                CreateStackCountLabel(itemImageRect, itemStack.Count, stackAnchorAreaSize ?? itemImageRect.sizeDelta);
            }

            return itemImageRect;
        }

        public static bool TryGetSlotUnderPointer
            (
                SlotView slotView,
                SlotModel slotModel,
                Vector2 screenPoint,
                ItemType? requiredType,
                Camera eventCamera,
                out SlotModel hoveredSlotModel
            )
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
            else if (slotModel == playerInventory.FaceSlot && slotsViewContainer.FaceSlot)
            {
                slotRect = slotsViewContainer.FaceSlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.BodySlot && slotsViewContainer.BodySlot)
            {
                slotRect = slotsViewContainer.BodySlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.HandsSlot && slotsViewContainer.HandsSlot)
            {
                slotRect = slotsViewContainer.HandsSlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.ArmsSlot && slotsViewContainer.ArmsSlot)
            {
                slotRect = slotsViewContainer.ArmsSlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.LegsSlot && slotsViewContainer.LegsSlot)
            {
                slotRect = slotsViewContainer.LegsSlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.HipsSlot && slotsViewContainer.HipsSlot)
            {
                slotRect = slotsViewContainer.HipsSlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.BackpackSlot && slotsViewContainer.BackpackSlot)
            {
                slotRect = slotsViewContainer.BackpackSlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.LeftWeaponSlot && slotsViewContainer.LeftWeaponSlot)
            {
                slotRect = slotsViewContainer.LeftWeaponSlot.GetComponent<RectTransform>();
            }
            else if (slotModel == playerInventory.RightWeaponSlot && slotsViewContainer.RightWeaponSlot)
            {
                slotRect = slotsViewContainer.RightWeaponSlot.GetComponent<RectTransform>();
            }

            return slotRect;
        }

        public static void SetSlotBlockedState(SlotView slotView, bool isBlocked)
        {
            if (!slotView)
            {
                return;
            }

            var image = slotView.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.type = isBlocked ? Image.Type.Simple : Image.Type.Tiled;
        }

        public static bool TryGetFastSlotUnderPointer(
            SlotView slotView,
            FastSlotModel fastSlotModel,
            Vector2 screenPoint,
            Camera eventCamera,
            out FastSlotModel hoveredFastSlotModel)
        {
            hoveredFastSlotModel = null;
            if (!slotView || fastSlotModel == null)
            {
                return false;
            }

            var slotRect = slotView.GetComponent<RectTransform>();
            if (!slotRect || !RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPoint, eventCamera))
            {
                return false;
            }

            hoveredFastSlotModel = fastSlotModel;
            return true;
        }

        public static bool TryGetFastSlotRect(
            SlotsViewContainer slotsViewContainer,
            PlayerInventory playerInventory,
            FastSlotModel fastSlotModel,
            out RectTransform slotRect)
        {
            slotRect = null;
            if (slotsViewContainer == null || playerInventory == null || fastSlotModel == null)
            {
                return false;
            }

            if (fastSlotModel == playerInventory.FastSlot1 && slotsViewContainer.FastSlot1)
            {
                slotRect = slotsViewContainer.FastSlot1.GetComponent<RectTransform>();
            }
            else if (fastSlotModel == playerInventory.FastSlot2 && slotsViewContainer.FastSlot2)
            {
                slotRect = slotsViewContainer.FastSlot2.GetComponent<RectTransform>();
            }
            else if (fastSlotModel == playerInventory.FastSlot3 && slotsViewContainer.FastSlot3)
            {
                slotRect = slotsViewContainer.FastSlot3.GetComponent<RectTransform>();
            }
            else if (fastSlotModel == playerInventory.FastSlot4 && slotsViewContainer.FastSlot4)
            {
                slotRect = slotsViewContainer.FastSlot4.GetComponent<RectTransform>();
            }

            return slotRect;
        }

        public static bool TryGetFastSlotRect(
            StatsHolder statsHolder,
            PlayerInventory playerInventory,
            FastSlotModel fastSlotModel,
            out RectTransform slotRect)
        {
            slotRect = null;
            if (statsHolder == null || playerInventory == null || fastSlotModel == null)
            {
                return false;
            }

            if (fastSlotModel == playerInventory.FastSlot1 && statsHolder.FastSlot1)
            {
                slotRect = statsHolder.FastSlot1.GetComponent<RectTransform>();
            }
            else if (fastSlotModel == playerInventory.FastSlot2 && statsHolder.FastSlot2)
            {
                slotRect = statsHolder.FastSlot2.GetComponent<RectTransform>();
            }
            else if (fastSlotModel == playerInventory.FastSlot3 && statsHolder.FastSlot3)
            {
                slotRect = statsHolder.FastSlot3.GetComponent<RectTransform>();
            }
            else if (fastSlotModel == playerInventory.FastSlot4 && statsHolder.FastSlot4)
            {
                slotRect = statsHolder.FastSlot4.GetComponent<RectTransform>();
            }

            return slotRect;
        }
        
        public static bool TryCaptureGrabOffset
            (
                IReadOnlyList<RectTransform> itemRects,
                IReadOnlyList<RectTransform> itemGrabRects,
                Vector2 screenPoint,
                Camera eventCamera,
                out Vector2 handGrabOffset
            )
        {
            handGrabOffset = Vector2.zero;
            for (var i = itemRects.Count - 1; i >= 0; i--)
            {
                if (TryCaptureOffsetOnRect(itemRects[i], screenPoint, eventCamera, out handGrabOffset))
                {
                    return true;
                }
            }

            for (var i = itemGrabRects.Count - 1; i >= 0; i--)
            {
                if (TryCaptureOffsetOnRect(itemGrabRects[i], screenPoint, eventCamera, out handGrabOffset))
                {
                    return true;
                }
            }

            return false;
        }

        public static RectTransform DrawHandSlot
            (
                RectTransform existingHandSlotRect,
                RectTransform canvasRect,
                ItemStack handItemStack,
                Vector2? stackAnchorAreaSize = null
            )
        {
            if (existingHandSlotRect)
            {
                Object.Destroy(existingHandSlotRect.gameObject);
            }

            if (handItemStack?.ItemConfig == null)
            {
                return null;
            }

            var handItemConfig = handItemStack.ItemConfig;
            var handItemObject = new GameObject($"Hand Item [{handItemConfig.Id}]", typeof(RectTransform));
            var handSlotRect = handItemObject.GetComponent<RectTransform>();
            handSlotRect.SetParent(canvasRect, false);
            handSlotRect.anchorMin = new Vector2(0.5f, 0.5f);
            handSlotRect.anchorMax = new Vector2(0.5f, 0.5f);
            handSlotRect.pivot = new Vector2(0.5f, 0.5f);
            handSlotRect.sizeDelta = handItemStack.SizeInInventory;

            CreateItemIcon(handSlotRect, handItemConfig.Icon, handItemStack.SizeInInventory, handItemStack.IsRotated);

            if (handItemStack.Count > 1)
            {
                CreateStackCountLabel(handSlotRect, handItemStack.Count, stackAnchorAreaSize ?? handSlotRect.sizeDelta);
            }

            return handSlotRect;
        }

        public static bool TryGetPointerPositionLocalToRect
            (
                RectTransform targetRect,
                Camera eventCamera,
                out Vector2 pointerPosition,
                out Vector2 localPoint
            )
        {
            pointerPosition = Vector2.zero;
            localPoint = Vector2.zero;
            var pointer = Pointer.current;
            if (pointer == null || targetRect == null)
            {
                return false;
            }

            pointerPosition = pointer.position.ReadValue();
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, pointerPosition, eventCamera, out localPoint);
        }

        public static void DrawSlotItem
            (
                SlotView slotView,
                SlotModel slotModel,
                ICollection<RectTransform> itemRects,
                ICollection<RectTransform> itemGrabRects
            )
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
            if (slotModel?.ItemStack == null)
            {
                return;
            }

            var itemImageRect = CreateItemImage(slotRect, slotModel.ItemStack, "Slot Item");
            itemImageRect.anchorMin = new Vector2(0.5f, 0.5f);
            itemImageRect.anchorMax = new Vector2(0.5f, 0.5f);
            itemImageRect.anchoredPosition = Vector2.zero;
            itemRects.Add(itemImageRect);
            itemGrabRects.Add(itemImageRect);
        }

        public static void DrawFastSlotItem(SlotView slotView, FastSlotModel fastSlotModel, bool isAvailable)
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
            if (fastSlotModel?.ItemConfig != null)
            {
                var itemImageRect = CreateItemImage(slotRect, new ItemStack(fastSlotModel.ItemConfig), "Fast Slot Item");
                itemImageRect.anchorMin = new Vector2(0.5f, 0.5f);
                itemImageRect.anchorMax = new Vector2(0.5f, 0.5f);
                itemImageRect.anchoredPosition = Vector2.zero;
                FitFastSlotItemToSlot(slotRect, itemImageRect);

                var canvasGroup = itemImageRect.gameObject.AddComponent<CanvasGroup>();
                canvasGroup.alpha = isAvailable ? 1f : 0.45f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            CreateFastSlotLabel(slotRect, fastSlotModel?.DisplayName);
        }

        public static void SetPopupRaycastState(RectTransform popupRect, bool blocksRaycasts)
        {
            if (popupRect == null)
            {
                return;
            }

            if (!popupRect.TryGetComponent(out CanvasGroup canvasGroup))
            {
                canvasGroup = popupRect.gameObject.AddComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.blocksRaycasts = blocksRaycasts;
            canvasGroup.interactable = blocksRaycasts;
        }

        public static void FillInventoryHoverPopup
            (
                RectTransform popupRect,
                UIConfig uiConfig,
                LocalizationConfig localizationConfig,
                StatIconsConfig statIconsConfig,
                StatsController statsController,
                PlayerInventory playerInventory,
                IObjectResolver resolver,
                ItemConfig itemConfig,
                ItemStack itemStack,
                bool isEquippedItemPopup,
                Color fillColor,
                Color positiveChangeColor,
                Color negativeChangeColor
            )
        {
            if (popupRect == null
             || uiConfig == null
             || localizationConfig == null
             || statIconsConfig == null
             || statsController == null
             || playerInventory == null
             || resolver == null
             || itemConfig == null)
            {
                return;
            }

            CreatePopupItemName(popupRect, uiConfig, resolver, itemConfig);
            CreatePopupWeight(popupRect, uiConfig, localizationConfig, resolver, itemStack);

            switch (itemConfig.ItemType)
            {
                case ItemType.Usable:
                    CreateUsablePopupStats(popupRect, uiConfig, statIconsConfig, resolver, itemConfig);
                    break;
                case ItemType.Helm:
                case ItemType.Face:
                case ItemType.Body:
                case ItemType.Hands:
                case ItemType.Arms:
                case ItemType.Legs:
                case ItemType.Hips:
                    CreateClothesPopupStats(
                        popupRect,
                        uiConfig,
                        statIconsConfig,
                        statsController,
                        playerInventory,
                        resolver,
                        itemConfig,
                        isEquippedItemPopup,
                        fillColor,
                        positiveChangeColor,
                        negativeChangeColor);
                    break;
                case ItemType.Backpack when itemConfig is BackpackItemConfig backpackItemConfig:
                    CreateBackpackSizePopup(popupRect, uiConfig, resolver, backpackItemConfig);
                    break;
            }
        }

        public static void CreatePopupButton
            (
                RectTransform popupRect,
                UIConfig uiConfig,
                IObjectResolver resolver,
                string label,
                UnityAction onClick,
                bool interactable = true
            )
        {
            if (popupRect == null || uiConfig?.PopupButton == null || resolver == null)
            {
                return;
            }

            var button = resolver.Instantiate(uiConfig.PopupButton, popupRect);
            button.name = $"{uiConfig.PopupButton.name} | {label}";
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            button.interactable = interactable;

            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }

        public static void RecalculatePopupSize(RectTransform popupRect)
        {
            if (popupRect == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(popupRect);
            var layoutGroup = popupRect.GetComponent<VerticalLayoutGroup>();
            var width = 0f;
            var height = 0f;
            var childCount = 0;

            for (var i = 0; i < popupRect.childCount; i++)
            {
                if (popupRect.GetChild(i) is not RectTransform child || !child.gameObject.activeSelf)
                {
                    continue;
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(child);
                var childWidth = Mathf.Max(LayoutUtility.GetPreferredWidth(child), child.rect.width);
                var childHeight = Mathf.Max(LayoutUtility.GetPreferredHeight(child), child.rect.height);
                width = Mathf.Max(width, childWidth);
                height += childHeight;
                childCount++;
            }

            if (layoutGroup != null)
            {
                width += layoutGroup.padding.left + layoutGroup.padding.right;
                height += layoutGroup.padding.top + layoutGroup.padding.bottom + layoutGroup.spacing * Mathf.Max(0, childCount - 1);
            }

            popupRect.sizeDelta = new Vector2(width, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(popupRect);
        }

        public static void UpdatePopupPosition(RectTransform popupRect, RectTransform popupParentRect, Camera eventCamera, Vector2 screenPoint)
        {
            if (popupRect == null || popupParentRect == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(popupParentRect, screenPoint, eventCamera, out var localPoint))
            {
                return;
            }

            var parentRect = popupParentRect.rect;
            var anchoredPosition = new Vector2(
                localPoint.x + parentRect.width * popupParentRect.pivot.x,
                localPoint.y - parentRect.height * (1f - popupParentRect.pivot.y));

            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, 0f, Mathf.Max(0f, parentRect.width - popupRect.rect.width));
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, -Mathf.Max(0f, parentRect.height - popupRect.rect.height), 0f);
            popupRect.anchoredPosition = anchoredPosition;
        }

        private static void CreateStackCountLabel(RectTransform parent, int count, Vector2 anchorAreaSize)
        {
            var labelObject = new GameObject("Stack Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(parent, false);
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(1f, 0f);
            labelRect.anchoredPosition = new Vector2(anchorAreaSize.x * 0.5f - 6f, -anchorAreaSize.y * 0.5f + 6f);
            labelRect.sizeDelta = new Vector2(72f, 30f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = count.ToString(CultureInfo.InvariantCulture);
            label.alignment = TextAlignmentOptions.BottomRight;
            label.fontSize = 24;
            label.color = Color.white;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        private static void CreateFastSlotLabel(RectTransform parent, string labelText)
        {
            if (parent == null || string.IsNullOrWhiteSpace(labelText))
            {
                return;
            }

            var labelObject = new GameObject("Fast Slot Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(parent, false);
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 0f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(6f, 6f);
            labelRect.sizeDelta = new Vector2(54f, 20f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.fontSize = 18;
            label.color = Color.white;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        private static void FitFastSlotItemToSlot(RectTransform slotRect, RectTransform itemImageRect)
        {
            if (slotRect == null || itemImageRect == null)
            {
                return;
            }

            var maxWidth = Mathf.Max(0f, slotRect.rect.width - 10f);
            var maxHeight = Mathf.Max(0f, slotRect.rect.height - 10f);
            var currentSize = itemImageRect.sizeDelta;
            if (maxWidth <= 0f || maxHeight <= 0f || currentSize.x <= 0f || currentSize.y <= 0f)
            {
                return;
            }

            var scale = Mathf.Min(1f, maxWidth / currentSize.x, maxHeight / currentSize.y);
            if (scale >= 0.999f)
            {
                return;
            }

            var fittedSize = currentSize * scale;
            itemImageRect.sizeDelta = fittedSize;

            for (var i = 0; i < itemImageRect.childCount; i++)
            {
                if (itemImageRect.GetChild(i) is RectTransform childRect)
                {
                    childRect.sizeDelta = fittedSize;
                }
            }
        }

        private static void CreateItemIcon(RectTransform parent, Sprite icon, Vector2 size, bool isRotated)
        {
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(parent, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = size;
            iconRect.localEulerAngles = isRotated ? new Vector3(0f, 0f, -90f) : Vector3.zero;

            var image = iconObject.GetComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static bool TryCaptureOffsetOnRect(RectTransform rect, Vector2 screenPoint, Camera eventCamera, out Vector2 handGrabOffset)
        {
            handGrabOffset = Vector2.zero;
            return rect
                   && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, eventCamera)
                   && RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out handGrabOffset);
        }

        private static void FillStatHolder(StatHolder statHolder, Stat stat)
        {
            if (statHolder == null || stat == null)
            {
                return;
            }

            var normalizedValue = Mathf.Approximately(stat.Max, 0f)
                ? 0f
                : stat.Value.Value / stat.Max;

            if (statHolder.Fill != null)
            {
                statHolder.Fill.fillAmount = normalizedValue;
            }

            if (statHolder.ChangedFill != null)
            {
                statHolder.ChangedFill.fillAmount = normalizedValue;
            }
        }

        private static void CreatePopupItemName(RectTransform popupRect, UIConfig uiConfig, IObjectResolver resolver, ItemConfig itemConfig)
        {
            if (popupRect == null || uiConfig?.PopupItemName == null || resolver == null || itemConfig == null)
            {
                return;
            }

            var itemName = resolver.Instantiate(uiConfig.PopupItemName, popupRect);
            itemName.name = $"{uiConfig.PopupItemName.name} | {itemConfig.name}";
            itemName.text = itemConfig.Name.GetLocalizedStringCached();
            itemName.transform.SetAsFirstSibling();
        }

        private static void CreatePopupWeight
            (
                RectTransform popupRect,
                UIConfig uiConfig,
                LocalizationConfig localizationConfig,
                IObjectResolver resolver,
                ItemStack itemStack
            )
        {
            if (popupRect == null || uiConfig?.PopupWeight == null || localizationConfig == null || resolver == null || itemStack?.ItemConfig == null)
            {
                return;
            }

            var popupWeight = resolver.Instantiate(uiConfig.PopupWeight, popupRect);
            popupWeight.name = $"{uiConfig.PopupWeight.name} | Weight";
            popupWeight.text = $"{itemStack.TotalWeight.ToString("F1", CultureInfo.InvariantCulture)} {localizationConfig.kg.GetLocalizedStringCached()}";
        }

        private static void CreateBackpackSizePopup
            (
                RectTransform popupRect,
                UIConfig uiConfig,
                IObjectResolver resolver,
                BackpackItemConfig backpackItemConfig
            )
        {
            if (popupRect == null || uiConfig?.PopupWeight == null || resolver == null || backpackItemConfig == null)
            {
                return;
            }

            var backpackSize = resolver.Instantiate(uiConfig.PopupWeight, popupRect);
            backpackSize.name = $"{uiConfig.PopupWeight.name} | Backpack Size";
            backpackSize.text = $"{backpackItemConfig.BackpackSize.x}x{backpackItemConfig.BackpackSize.y}";
        }

        private static void CreateUsablePopupStats
            (
                RectTransform popupRect,
                UIConfig uiConfig,
                StatIconsConfig statIconsConfig,
                IObjectResolver resolver,
                ItemConfig itemConfig
            )
        {
            if (popupRect == null || uiConfig?.StatHolderForUsable == null || statIconsConfig == null || resolver == null || itemConfig == null)
            {
                return;
            }

            foreach (var statType in UsablePopupStatTypes)
            {
                var value = GetItemStatValue(itemConfig, statType);
                if (Mathf.Approximately(value, 0f))
                {
                    continue;
                }

                var statHolder = resolver.Instantiate(uiConfig.StatHolderForUsable, popupRect);
                statHolder.name = $"{uiConfig.StatHolderForUsable.name} | {statType}";

                if (statHolder.Icon != null)
                {
                    statHolder.Icon.sprite = GetStatIcon(statIconsConfig, statType);
                }

                if (statHolder.Name != null)
                {
                    statHolder.Name.text = GetStatDisplayName(statType);
                }

                if (statHolder.Amount != null)
                {
                    statHolder.Amount.text = FormatSignedValue(value);
                }
            }
        }

        private static void CreateClothesPopupStats
            (
                RectTransform popupRect,
                UIConfig uiConfig,
                StatIconsConfig statIconsConfig,
                StatsController statsController,
                PlayerInventory playerInventory,
                IObjectResolver resolver,
                ItemConfig itemConfig,
                bool isEquippedItemPopup,
                Color fillColor,
                Color positiveChangeColor,
                Color negativeChangeColor
            )
        {
            if (popupRect == null
             || uiConfig?.StatHolderForClothes == null
             || statIconsConfig == null
             || statsController == null
             || playerInventory == null
             || resolver == null
             || itemConfig == null)
            {
                return;
            }

            var equippedItemConfig = GetEquippedItemConfig(playerInventory, itemConfig.ItemType);

            foreach (var statType in DefensePopupStatTypes)
            {
                var stat = statsController.GetStat(statType);
                var currentValue = stat.Value.Value;
                var currentEquippedValue = GetItemStatValue(equippedItemConfig, statType);
                var hoveredItemValue = GetItemStatValue(itemConfig, statType);

                float baseValue;
                float finalValue;

                if (isEquippedItemPopup)
                {
                    baseValue = currentValue - hoveredItemValue;
                    finalValue = currentValue;
                }
                else if (equippedItemConfig == null)
                {
                    baseValue = currentValue;
                    finalValue = currentValue + hoveredItemValue;
                }
                else
                {
                    baseValue = currentValue;
                    finalValue = currentValue - currentEquippedValue + hoveredItemValue;
                }

                CreateClothesPopupStatHolder(
                    popupRect,
                    uiConfig,
                    statIconsConfig,
                    resolver,
                    statType,
                    stat,
                    baseValue,
                    finalValue,
                    fillColor,
                    positiveChangeColor,
                    negativeChangeColor);
            }
        }

        private static void CreateClothesPopupStatHolder
            (
                RectTransform popupRect,
                UIConfig uiConfig,
                StatIconsConfig statIconsConfig,
                IObjectResolver resolver,
                StatType statType,
                Stat stat,
                float baseValue,
                float finalValue,
                Color fillColor,
                Color positiveChangeColor,
                Color negativeChangeColor
            )
        {
            var statHolder = resolver.Instantiate(uiConfig.StatHolderForClothes, popupRect);
            statHolder.name = $"{uiConfig.StatHolderForClothes.name} | {statType}";

            if (statHolder.Icon != null)
            {
                statHolder.Icon.sprite = GetStatIcon(statIconsConfig, statType);
                statHolder.Icon.color = fillColor;
            }

            if (statHolder.Fill == null || statHolder.ChangedFill == null)
            {
                return;
            }

            var normalizedBase = GetNormalizedPopupStatValue(stat, baseValue);
            var normalizedFinal = GetNormalizedPopupStatValue(stat, finalValue);

            if (normalizedFinal > normalizedBase)
            {
                statHolder.Fill.fillAmount = normalizedBase;
                statHolder.ChangedFill.fillAmount = normalizedFinal;
                statHolder.ChangedFill.color = positiveChangeColor;
            }
            else if (normalizedFinal < normalizedBase)
            {
                statHolder.Fill.fillAmount = normalizedFinal;
                statHolder.ChangedFill.fillAmount = normalizedBase;
                statHolder.ChangedFill.color = negativeChangeColor;
            }
            else
            {
                statHolder.Fill.fillAmount = normalizedFinal;
                statHolder.ChangedFill.fillAmount = normalizedFinal;
                statHolder.ChangedFill.color = fillColor;
            }

            statHolder.Fill.color = fillColor;
        }

        private static ItemConfig GetEquippedItemConfig(PlayerInventory playerInventory, ItemType itemType)
        {
            return itemType switch
            {
                ItemType.Helm => playerInventory.HelmSlot.ItemConfig,
                ItemType.Face => playerInventory.FaceSlot.ItemConfig,
                ItemType.Body => playerInventory.BodySlot.ItemConfig,
                ItemType.Hands => playerInventory.HandsSlot.ItemConfig,
                ItemType.Arms => playerInventory.ArmsSlot.ItemConfig,
                ItemType.Legs => playerInventory.LegsSlot.ItemConfig,
                ItemType.Hips => playerInventory.HipsSlot.ItemConfig,
                _ => null
            };
        }

        private static float GetNormalizedPopupStatValue(Stat stat, float value)
        {
            if (stat == null || Mathf.Approximately(stat.Max, 0f))
            {
                return 0f;
            }

            var clampedValue = Mathf.Clamp(value, stat.Min, stat.Max);
            return clampedValue / stat.Max;
        }

        private static float GetItemStatValue(ItemConfig itemConfig, StatType statType)
        {
            if (itemConfig == null)
            {
                return 0f;
            }

            return statType switch
            {
                StatType.Hp => itemConfig.HpStat,
                StatType.Water => itemConfig.WaterStat,
                StatType.Food => itemConfig.FoodStat,
                StatType.Chill => itemConfig.ChillStat,
                StatType.PhysicalDefense => itemConfig.PhysicalDefense,
                StatType.TemperatureDefense => itemConfig.TemperatureDefense,
                StatType.PsiDefense => itemConfig.PsiDefense,
                StatType.MagicDefense => itemConfig.MagicDefense,
                _ => 0f
            };
        }

        private static Sprite GetStatIcon(StatIconsConfig statIconsConfig, StatType statType)
        {
            return statType switch
            {
                StatType.Hp => statIconsConfig.HpStat,
                StatType.Water => statIconsConfig.WaterStat,
                StatType.Food => statIconsConfig.FoodStat,
                StatType.Chill => statIconsConfig.ChillStat,
                StatType.Stamina => statIconsConfig.StaminaStat,
                StatType.PhysicalDefense => statIconsConfig.PhysicalDefenseStat,
                StatType.TemperatureDefense => statIconsConfig.TemperatureDefenseStat,
                StatType.PsiDefense => statIconsConfig.PsiDefenseStat,
                StatType.MagicDefense => statIconsConfig.MagicDefenseStat,
                _ => null
            };
        }

        private static string GetStatDisplayName(StatType statType)
        {
            return statType switch
            {
                StatType.Hp => "HP",
                StatType.Water => "Water",
                StatType.Food => "Food",
                StatType.Chill => "Chill",
                StatType.Stamina => "Stamina",
                StatType.PhysicalDefense => "Physical Defense",
                StatType.TemperatureDefense => "Temperature Defense",
                StatType.PsiDefense => "Psi Defense",
                StatType.MagicDefense => "Magic Defense",
                _ => statType.ToString()
            };
        }

        private static string FormatSignedValue(float value)
        {
            return value > 0f
                ? $"+{value.ToString("0.##", CultureInfo.InvariantCulture)}"
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
