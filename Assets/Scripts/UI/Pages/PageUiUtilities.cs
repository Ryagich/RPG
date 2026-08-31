using System.Collections.Generic;
using System.Globalization;
using Colors;
using Combat;
using Factions;
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
using UnityEngine.Localization;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;
using Money;

namespace UI.Pages
{
    internal static class PageUiUtilities
    {
        // Chill отвечает за сон. До появления дня/ночи и сна не показываем его в usable item UI,
        // но сам StatType и serialized данные остаются в проекте.
        private static readonly StatType[] UsablePopupStatTypes = { StatType.Hp, StatType.Water, StatType.Food, StatType.Stamina };
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

        public static void FillInfoAboutPlayer(
            InfoAboutPlayer infoAboutPlayer,
            Character.CharacterInfo currentCharacterInfo,
            MoneyStorage moneyStorage,
            FactionConfig faction)
        {
            if (infoAboutPlayer == null)
            {
                return;
            }

            infoAboutPlayer.Group.text = faction == null
                ? "---"
                : faction.Name.GetLocalizedStringCached();

            if (currentCharacterInfo == null)
            {
                return;
            }

            infoAboutPlayer.Photo.sprite = currentCharacterInfo.Photo;
            infoAboutPlayer.Name.text = currentCharacterInfo.Name.GetLocalizedStringCached();
            if (infoAboutPlayer.Money != null)
            {
                infoAboutPlayer.Money.text = moneyStorage == null || moneyStorage.HasUnlimitedFunds
                                                 ? "---"
                                                 : moneyStorage.CurrentMoney.Value.ToString(CultureInfo.InvariantCulture);
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
            var maxLabel = localizationConfig.max.GetLocalizedStringCached();
            var maxText = maxWeight.HasValue && maxWeight.Value >= 0f
                ? $"{maxLabel} {maxWeight.Value.ToString("F1", CultureInfo.InvariantCulture)}"
                : $"{maxLabel} ...";

            var grayColor = ColorUtility.ToHtmlStringRGB(colorsConfig.Gray);
            var whiteColor = ColorUtility.ToHtmlStringRGB(colorsConfig.White);
            
            infoAboutInventory.Weight.text =
                $"<color=#{grayColor}>{currentWeightLabel}</color> " +
                $"<color=#{whiteColor}>{currentWeightText}</color> " +
                $"<color=#{grayColor}>({maxText})</color>";
        }

        public static void FillSellInventoryInfoText(TMP_Text infoText, LocalizationConfig localizationConfig, ColorsConfig colorsConfig, int totalPrice, float totalWeight)
        {
            if (infoText == null || localizationConfig == null || colorsConfig == null)
            {
                return;
            }

            var currentWeightText = totalWeight.ToString("F1", CultureInfo.InvariantCulture);

            var grayColor = ColorUtility.ToHtmlStringRGB(colorsConfig.Gray);
            var whiteColor = ColorUtility.ToHtmlStringRGB(colorsConfig.White);

            infoText.text = totalPrice is 0 ? "" :
                $"<color=#{whiteColor}>{totalPrice}</color> " +
                $"<color=#{grayColor}>(</color>" +
                $"<color=#{whiteColor}>{currentWeightText}</color> " +
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

        public static RectTransform CreateSectionsLayout(RectTransform parent, string pageName)
        {
            if (parent == null)
            {
                return null;
            }

            // Keep page sections under a dedicated stretch container so layout remains stable
            // when the screen size changes, regardless of whether the page uses 1, 2, or 3 sections.
            var layoutObject = new GameObject(
                $"Sections Layout | {pageName}",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            var layoutRect = layoutObject.GetComponent<RectTransform>();
            layoutRect.SetParent(parent, false);
            layoutRect.anchorMin = Vector2.zero;
            layoutRect.anchorMax = Vector2.one;
            layoutRect.offsetMin = Vector2.zero;
            layoutRect.offsetMax = Vector2.zero;
            layoutRect.pivot = new Vector2(0.5f, 0.5f);

            var layoutGroup = layoutObject.GetComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 0f;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = true;
            layoutGroup.childScaleWidth = false;
            layoutGroup.childScaleHeight = false;

            return layoutRect;
        }

        public static RectTransform CreateSectionPlaceholder(RectTransform layoutRect, string sectionName)
        {
            if (layoutRect == null)
            {
                return null;
            }

            // Create an empty layout child only when a section is absent, so the remaining
            // real sections still keep their intended third of the screen.
            var placeholderObject = new GameObject(
                $"{sectionName} Placeholder",
                typeof(RectTransform),
                typeof(LayoutElement));
            var placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.SetParent(layoutRect, false);
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            placeholderRect.pivot = new Vector2(0.5f, 0.5f);

            RegisterSectionInLayout(placeholderRect);
            return placeholderRect;
        }

        public static void RegisterSectionInLayout(RectTransform sectionRect)
        {
            if (sectionRect == null)
            {
                return;
            }

            var layoutElement = sectionRect.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = sectionRect.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minWidth = 0f;
            layoutElement.preferredWidth = -1f;
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = 0f;
            layoutElement.preferredHeight = -1f;
            layoutElement.flexibleHeight = 1f;
        }

        public static RectTransform CreatePopupContent(RectTransform popupRect, UIConfig uiConfig, IObjectResolver resolver, bool blocksRaycasts)
        {
            if (popupRect == null || uiConfig?.PopupContent == null || resolver == null)
            {
                return null;
            }

            if (IsFantasyWarriorInventoryHoverPopup(popupRect))
            {
                SetPopupRaycastState(popupRect, blocksRaycasts);
                return FindRect(popupRect, "Content") ?? popupRect;
            }

            var popupContent = resolver.Instantiate(uiConfig.PopupContent, popupRect);
            popupContent.name = uiConfig.PopupContent.name;

            var popupContentRect = popupContent.transform as RectTransform;
            SetPopupRaycastState(popupContentRect, blocksRaycasts);
            return popupContentRect;
        }

        public static RectTransform CreatePopupRoot(
            RectTransform popupParentRect,
            UIConfig uiConfig,
            IObjectResolver resolver,
            bool useInventoryHoverPopup,
            string nameSuffix)
        {
            if (popupParentRect == null || uiConfig == null || resolver == null)
            {
                return null;
            }

            var prefab = useInventoryHoverPopup && uiConfig.InventoryHoverPopupRect != null
                ? uiConfig.InventoryHoverPopupRect
                : uiConfig.PopupRect;

            if (prefab == null)
            {
                return null;
            }

            var popupRoot = resolver.Instantiate(prefab, popupParentRect);
            popupRoot.name = $"{prefab.name} | {nameSuffix}";

            if (useInventoryHoverPopup && prefab == uiConfig.InventoryHoverPopupRect)
            {
                PrepareFantasyWarriorInventoryHoverPopup(popupRoot);
            }

            return popupRoot;
        }

        public static void FillInventoryHoverPopup
            (
                RectTransform popupRect,
                RectTransform popupContentRect,
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
             || popupContentRect == null
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

            if (TryFillFantasyWarriorInventoryHoverPopup(
                    popupRect,
                    uiConfig,
                    localizationConfig,
                    statIconsConfig,
                    statsController,
                    playerInventory,
                    itemConfig,
                    itemStack,
                    isEquippedItemPopup,
                    fillColor,
                    positiveChangeColor,
                    negativeChangeColor))
            {
                return;
            }

            CreatePopupItemName(popupRect, uiConfig, resolver, itemConfig);
            CreatePopupWeight(popupContentRect, uiConfig, localizationConfig, resolver, itemStack);

            switch (itemConfig.ItemType)
            {
                case ItemType.Usable:
                    CreateUsablePopupStats(popupContentRect, uiConfig, statIconsConfig, resolver, itemConfig);
                    break;
                case ItemType.Helm:
                case ItemType.Face:
                case ItemType.Body:
                case ItemType.Hands:
                case ItemType.Arms:
                case ItemType.Legs:
                case ItemType.Hips:
                    CreateClothesPopupStats(
                        popupContentRect,
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
                case ItemType.Backpack:
                    CreateBackpackSizePopup(popupContentRect, uiConfig, resolver, itemConfig);
                    break;
            }
        }

        public static bool FillMapQuestPopup(
            RectTransform popupRect,
            string questName,
            string currentStepName,
            string currentStepDescription,
            Sprite icon,
            Color iconColor)
        {
            if (popupRect == null)
            {
                return false;
            }

            TMP_Text questNameText = FindText(popupRect, "Content/Item/Name/Label_ItemName");
            if (questNameText != null)
            {
                questNameText.text = questName ?? string.Empty;
            }

            RectTransform statsGroupRect = FindRect(popupRect, "Content/Stats_Group");
            RectTransform stageRect = FindRect(popupRect, "Content/HUD_Stat_Base_Large");
            TMP_Text stageText = FindText(stageRect, "Label_Stat_Text");
            if (stageText != null)
            {
                stageText.text = currentStepName ?? string.Empty;
                MoveMapQuestStatsBelowStage(statsGroupRect, ResizeMapQuestStageToText(stageRect, stageText));
            }

            var popupIcon = FindImage(popupRect, "Content/HUD_Stat_Base_Large/Quest ICON");
            if (popupIcon != null)
            {
                popupIcon.sprite = icon;
                popupIcon.preserveAspect = true;
                popupIcon.enabled = icon != null;
                popupIcon.color = iconColor;
                popupIcon.raycastTarget = false;
            }

            RectTransform backgroundRect = FindRect(statsGroupRect, "Background");
            TMP_Text descriptionText = FindText(backgroundRect, "Label_ItemDescription");
            if (descriptionText != null)
            {
                descriptionText.text = currentStepDescription ?? string.Empty;
                ResizePopupSectionToText(statsGroupRect, descriptionText, backgroundRect);
            }

            ResizeMapQuestPopupToContent(popupRect);

            return true;
        }

        private static float ResizeMapQuestStageToText(RectTransform stageRect, TMP_Text stageText)
        {
            if (stageRect == null || stageText == null)
            {
                return 0f;
            }

            Canvas.ForceUpdateCanvases();

            RectTransform textRect = stageText.rectTransform;
            float availableWidth = textRect.rect.width;
            if (availableWidth <= 0f)
            {
                return 0f;
            }

            float stageBottomBeforeResize = GetBottomInParent(stageRect);
            float currentTextHeight = textRect.rect.height;
            float sectionPadding = Mathf.Max(0f, stageRect.rect.height - currentTextHeight);
            float preferredTextHeight = Mathf.Ceil(stageText.GetPreferredValues(stageText.text, availableWidth, 0f).y);

            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredTextHeight);
            stageRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(stageRect.rect.height, preferredTextHeight + sectionPadding));

            LayoutRebuilder.ForceRebuildLayoutImmediate(stageRect);
            return GetBottomInParent(stageRect) - stageBottomBeforeResize;
        }

        private static void MoveMapQuestStatsBelowStage(RectTransform statsGroupRect, float stageBottomOffset)
        {
            if (statsGroupRect == null || Mathf.Approximately(stageBottomOffset, 0f))
            {
                return;
            }

            statsGroupRect.anchoredPosition += Vector2.up * stageBottomOffset;
        }

        private static float GetBottomInParent(RectTransform rect)
        {
            if (rect == null || rect.parent == null)
            {
                return 0f;
            }

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float bottom = float.PositiveInfinity;
            for (var i = 0; i < corners.Length; i++)
            {
                bottom = Mathf.Min(bottom, rect.parent.InverseTransformPoint(corners[i]).y);
            }

            return float.IsPositiveInfinity(bottom) ? 0f : bottom;
        }

        private static void ResizeMapQuestPopupToContent(RectTransform popupRect)
        {
            if (popupRect == null)
            {
                return;
            }

            RectTransform contentRect = FindRect(popupRect, "Content");
            if (contentRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            float contentBottom = 0f;
            var corners = new Vector3[4];
            for (var i = 0; i < contentRect.childCount; i++)
            {
                if (contentRect.GetChild(i) is not RectTransform child || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                child.GetWorldCorners(corners);
                for (var cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    contentBottom = Mathf.Min(contentBottom, popupRect.InverseTransformPoint(corners[cornerIndex]).y);
                }
            }

            popupRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(popupRect.rect.height, -contentBottom));
            LayoutRebuilder.ForceRebuildLayoutImmediate(popupRect);
        }

        private static void ResizePopupSectionToText(RectTransform sectionRect, TMP_Text text, RectTransform backgroundRect)
        {
            if (sectionRect == null || text == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            float availableWidth = text.rectTransform.rect.width;
            if (availableWidth <= 0f)
            {
                return;
            }

            float preferredTextHeight = Mathf.Ceil(text.GetPreferredValues(text.text, availableWidth, 0f).y);
            float currentTextHeight = text.rectTransform.rect.height;
            float sectionPadding = Mathf.Max(0f, sectionRect.rect.height - currentTextHeight);
            float targetHeight = Mathf.Max(sectionRect.rect.height, preferredTextHeight + sectionPadding);

            sectionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
            if (backgroundRect != null)
            {
                backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionRect);
        }

        public static void CreatePopupButton
            (
                RectTransform popupContentRect,
                UIConfig uiConfig,
                IObjectResolver resolver,
                LocalizedString label,
                UnityAction onClick,
                bool interactable = true
            )
        {
            if (popupContentRect == null || uiConfig?.PopupButton == null || resolver == null)
            {
                return;
            }

            var button = resolver.Instantiate(uiConfig.PopupButton, popupContentRect);
            string localizedLabel = label.GetLocalizedStringCached();
            button.name = $"{uiConfig.PopupButton.name} | {localizedLabel}";
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            button.interactable = interactable;

            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = localizedLabel;
            }
        }

        public static void RecalculatePopupLayout(RectTransform popupRect, RectTransform popupContentRect)
        {
            if (IsFantasyWarriorInventoryHoverPopup(popupRect))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(popupRect);
                return;
            }

            RecalculatePopupSize(popupContentRect);
            RecalculatePopupSize(popupRect);
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

            var popupWidth = popupRect.rect.width * Mathf.Abs(popupRect.localScale.x);
            var popupHeight = popupRect.rect.height * Mathf.Abs(popupRect.localScale.y);
            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, 0f, Mathf.Max(0f, parentRect.width - popupWidth));
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, -Mathf.Max(0f, parentRect.height - popupHeight), 0f);
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

        private readonly struct FantasyWarriorStatRow
        {
            public readonly string Text;
            public readonly Sprite Icon;
            public readonly bool HasBar;
            public readonly float NormalizedBase;
            public readonly float NormalizedFinal;
            public readonly Color FillColor;
            public readonly Color PositiveChangeColor;
            public readonly Color NegativeChangeColor;

            private FantasyWarriorStatRow(
                string text,
                Sprite icon,
                bool hasBar,
                float normalizedBase,
                float normalizedFinal,
                Color fillColor,
                Color positiveChangeColor,
                Color negativeChangeColor)
            {
                Text = text;
                Icon = icon;
                HasBar = hasBar;
                NormalizedBase = normalizedBase;
                NormalizedFinal = normalizedFinal;
                FillColor = fillColor;
                PositiveChangeColor = positiveChangeColor;
                NegativeChangeColor = negativeChangeColor;
            }

            public static FantasyWarriorStatRow TextOnly(string text, Sprite icon)
            {
                return new FantasyWarriorStatRow(text, icon, false, 0f, 0f, Color.white, Color.white, Color.white);
            }

            public static FantasyWarriorStatRow WithBar(
                string text,
                Sprite icon,
                float normalizedBase,
                float normalizedFinal,
                Color fillColor,
                Color positiveChangeColor,
                Color negativeChangeColor)
            {
                return new FantasyWarriorStatRow(
                    text,
                    icon,
                    true,
                    Mathf.Clamp01(normalizedBase),
                    Mathf.Clamp01(normalizedFinal),
                    fillColor,
                    positiveChangeColor,
                    negativeChangeColor);
            }
        }

        private static bool TryFillFantasyWarriorInventoryHoverPopup(
            RectTransform popupRect,
            UIConfig uiConfig,
            LocalizationConfig localizationConfig,
            StatIconsConfig statIconsConfig,
            StatsController statsController,
            PlayerInventory playerInventory,
            ItemConfig itemConfig,
            ItemStack itemStack,
            bool isEquippedItemPopup,
            Color fillColor,
            Color positiveChangeColor,
            Color negativeChangeColor)
        {
            if (!IsFantasyWarriorInventoryHoverPopup(popupRect) || itemConfig == null)
            {
                return false;
            }

            SetActive(popupRect, "Input_Group", false);
            SetActive(popupRect, "Label_Equipped", isEquippedItemPopup);

            ConfigureFantasyWarriorItemName(popupRect, itemConfig.Name.GetLocalizedStringCached());
            ConfigureFantasyWarriorLabel(
                popupRect,
                "Content/Item/Name/Label_ItemRarity",
                localizationConfig.GetItemTypeDisplayName(itemConfig.ItemType),
                TextAlignmentOptions.Left,
                34f,
                22f,
                false);
            ConfigureFantasyWarriorSizeLabel(popupRect, itemConfig);

            var icon = FindImage(popupRect, "Content/Item/Icon/ICON");
            if (icon != null)
            {
                icon.sprite = itemConfig.Icon;
                icon.preserveAspect = true;
                icon.enabled = itemConfig.Icon != null;
            }

            var description = GetItemDescriptionText(itemConfig);
            ConfigureFantasyWarriorPrimaryStats(
                popupRect,
                itemConfig,
                playerInventory,
                isEquippedItemPopup,
                uiConfig.InventoryHoverWeightIcon,
                positiveChangeColor,
                negativeChangeColor);
            ConfigureFantasyWarriorStatsGroupDensity(popupRect, HasFantasyWarriorPrimaryStat(itemConfig));
            ConfigureFantasyWarriorStatsGroupLayout(popupRect);
            var descriptionText = ConfigureFantasyWarriorDescription(popupRect, description);
            ConfigureFantasyWarriorValueStats(
                popupRect,
                (itemStack?.TotalPrice ?? itemConfig.Price).ToString(CultureInfo.InvariantCulture),
                itemStack == null
                    ? itemConfig.Weight.ToString("F1", CultureInfo.InvariantCulture)
                    : itemStack.TotalWeight.ToString("F1", CultureInfo.InvariantCulture),
                uiConfig.InventoryHoverWeightIcon);

            var rows = new List<FantasyWarriorStatRow>();
            if (itemStack?.Count > 1)
            {
                rows.Add(FantasyWarriorStatRow.TextOnly($"Stack: x{itemStack.Count.ToString(CultureInfo.InvariantCulture)}", null));
            }

            AddItemSpecificRows(
                rows,
                itemConfig,
                statIconsConfig,
                statsController,
                playerInventory,
                isEquippedItemPopup,
                fillColor,
                positiveChangeColor,
                negativeChangeColor);
            FillFantasyWarriorStatRows(popupRect, rows, !string.IsNullOrWhiteSpace(description));
            ResizeFantasyWarriorDescriptionArea(popupRect, descriptionText);
            return true;
        }

        private static void AddItemSpecificRows(
            List<FantasyWarriorStatRow> rows,
            ItemConfig itemConfig,
            StatIconsConfig statIconsConfig,
            StatsController statsController,
            PlayerInventory playerInventory,
            bool isEquippedItemPopup,
            Color fillColor,
            Color positiveChangeColor,
            Color negativeChangeColor)
        {
            switch (itemConfig.ItemType)
            {
                case ItemType.Usable:
                    AddSignedRow(rows, "HP", itemConfig.HpStat, GetStatIcon(statIconsConfig, StatType.Hp));
                    AddSignedRow(rows, "Water", itemConfig.WaterStat, GetStatIcon(statIconsConfig, StatType.Water));
                    AddSignedRow(rows, "Food", itemConfig.FoodStat, GetStatIcon(statIconsConfig, StatType.Food));
                    AddSignedRow(rows, "Stamina", itemConfig.StaminaStat, GetStatIcon(statIconsConfig, StatType.Stamina));
                    break;
                case ItemType.Helm:
                case ItemType.Face:
                case ItemType.Body:
                case ItemType.Hands:
                case ItemType.Arms:
                case ItemType.Legs:
                case ItemType.Hips:
                    AddFantasyWarriorArmorRows(
                        rows,
                        itemConfig,
                        statIconsConfig,
                        statsController,
                        playerInventory,
                        isEquippedItemPopup,
                        fillColor,
                        positiveChangeColor,
                        negativeChangeColor);
                    break;
            }
        }

        private static void AddSignedRow(List<FantasyWarriorStatRow> rows, string label, float value, Sprite icon)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return;
            }

            var text = string.IsNullOrWhiteSpace(label)
                ? FormatSignedValue(value)
                : $"{label}: {FormatSignedValue(value)}";
            rows.Add(FantasyWarriorStatRow.TextOnly(text, icon));
        }

        private static void AddPercentRow(List<FantasyWarriorStatRow> rows, string label, float value, Sprite icon)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return;
            }

            var valueText = $"{FormatSignedValue(value * 100f)}%";
            var text = string.IsNullOrWhiteSpace(label)
                ? valueText
                : $"{label}: {valueText}";
            rows.Add(FantasyWarriorStatRow.TextOnly(text, icon));
        }

        private static void AddFantasyWarriorArmorRows(
            List<FantasyWarriorStatRow> rows,
            ItemConfig itemConfig,
            StatIconsConfig statIconsConfig,
            StatsController statsController,
            PlayerInventory playerInventory,
            bool isEquippedItemPopup,
            Color fillColor,
            Color positiveChangeColor,
            Color negativeChangeColor)
        {
            if (itemConfig == null || statsController == null || playerInventory == null)
            {
                return;
            }

            var equippedItemConfig = GetEquippedItemConfig(playerInventory, itemConfig.ItemType);

            foreach (var statType in DefensePopupStatTypes)
            {
                var hoveredItemValue = GetItemStatValue(itemConfig, statType);
                var stat = statsController.GetStat(statType);
                var currentEquippedValue = GetItemStatValue(equippedItemConfig, statType);

                float baseValue;
                float finalValue;

                if (statType == StatType.PhysicalDefense)
                {
                    var currentValue = PhysicalDefenseCalculator.CalculateEffective(playerInventory);
                    if (isEquippedItemPopup)
                    {
                        baseValue = PhysicalDefenseCalculator.CalculateEffective(playerInventory, itemConfig.ItemType, null);
                        finalValue = currentValue;
                    }
                    else
                    {
                        baseValue = currentValue;
                        finalValue = PhysicalDefenseCalculator.CalculateEffective(playerInventory, itemConfig.ItemType, itemConfig);
                    }
                }
                else if (isEquippedItemPopup)
                {
                    var currentValue = stat.Value.Value;
                    baseValue = currentValue - hoveredItemValue;
                    finalValue = currentValue;
                }
                else if (equippedItemConfig == null)
                {
                    var currentValue = stat.Value.Value;
                    baseValue = currentValue;
                    finalValue = currentValue + hoveredItemValue;
                }
                else
                {
                    var currentValue = stat.Value.Value;
                    baseValue = currentValue;
                    finalValue = currentValue - currentEquippedValue + hoveredItemValue;
                }

                var normalizedBase = GetNormalizedPopupStatValue(stat, baseValue);
                var normalizedFinal = GetNormalizedPopupStatValue(stat, finalValue);
                var displayValue = finalValue - baseValue;
                if (Mathf.Approximately(displayValue, 0f))
                {
                    continue;
                }

                var text = statType == StatType.PhysicalDefense
                    ? $"{FormatSignedValue(displayValue * 100f)}%"
                    : $"{FormatSignedValue(displayValue)}%";

                rows.Add(FantasyWarriorStatRow.WithBar(
                    text,
                    GetStatIcon(statIconsConfig, statType),
                    normalizedBase,
                    normalizedFinal,
                    fillColor,
                    positiveChangeColor,
                    negativeChangeColor));
            }
        }

        private static void FillFantasyWarriorStatRows(RectTransform popupRect, IReadOnlyList<FantasyWarriorStatRow> rows, bool hasDescription)
        {
            var rowIndex = 0;
            for (var i = 0; i < 4; i++)
            {
                var rowRect = FindFantasyWarriorStatRow(popupRect, i);
                if (rowRect == null)
                {
                    continue;
                }

                var hasRow = rowIndex < rows.Count;
                rowRect.gameObject.SetActive(hasRow);
                if (!hasRow)
                {
                    if (rowRect.TryGetComponent(out LayoutElement inactiveLayoutElement))
                    {
                        inactiveLayoutElement.ignoreLayout = true;
                    }

                    continue;
                }

                rowRect.name = $"HUD_ItemStat_{rowIndex:00}";
                rowRect.SetSiblingIndex(rowIndex);
                ConfigureFantasyWarriorStatRowLayout(rowRect);
                rowRect.anchoredPosition = new Vector2(0f, -GetFantasyWarriorRowsBlockHeight(rowIndex));
                var row = rows[rowIndex];
                SetText(rowRect, "Label_Stat_Text", row.Text);
                var icon = FindImage(rowRect, "ICON");
                if (icon != null)
                {
                    icon.sprite = row.Icon;
                    icon.enabled = row.Icon != null;
                }

                ConfigureFantasyWarriorArmorBar(rowRect, row);
                rowIndex++;
            }

            var background = FindRect(popupRect, "Content/Stats_Group/Background");
            if (background != null)
            {
                background.SetSiblingIndex(rowIndex);
                if (background.TryGetComponent(out LayoutElement backgroundLayoutElement))
                {
                    backgroundLayoutElement.ignoreLayout = false;
                }

                background.anchorMin = new Vector2(0.5f, 1f);
                background.anchorMax = new Vector2(0.5f, 1f);
                background.pivot = new Vector2(0.5f, 1f);
                background.anchoredPosition = new Vector2(0f, -GetFantasyWarriorRowsBlockHeight(rowIndex));
            }

            SetActive(popupRect, "Content/Stats_Group/Background", hasDescription);
        }

        private static void ConfigureFantasyWarriorArmorBar(RectTransform rowRect, FantasyWarriorStatRow row)
        {
            var barRoot = FindRect(rowRect, "HUD_Stat_Bar");
            if (!row.HasBar)
            {
                if (barRoot != null)
                {
                    barRoot.gameObject.SetActive(false);
                }

                return;
            }

            if (barRoot == null)
            {
                var barObject = new GameObject("HUD_Stat_Bar", typeof(RectTransform));
                barRoot = barObject.GetComponent<RectTransform>();
                barRoot.SetParent(rowRect, false);
            }

            barRoot.gameObject.SetActive(true);
            barRoot.anchorMin = new Vector2(0f, 0.5f);
            barRoot.anchorMax = new Vector2(1f, 0.5f);
            barRoot.pivot = new Vector2(0.5f, 0.5f);
            barRoot.offsetMin = new Vector2(92f, -9f);
            barRoot.offsetMax = new Vector2(-170f, 9f);

            var backFill = FindOrCreateFantasyWarriorBarImage(barRoot, "Back");
            var changedFill = FindOrCreateFantasyWarriorBarImage(barRoot, "Changed");
            var fill = FindOrCreateFantasyWarriorBarImage(barRoot, "Fill");

            backFill.type = Image.Type.Simple;
            changedFill.type = Image.Type.Simple;
            fill.type = Image.Type.Simple;

            backFill.color = Color.black;
            fill.color = Color.white;

            SetFantasyWarriorBarSegment(backFill.rectTransform, 0f, 1f);

            if (row.NormalizedFinal > row.NormalizedBase)
            {
                SetFantasyWarriorBarSegment(fill.rectTransform, 0f, row.NormalizedBase);
                SetFantasyWarriorBarSegment(changedFill.rectTransform, row.NormalizedBase, row.NormalizedFinal);
                changedFill.color = EnsureVisibleBarColor(row.PositiveChangeColor, Color.green);
                changedFill.gameObject.SetActive(true);
            }
            else if (row.NormalizedFinal < row.NormalizedBase)
            {
                SetFantasyWarriorBarSegment(fill.rectTransform, 0f, row.NormalizedFinal);
                SetFantasyWarriorBarSegment(changedFill.rectTransform, row.NormalizedFinal, row.NormalizedBase);
                changedFill.color = EnsureVisibleBarColor(row.NegativeChangeColor, Color.red);
                changedFill.gameObject.SetActive(true);
            }
            else
            {
                SetFantasyWarriorBarSegment(fill.rectTransform, 0f, row.NormalizedFinal);
                changedFill.color = Color.white;
                changedFill.gameObject.SetActive(false);
            }

            backFill.transform.SetSiblingIndex(0);
            changedFill.transform.SetSiblingIndex(1);
            fill.transform.SetSiblingIndex(2);

            var textRect = FindRect(rowRect, "Label_Stat_Text");
            if (textRect != null)
            {
                textRect.anchorMin = new Vector2(1f, 0.5f);
                textRect.anchorMax = new Vector2(1f, 0.5f);
                textRect.pivot = new Vector2(1f, 0.5f);
                textRect.anchoredPosition = new Vector2(-28f, 0f);
                textRect.sizeDelta = new Vector2(130f, 48f);
            }
        }

        private static Color EnsureVisibleBarColor(Color configuredColor, Color fallback)
        {
            if (configuredColor.a <= 0.01f)
            {
                configuredColor = fallback;
            }

            configuredColor.a = 1f;
            return configuredColor;
        }

        private static void SetFantasyWarriorBarSegment(RectTransform rect, float normalizedMin, float normalizedMax)
        {
            if (rect == null)
            {
                return;
            }

            normalizedMin = Mathf.Clamp01(normalizedMin);
            normalizedMax = Mathf.Clamp01(normalizedMax);
            if (normalizedMax < normalizedMin)
            {
                (normalizedMin, normalizedMax) = (normalizedMax, normalizedMin);
            }

            rect.anchorMin = new Vector2(normalizedMin, 0f);
            rect.anchorMax = new Vector2(normalizedMax, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image FindOrCreateFantasyWarriorBarImage(RectTransform barRoot, string name)
        {
            var image = FindImage(barRoot, name);
            RectTransform imageRect;
            if (image == null)
            {
                var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imageRect = imageObject.GetComponent<RectTransform>();
                imageRect.SetParent(barRoot, false);
                image = imageObject.GetComponent<Image>();
            }
            else
            {
                imageRect = image.GetComponent<RectTransform>();
            }

            image.gameObject.SetActive(true);
            image.raycastTarget = false;
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            return image;
        }

        private static RectTransform FindFantasyWarriorStatRow(RectTransform popupRect, int index)
        {
            return FindRect(popupRect, $"Content/Stats_Group/HUD_Stat_{index:00}")
                   ?? FindRect(popupRect, $"Content/Stats_Group/HUD_ItemStat_{index:00}")
                   ?? FindRect(popupRect, $"Content/Stats_Group/HUD_ArmorStat_{index:00}");
        }

        private static void ConfigureFantasyWarriorItemName(RectTransform popupRect, string itemName)
        {
            var text = FindText(popupRect, "Content/Item/Name/Label_ItemName");
            if (text == null)
            {
                return;
            }

            text.text = itemName;
            text.alignment = TextAlignmentOptions.Left;
            text.enableAutoSizing = true;
            text.fontSizeMax = 40f;
            text.fontSizeMin = 20f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void ConfigureFantasyWarriorPrimaryStats(
            RectTransform popupRect,
            ItemConfig itemConfig,
            PlayerInventory playerInventory,
            bool isEquippedItemPopup,
            Sprite backpackIcon,
            Color positiveChangeColor,
            Color negativeChangeColor)
        {
            var primaryStat = FindRect(popupRect, "Content/HUD_Stat_Base_Large");
            if (primaryStat == null || itemConfig == null)
            {
                return;
            }

            if (itemConfig.ItemType == ItemType.Weapon)
            {
                primaryStat.name = "HUD_Stat_WeaponDamage";
                primaryStat.gameObject.SetActive(true);
                ConfigureFantasyWarriorFullStatRect(primaryStat);
                ConfigureFantasyWarriorLabel(
                    primaryStat,
                    "Label_Stat_Number",
                    FormatWeaponDamage(itemConfig),
                    TextAlignmentOptions.Left,
                    76f,
                    48f,
                    false);
                SetText(primaryStat, "Label_Stat_Text", string.Empty);
                return;
            }

            if (itemConfig.ItemType == ItemType.Backpack)
            {
                primaryStat.name = "HUD_Stat_BackpackInventorySize";
                primaryStat.gameObject.SetActive(true);
                ConfigureFantasyWarriorFullStatRect(primaryStat);
                ConfigureFantasyWarriorPrimaryStatIcon(primaryStat, backpackIcon);
                ConfigureFantasyWarriorLabel(
                    primaryStat,
                    "Label_Stat_Number",
                    FormatBackpackInventorySize(itemConfig, playerInventory, isEquippedItemPopup, positiveChangeColor, negativeChangeColor),
                    TextAlignmentOptions.Left,
                    76f,
                    42f,
                    false);

                var numberText = FindText(primaryStat, "Label_Stat_Number");
                if (numberText != null)
                {
                    numberText.richText = true;
                    numberText.color = Color.white;
                }

                SetText(primaryStat, "Label_Stat_Text", string.Empty);
                return;
            }

            primaryStat.name = "HUD_Stat_WeaponDamage_Unused";
            primaryStat.gameObject.SetActive(false);
        }

        private static bool HasFantasyWarriorPrimaryStat(ItemConfig itemConfig)
        {
            return itemConfig?.ItemType is ItemType.Weapon or ItemType.Backpack;
        }

        private static void ConfigureFantasyWarriorPrimaryStatIcon(RectTransform statRect, Sprite iconSprite)
        {
            var icon = FindImage(statRect, "ICON");
            if (icon == null)
            {
                return;
            }

            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;
        }

        private static void ConfigureFantasyWarriorFullStatRect(RectTransform statRect)
        {
            if (statRect == null)
            {
                return;
            }

            statRect.anchorMin = new Vector2(0f, statRect.anchorMin.y);
            statRect.anchorMax = new Vector2(1f, statRect.anchorMax.y);
            statRect.pivot = new Vector2(0.5f, statRect.pivot.y);
            statRect.offsetMin = new Vector2(0f, statRect.offsetMin.y);
            statRect.offsetMax = new Vector2(0f, statRect.offsetMax.y);
            statRect.anchoredPosition = new Vector2(0f, statRect.anchoredPosition.y);
        }

        private static void ConfigureFantasyWarriorValueStats(
            RectTransform popupRect,
            string priceText,
            string weightText,
            Sprite weightIcon)
        {
            var valueGroup = FindRect(popupRect, "Content/Value_PriceWeight") ?? FindRect(popupRect, "Content/Value");
            if (valueGroup == null)
            {
                return;
            }

            valueGroup.name = "Value_PriceWeight";

            var priceStat = FindRect(valueGroup, "HUD_Stat_ItemPrice") ?? FindRect(valueGroup, "HUD_Stat_Value");
            if (priceStat == null)
            {
                return;
            }

            priceStat.name = "HUD_Stat_ItemPrice";
            ConfigureFantasyWarriorValueRect(priceStat, true);
            ConfigureFantasyWarriorLabel(priceStat, "Label_Stat_Text", priceText, TextAlignmentOptions.Left, 56f, 34f, false);

            var weightStat = FindRect(valueGroup, "HUD_Stat_ItemWeight");
            if (weightStat == null)
            {
                weightStat = Object.Instantiate(priceStat, valueGroup);
            }

            weightStat.name = "HUD_Stat_ItemWeight";
            weightStat.SetSiblingIndex(priceStat.GetSiblingIndex() + 1);
            ConfigureFantasyWarriorValueRect(weightStat, false);
            ConfigureFantasyWarriorLabel(weightStat, "Label_Stat_Text", weightText, TextAlignmentOptions.Right, 56f, 34f, false);
            ConfigureFantasyWarriorWeightValueContent(weightStat, priceStat);

            var weightIconImage = FindOrCreateFantasyWarriorValueIcon(weightStat, priceStat, true);
            if (weightIconImage != null && weightIcon != null)
            {
                weightIconImage.sprite = weightIcon;
                weightIconImage.enabled = true;
                weightIconImage.gameObject.SetActive(true);
                weightIconImage.preserveAspect = true;
                weightIconImage.color = Color.white;
                weightIconImage.raycastTarget = false;
            }
        }

        private static Image FindOrCreateFantasyWarriorValueIcon(RectTransform valueStat, RectTransform templateStat, bool alignRight)
        {
            if (valueStat == null)
            {
                return null;
            }

            var icon = FindImage(valueStat, "ICON");
            RectTransform iconRect;
            if (icon != null)
            {
                iconRect = icon.GetComponent<RectTransform>();
            }
            else if (FindImage(templateStat, "ICON") is { } templateIcon)
            {
                var iconObject = Object.Instantiate(templateIcon.gameObject, valueStat);
                iconObject.name = "ICON";
                icon = iconObject.GetComponent<Image>();
                iconRect = iconObject.GetComponent<RectTransform>();
            }
            else
            {
                var iconObject = new GameObject("ICON", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.SetParent(valueStat, false);
                icon = iconObject.GetComponent<Image>();
            }

            iconRect.anchorMin = new Vector2(alignRight ? 1f : 0f, 0.5f);
            iconRect.anchorMax = new Vector2(alignRight ? 1f : 0f, 0.5f);
            iconRect.pivot = new Vector2(alignRight ? 1f : 0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(alignRight ? -18f : 18f, 0f);
            iconRect.sizeDelta = new Vector2(44f, 44f);
            return icon;
        }

        private static void ConfigureFantasyWarriorWeightValueContent(RectTransform weightStat, RectTransform priceStat)
        {
            var textRect = FindRect(weightStat, "Label_Stat_Text");
            if (textRect == null)
            {
                return;
            }

            var priceTextRect = FindRect(priceStat, "Label_Stat_Text");
            var priceText = FindText(priceStat, "Label_Stat_Text");
            var weightText = textRect.GetComponent<TMP_Text>();
            if (priceText != null && weightText != null)
            {
                weightText.enableAutoSizing = priceText.enableAutoSizing;
                weightText.fontSize = priceText.fontSize;
                weightText.fontSizeMax = priceText.fontSizeMax;
                weightText.fontSizeMin = priceText.fontSizeMin;
            }

            var priceTextSize = priceTextRect != null
                ? new Vector2(
                    Mathf.Max(priceTextRect.sizeDelta.x, priceTextRect.rect.width),
                    Mathf.Max(priceTextRect.sizeDelta.y, priceTextRect.rect.height))
                : new Vector2(180f, 64f);

            textRect.anchorMin = new Vector2(1f, 0.5f);
            textRect.anchorMax = new Vector2(1f, 0.5f);
            textRect.pivot = new Vector2(1f, 0.5f);
            textRect.anchoredPosition = new Vector2(-72f, 0f);
            textRect.sizeDelta = new Vector2(Mathf.Max(180f, priceTextSize.x), Mathf.Max(64f, priceTextSize.y));
        }

        private static void ConfigureFantasyWarriorValueRect(RectTransform valueRect, bool leftAligned)
        {
            if (valueRect == null)
            {
                return;
            }

            valueRect.anchorMin = leftAligned ? new Vector2(0f, 0f) : new Vector2(0.5f, 0f);
            valueRect.anchorMax = leftAligned ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
            valueRect.pivot = leftAligned ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;
            valueRect.anchoredPosition = Vector2.zero;
        }

        private static void ConfigureFantasyWarriorSizeLabel(RectTransform popupRect, ItemConfig itemConfig)
        {
            if (popupRect == null || itemConfig == null)
            {
                return;
            }

            var labelRect = FindRect(popupRect, "Label_ItemSize");
            if (labelRect == null)
            {
                var labelObject = new GameObject("Label_ItemSize", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.SetParent(popupRect, false);
            }

            labelRect.anchorMin = new Vector2(1f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(1f, 1f);
            labelRect.anchoredPosition = new Vector2(-24f, -24f);
            labelRect.sizeDelta = new Vector2(170f, 68f);

            var text = labelRect.GetComponent<TMP_Text>();
            ApplyFantasyWarriorFont(popupRect, text);
            text.text = FormatItemSize(itemConfig);
            text.alignment = TextAlignmentOptions.TopRight;
            text.enableAutoSizing = true;
            text.fontSizeMax = 54f;
            text.fontSizeMin = 34f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
        }

        private static TMP_Text ConfigureFantasyWarriorDescription(RectTransform popupRect, string description)
        {
            var backgroundRect = FindRect(popupRect, "Content/Stats_Group/Background");
            if (backgroundRect == null)
            {
                return null;
            }

            var descriptionRect = FindRect(backgroundRect, "Label_ItemDescription");
            if (descriptionRect == null)
            {
                var descriptionObject = new GameObject("Label_ItemDescription", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                descriptionRect = descriptionObject.GetComponent<RectTransform>();
                descriptionRect.SetParent(backgroundRect, false);
            }

            descriptionRect.anchorMin = new Vector2(0f, 1f);
            descriptionRect.anchorMax = new Vector2(0f, 1f);
            descriptionRect.pivot = new Vector2(0f, 1f);
            descriptionRect.anchoredPosition = new Vector2(28f, -14f);

            var text = descriptionRect.GetComponent<TMP_Text>();
            ApplyFantasyWarriorFont(popupRect, text);
            text.text = string.IsNullOrWhiteSpace(description) ? GetFallbackDescriptionText() : description.Trim();
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableAutoSizing = true;
            text.fontSizeMax = 32f;
            text.fontSizeMin = 18f;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;

            return text;
        }

        private static void ConfigureFantasyWarriorStatsGroupDensity(RectTransform popupRect, bool hasPrimaryStat)
        {
            var statsGroup = FindRect(popupRect, "Content/Stats_Group");
            if (statsGroup == null)
            {
                return;
            }

            statsGroup.anchoredPosition = hasPrimaryStat
                ? new Vector2(statsGroup.anchoredPosition.x, -340f)
                : new Vector2(statsGroup.anchoredPosition.x, -250f);
            statsGroup.sizeDelta = hasPrimaryStat
                ? new Vector2(statsGroup.sizeDelta.x, 220f)
                : new Vector2(statsGroup.sizeDelta.x, 420f);

            var background = FindRect(statsGroup, "Background");
            if (background != null && background.TryGetComponent(out LayoutElement layoutElement))
            {
                layoutElement.ignoreLayout = false;
                layoutElement.preferredHeight = hasPrimaryStat ? 100f : 190f;
                layoutElement.minHeight = hasPrimaryStat ? 80f : 160f;
            }
        }

        private static void ConfigureFantasyWarriorStatsGroupLayout(RectTransform popupRect)
        {
            var statsGroup = FindRect(popupRect, "Content/Stats_Group");
            if (statsGroup == null)
            {
                return;
            }

            var layoutGroup = statsGroup.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.enabled = false;
            }
        }

        private static void ConfigureFantasyWarriorLabel(
            Transform root,
            string path,
            string value,
            TextAlignmentOptions alignment,
            float fontSizeMax,
            float fontSizeMin,
            bool wrap)
        {
            var text = FindText(root, path);
            if (text == null)
            {
                return;
            }

            ApplyFantasyWarriorFont(root, text);
            text.text = value;
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSizeMax;
            text.fontSizeMin = fontSizeMin;
            text.enableWordWrapping = wrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static string FormatWeaponDamage(ItemConfig itemConfig)
        {
            return $"{FormatWeaponDamageRange(itemConfig.WeaponDamageRange)} / " +
                   FormatWeaponDamageRange(itemConfig.HeavyWeaponDamageRange);
        }

        private static string FormatWeaponDamageRange(Vector2Int damageRange)
        {
            var min = Mathf.Min(damageRange.x, damageRange.y);
            var max = Mathf.Max(damageRange.x, damageRange.y);
            return $"{min.ToString(CultureInfo.InvariantCulture)}-{max.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string FormatBackpackInventorySize(
            ItemConfig backpackItemConfig,
            PlayerInventory playerInventory,
            bool isEquippedItemPopup,
            Color positiveChangeColor,
            Color negativeChangeColor)
        {
            var backpackSize = backpackItemConfig.BackpackSize;
            var referenceSize = isEquippedItemPopup
                ? playerInventory?.BaseInventorySize ?? backpackSize
                : GetCurrentPlayerInventorySize(playerInventory, backpackSize);
            var delta = backpackSize - referenceSize;
            var sizeText = FormatInventorySize(backpackSize);
            if (delta == Vector2Int.zero)
            {
                return sizeText;
            }

            var deltaColor = isEquippedItemPopup
                ? EnsureVisibleBarColor(positiveChangeColor, Color.green)
                : GetInventorySizeDeltaColor(referenceSize, backpackSize, positiveChangeColor, negativeChangeColor);
            return $"{sizeText} <color=#{ColorUtility.ToHtmlStringRGBA(deltaColor)}>- {FormatInventorySizeDelta(referenceSize, backpackSize)}</color>";
        }

        private static Vector2Int GetCurrentPlayerInventorySize(PlayerInventory playerInventory, Vector2Int fallbackSize)
        {
            return playerInventory?.Tiles?.tiles == null
                ? fallbackSize
                : new Vector2Int(playerInventory.Tiles.tiles.GetLength(0), playerInventory.Tiles.tiles.GetLength(1));
        }

        private static Color GetInventorySizeDeltaColor(
            Vector2Int referenceSize,
            Vector2Int targetSize,
            Color positiveChangeColor,
            Color negativeChangeColor)
        {
            var referenceArea = referenceSize.x * referenceSize.y;
            var targetArea = targetSize.x * targetSize.y;
            if (targetArea > referenceArea)
            {
                return EnsureVisibleBarColor(positiveChangeColor, Color.green);
            }

            if (targetArea < referenceArea)
            {
                return EnsureVisibleBarColor(negativeChangeColor, Color.red);
            }

            return Color.white;
        }

        private static string FormatInventorySize(Vector2Int size)
        {
            return $"{size.x.ToString(CultureInfo.InvariantCulture)}x{size.y.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string FormatInventorySizeDelta(Vector2Int referenceSize, Vector2Int targetSize)
        {
            var delta = targetSize - referenceSize;
            var referenceArea = referenceSize.x * referenceSize.y;
            var targetArea = targetSize.x * targetSize.y;
            var sign = targetArea >= referenceArea ? "+" : "-";
            return $"{sign}{Mathf.Abs(delta.x).ToString(CultureInfo.InvariantCulture)}x{Mathf.Abs(delta.y).ToString(CultureInfo.InvariantCulture)}";
        }

        private static string FormatItemSize(ItemConfig itemConfig)
        {
            return itemConfig == null
                ? "0x0"
                : $"{itemConfig.Size.x.ToString(CultureInfo.InvariantCulture)}x{itemConfig.Size.y.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string GetItemDescriptionText(ItemConfig itemConfig)
        {
            var description = itemConfig?.Description?.GetLocalizedStringCached();
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            description = itemConfig?.Description?.GetLocalizedString();
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            return GetFallbackDescriptionText();
        }

        private static string GetFallbackDescriptionText()
        {
            var fallback = new LocalizedString("Tables", "Null String");
            var text = fallback.GetLocalizedStringCached();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            text = fallback.GetLocalizedString();
            return string.IsNullOrWhiteSpace(text) ? "Null String" : text;
        }

        private static void ResizeFantasyWarriorDescriptionArea(RectTransform popupRect, TMP_Text descriptionText)
        {
            var statsGroup = FindRect(popupRect, "Content/Stats_Group");
            var background = FindRect(popupRect, "Content/Stats_Group/Background");
            var descriptionRect = FindRect(background, "Label_ItemDescription");
            if (popupRect == null || statsGroup == null || background == null || descriptionRect == null || descriptionText == null)
            {
                return;
            }

            ConfigureFantasyWarriorStatsGroupLayout(popupRect);

            var statsWidth = statsGroup.rect.width > 0f ? statsGroup.rect.width : 740f;
            var textWidth = Mathf.Max(220f, statsWidth - 116f);
            var preferredText = descriptionText.GetPreferredValues(descriptionText.text, textWidth, 0f);
            var textHeight = Mathf.Clamp(preferredText.y, 32f, 330f);
            var backgroundWidth = textWidth + 56f;
            var backgroundHeight = Mathf.Clamp(textHeight + 34f, 72f, 390f);
            var statsRowsHeight = GetActiveFantasyWarriorStatRowsHeight(statsGroup, out var activeRows);
            var descriptionActive = background.gameObject.activeSelf;
            var activeChildren = activeRows + (descriptionActive ? 1 : 0);
            var spacingHeight = 8f * Mathf.Max(0, activeChildren - 1);
            var paddingHeight = 0f;

            background.anchorMin = new Vector2(0.5f, 1f);
            background.anchorMax = new Vector2(0.5f, 1f);
            background.pivot = new Vector2(0.5f, 1f);
            background.sizeDelta = new Vector2(backgroundWidth, backgroundHeight);
            descriptionRect.sizeDelta = new Vector2(textWidth, textHeight);

            if (background.TryGetComponent(out LayoutElement layoutElement))
            {
                layoutElement.ignoreLayout = false;
                layoutElement.minWidth = backgroundWidth;
                layoutElement.preferredWidth = backgroundWidth;
                layoutElement.minHeight = backgroundHeight;
                layoutElement.preferredHeight = backgroundHeight;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(background);
            LayoutRebuilder.ForceRebuildLayoutImmediate(statsGroup);
            var statsGroupHeight = statsRowsHeight + (descriptionActive ? backgroundHeight : 0f) + spacingHeight + paddingHeight;
            statsGroup.sizeDelta = new Vector2(statsGroup.sizeDelta.x, statsGroupHeight);

            var value = FindRect(popupRect, "Content/Value_PriceWeight") ?? FindRect(popupRect, "Content/Value");
            var valueHeight = value != null && value.rect.height > 0f ? value.rect.height : 80f;
            var descriptionBottom = Mathf.Abs(statsGroup.anchoredPosition.y) + statsGroupHeight;
            var hasPrimaryStat = HasActiveFantasyWarriorPrimaryStat(popupRect);
            var fixedBottomArea = valueHeight + 92f;
            var minimumHeight = descriptionBottom + fixedBottomArea;
            var requiredHeight = Mathf.Clamp(minimumHeight, hasPrimaryStat ? 560f : 0f, 980f);
            popupRect.sizeDelta = new Vector2(popupRect.sizeDelta.x, requiredHeight);

            var content = FindRect(popupRect, "Content");
            if (content != null)
            {
                content.offsetMin = new Vector2(content.offsetMin.x, 40f);
                content.offsetMax = new Vector2(content.offsetMax.x, -40f);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(popupRect);
        }

        private static void ConfigureFantasyWarriorStatRowLayout(RectTransform rowRect)
        {
            if (rowRect == null)
            {
                return;
            }

            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(-60f, 48f);

            var layoutElement = rowRect.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = rowRect.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = false;
            layoutElement.minHeight = 44f;
            layoutElement.preferredHeight = 48f;
            layoutElement.flexibleHeight = 0f;
        }

        private static float GetActiveFantasyWarriorStatRowsHeight(RectTransform statsGroup, out int activeRows)
        {
            activeRows = 0;
            if (statsGroup == null)
            {
                return 0f;
            }

            var height = 0f;
            for (var i = 0; i < statsGroup.childCount; i++)
            {
                if (statsGroup.GetChild(i) is not RectTransform row
                 || !row.gameObject.activeSelf
                 || !row.name.StartsWith("HUD_ItemStat_"))
                {
                    continue;
                }

                height += row.TryGetComponent(out LayoutElement layoutElement)
                    ? Mathf.Max(layoutElement.preferredHeight, row.rect.height)
                    : Mathf.Max(48f, row.rect.height);
                activeRows++;
            }

            return height;
        }

        private static float GetFantasyWarriorRowsBlockHeight(int rowCount)
        {
            return rowCount <= 0 ? 0f : rowCount * 48f + Mathf.Max(0, rowCount) * 8f;
        }

        private static bool HasActiveFantasyWarriorPrimaryStat(RectTransform popupRect)
        {
            return FindRect(popupRect, "Content/HUD_Stat_WeaponDamage")?.gameObject.activeSelf == true
                   || FindRect(popupRect, "Content/HUD_Stat_BackpackInventorySize")?.gameObject.activeSelf == true
                   || FindRect(popupRect, "Content/HUD_Stat_Base_Large")?.gameObject.activeSelf == true;
        }

        private static void ApplyFantasyWarriorFont(Transform root, TMP_Text target)
        {
            if (root == null || target == null)
            {
                return;
            }

            var source = FindText(root, "Content/Item/Name/Label_ItemName")
                         ?? FindText(root, "Label_Stat_Text")
                         ?? root.GetComponentInChildren<TMP_Text>(true);
            if (source == null || source == target)
            {
                return;
            }

            target.font = source.font;
            target.fontSharedMaterial = source.fontSharedMaterial;
        }

        private static void PrepareFantasyWarriorInventoryHoverPopup(RectTransform popupRect)
        {
            if (popupRect == null)
            {
                return;
            }

            popupRect.anchorMin = new Vector2(0f, 1f);
            popupRect.anchorMax = new Vector2(0f, 1f);
            popupRect.pivot = new Vector2(0f, 1f);
            popupRect.localScale = Vector3.one * 0.55f;
            SetPopupRaycastState(popupRect, false);
        }

        private static bool IsFantasyWarriorInventoryHoverPopup(RectTransform popupRect)
        {
            return popupRect != null
                && popupRect.name.Contains("HUD_FantasyWarrior_ItemPickupInfo04")
                && popupRect.Find("Content/Item/Icon/ICON") != null;
        }

        private static RectTransform FindRect(Transform root, string path)
        {
            return root == null ? null : root.Find(path) as RectTransform;
        }

        private static Image FindImage(Transform root, string path)
        {
            var rect = FindRect(root, path);
            return rect == null ? null : rect.GetComponent<Image>();
        }

        private static TMP_Text FindText(Transform root, string path)
        {
            var rect = FindRect(root, path);
            return rect == null ? null : rect.GetComponent<TMP_Text>();
        }

        private static void SetText(Transform root, string path, string value)
        {
            var text = FindText(root, path);
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetActive(Transform root, string path, bool isActive)
        {
            var rect = FindRect(root, path);
            if (rect != null)
            {
                rect.gameObject.SetActive(isActive);
            }
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

            var popupTitle = resolver.Instantiate(uiConfig.PopupItemName, popupRect);
            popupTitle.name = $"{uiConfig.PopupItemName.name} | {itemConfig.name}";

            if (popupTitle.Text != null)
            {
                popupTitle.Text.text = itemConfig.Name.GetLocalizedStringCached();
            }

            popupTitle.transform.SetAsFirstSibling();
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
            popupWeight.text = itemStack.TotalWeight.ToString("F1", CultureInfo.InvariantCulture);
        }

        private static void CreateBackpackSizePopup
            (
                RectTransform popupRect,
                UIConfig uiConfig,
                IObjectResolver resolver,
                ItemConfig backpackItemConfig
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
                var currentEquippedValue = GetItemStatValue(equippedItemConfig, statType);
                var hoveredItemValue = GetItemStatValue(itemConfig, statType);

                float baseValue;
                float finalValue;

                if (statType == StatType.PhysicalDefense)
                {
                    var currentValue = PhysicalDefenseCalculator.CalculateEffective(playerInventory);
                    if (isEquippedItemPopup)
                    {
                        baseValue = PhysicalDefenseCalculator.CalculateEffective(playerInventory, itemConfig.ItemType, null);
                        finalValue = currentValue;
                    }
                    else
                    {
                        baseValue = currentValue;
                        finalValue = PhysicalDefenseCalculator.CalculateEffective(playerInventory, itemConfig.ItemType, itemConfig);
                    }
                }
                else if (isEquippedItemPopup)
                {
                    var currentValue = stat.Value.Value;
                    baseValue = currentValue - hoveredItemValue;
                    finalValue = currentValue;
                }
                else if (equippedItemConfig == null)
                {
                    var currentValue = stat.Value.Value;
                    baseValue = currentValue;
                    finalValue = currentValue + hoveredItemValue;
                }
                else
                {
                    var currentValue = stat.Value.Value;
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
                StatType.Stamina => itemConfig.StaminaStat,
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
