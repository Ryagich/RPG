using System.Collections.Generic;
using GameModes;
using Localization;
using MessagePipe;
using Messages;
using Quests;
using Stats;
using TMPro;
using UI.Configs;
using UI.Map;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace UI.Pages
{
    public class MapPage : BasePage, ITickable
    {
        public override PageType Type { get; } = PageType.Map;

        private readonly UIConfig uiConfig;
        private readonly MapConfig mapConfig;
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly StatFiller hpFiller;
        private readonly QuestController questController;
        private readonly Transform playerTransform;
        private readonly Animator playerAnimator;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect;
        private ScrollRect mapScroll;
        private CharacterIcon characterIcon;
        private MapScrollController mapScrollController;
        private Title title;
        private ScrollRect questionsScrollView;
        private readonly List<QuestShortInfo> questEntries = new();
        private readonly List<MapQuestMarkerData> questMarkers = new();
        private Image bloodScreen;
        private HeartbeatPulse heartbeatPulse;
        private BloodScreenController bloodScreenController;
        private bool isQuestScrollVisible = true;
        private RectTransform popupRect;
        private QuestProgress hoverPopupTarget;
        private float hoverPopupElapsed;

        public MapPage(
            UIConfig uiConfig,
            MapConfig mapConfig,
            StatsConfig statsConfig,
            StatsController statsController,
            StatFillers statFillers,
            QuestController questController,
            Transform playerTransform,
            Animator playerAnimator,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher)
        {
            this.uiConfig = uiConfig;
            this.mapConfig = mapConfig;
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            hpFiller = statFillers.Get(StatType.Hp);
            this.questController = questController;
            this.playerTransform = playerTransform;
            this.playerAnimator = playerAnimator;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            mapScroll = resolver.Instantiate(uiConfig.MapScroll, contentRect);
            mapScroll.name = $"{uiConfig.MapScroll.name} | {Type}";

            characterIcon = resolver.Instantiate(uiConfig.CharacterIcon, mapScroll.content);
            characterIcon.name = $"{uiConfig.CharacterIcon.name} | {Type}";

            mapScrollController = mapScroll.GetComponent<MapScrollController>();
            if (mapScrollController == null)
            {
                mapScrollController = mapScroll.gameObject.AddComponent<MapScrollController>();
            }

            mapScrollController.Initialize(
                mapScroll,
                characterIcon,
                playerTransform,
                playerAnimator != null ? playerAnimator.transform : playerTransform,
                mapConfig);

            title = resolver.Instantiate(uiConfig.Title, contentRect);
            title.name = $"{uiConfig.Title.name} | {Type}";
            title.ExitButton.onClick.AddListener(CloseMap);
            if (title.QuestButton != null)
            {
                title.QuestButton.onClick.AddListener(ToggleQuestScrollVisibility);
            }

            questionsScrollView = resolver.Instantiate(uiConfig.QuestionsScrollView, contentRect);
            questionsScrollView.name = $"{uiConfig.QuestionsScrollView.name} | {Type}";

            bloodScreen = PageUiUtilities.CreateBloodScreen(uiConfig, resolver, contentRect, Type);
            heartbeatPulse = new HeartbeatPulse(statsConfig, statsController.Hp, hpFiller);
            bloodScreenController = new BloodScreenController(statsConfig, statsController.Hp, hpFiller, heartbeatPulse, bloodScreen);

            FillQuestStates();
        }

        public void Tick()
        {
            HandleQuestPopup();
        }

        public override void Hide()
        {
            ClosePopup();
            ResetHoverPopupState();

            bloodScreenController?.Dispose();
            bloodScreenController = null;

            heartbeatPulse?.Dispose();
            heartbeatPulse = null;

            if (title != null)
            {
                if (title.QuestButton != null)
                {
                    title.QuestButton.onClick.RemoveListener(ToggleQuestScrollVisibility);
                }

                title.ExitButton.onClick.RemoveListener(CloseMap);
                title = null;
            }

            questEntries.Clear();
            questMarkers.Clear();
            mapScrollController = null;
            characterIcon = null;
            mapScroll = null;
            questionsScrollView = null;
            bloodScreen = null;
            popupRect = null;

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }

        private void CloseMap()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }

        private void FillQuestStates()
        {
            if (questionsScrollView == null || questionsScrollView.content == null || uiConfig.QuestShortInfo == null)
            {
                return;
            }

            ClearQuestEntries();
            questMarkers.Clear();

            foreach (QuestProgress questProgress in questController.Progress)
            {
                if (questProgress == null || questProgress.IsCompleted)
                {
                    continue;
                }

                AddQuestEntry(questProgress);
                AddQuestMarker(questProgress);
            }

            mapScrollController?.SetQuestMarkers(uiConfig.MapIcon, questMarkers);
            RefreshQuestScrollContent();
            ApplyQuestScrollVisibility();
        }

        private void ClearQuestEntries()
        {
            if (questionsScrollView?.content == null)
            {
                return;
            }

            for (var i = questionsScrollView.content.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(questionsScrollView.content.GetChild(i).gameObject);
            }

            questEntries.Clear();
        }

        private void AddQuestEntry(QuestProgress questProgress)
        {
            string description = FormatQuestState(questProgress);
            QuestShortInfo questShortInfo = resolver.Instantiate(uiConfig.QuestShortInfo, questionsScrollView.content);
            questShortInfo.name = $"{uiConfig.QuestShortInfo.name} | Quest State";

            if (questShortInfo.Description != null)
            {
                questShortInfo.Description.text = description;
            }

            if (questShortInfo.Button != null)
            {
                questShortInfo.Button.interactable = questProgress?.CurrentNode?.MapTarget != null;
                questShortInfo.Button.onClick.AddListener(() => FocusQuestTarget(questProgress));
            }

            questEntries.Add(questShortInfo);
        }

        private void AddQuestMarker(QuestProgress questProgress)
        {
            if (questProgress?.CurrentNode == null)
            {
                return;
            }

            Transform mapTarget = questProgress.CurrentNode.MapTarget;
            if (mapTarget == null)
            {
                return;
            }

            questMarkers.Add(new MapQuestMarkerData(mapTarget, questProgress));
        }

        private void FocusQuestTarget(QuestProgress questProgress)
        {
            if (questProgress?.CurrentNode == null || mapScrollController == null)
            {
                return;
            }

            Transform mapTarget = questProgress.CurrentNode.MapTarget;
            if (mapTarget == null)
            {
                return;
            }

            mapScrollController.FocusOnTarget(mapTarget);
        }

        private void HandleQuestPopup()
        {
            if (contentRect == null || mapScrollController == null)
            {
                ResetHoverPopupState();
                ClosePopup();
                return;
            }

            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                ResetHoverPopupState();
                ClosePopup();
                return;
            }

            Vector2 screenPoint = pointer.position.ReadValue();
            if (!mapScrollController.TryGetQuestMarkerAtScreenPoint(screenPoint, out QuestProgress target))
            {
                ResetHoverPopupState();
                ClosePopup();
                return;
            }

            bool targetChanged = !IsSamePopupTarget(hoverPopupTarget, target);
            if (targetChanged)
            {
                hoverPopupTarget = target;
                hoverPopupElapsed = 0f;
            }

            hoverPopupElapsed += Time.deltaTime;

            if (popupRect != null)
            {
                if (targetChanged)
                {
                    ClosePopup();
                    TryOpenQuestPopup(target, screenPoint);
                    return;
                }

                PageUiUtilities.UpdatePopupPosition(popupRect, contentRect, GetEventCamera(), screenPoint);
                return;
            }

            if (hoverPopupElapsed < uiConfig.PopupHoverOpenDelaySeconds)
            {
                return;
            }

            TryOpenQuestPopup(target, screenPoint);
        }

        private void ResetHoverPopupState()
        {
            hoverPopupTarget = null;
            hoverPopupElapsed = 0f;
        }

        private bool TryOpenQuestPopup(QuestProgress target, Vector2 screenPoint)
        {
            if (target == null || contentRect == null || uiConfig.QuestPopup == null)
            {
                return false;
            }

            popupRect = resolver.Instantiate(uiConfig.QuestPopup, contentRect);
            popupRect.name = $"{uiConfig.QuestPopup.name} | Quest Popup";
            PageUiUtilities.SetPopupRaycastState(popupRect, false);

            FillQuestPopup(popupRect, target);
            PageUiUtilities.RecalculatePopupSize(popupRect);
            PageUiUtilities.UpdatePopupPosition(popupRect, contentRect, GetEventCamera(), screenPoint);
            return true;
        }

        private void FillQuestPopup(RectTransform targetPopupRect, QuestProgress questProgress)
        {
            if (targetPopupRect == null || questProgress == null)
            {
                return;
            }

            TMP_Text titleText = null;
            TMP_Text descriptionText = null;
            Image iconImage = null;

            TMP_Text[] texts = targetPopupRect.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                if (text.gameObject.name == "Text (TMP)")
                {
                    titleText = text;
                }
                else if (text.gameObject.name == "Text Description (TMP)")
                {
                    descriptionText = text;
                }
            }

            Image[] images = targetPopupRect.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && image.transform != targetPopupRect && image.gameObject.name == "Image")
                {
                    iconImage = image;
                    break;
                }
            }

            if (titleText != null)
            {
                titleText.text = FormatQuestState(questProgress);
            }

            if (descriptionText != null)
            {
                descriptionText.text = GetQuestDescription(questProgress);
            }

            if (iconImage != null)
            {
                iconImage.sprite = questProgress.CurrentNode != null ? questProgress.CurrentNode.Icon : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(targetPopupRect);

            if (descriptionText != null && descriptionText.transform is RectTransform descriptionRect)
            {
                float descriptionWidth = descriptionRect.rect.width > 0f
                    ? descriptionRect.rect.width
                    : Mathf.Max(240f, targetPopupRect.rect.width - 32f);
                descriptionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, descriptionWidth);
                Vector2 preferredDescription = descriptionText.GetPreferredValues(descriptionText.text, descriptionWidth, 0f);
                descriptionRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    Mathf.Max(preferredDescription.y, descriptionText.fontSize + 8f));
            }

            if (titleText != null && titleText.transform.parent is RectTransform titleContainerRect)
            {
                float preferredTitleHeight = titleText.GetPreferredValues(titleText.text, titleText.rectTransform.rect.width, 0f).y;
                float minTitleHeight = 110f;
                titleContainerRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    Mathf.Max(minTitleHeight, preferredTitleHeight + 24f));
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(targetPopupRect);
        }

        private static bool IsSamePopupTarget(QuestProgress first, QuestProgress second)
        {
            return ReferenceEquals(first, second);
        }

        private Camera GetEventCamera()
        {
            return canvasRect != null
                ? canvasRect.GetComponentInParent<Canvas>()?.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvasRect.GetComponentInParent<Canvas>()?.worldCamera
                : null;
        }

        private void ClosePopup()
        {
            if (popupRect == null)
            {
                return;
            }

            Object.Destroy(popupRect.gameObject);
            popupRect = null;
        }

        private void RefreshQuestScrollContent()
        {
            if (questionsScrollView?.content == null)
            {
                return;
            }

            RectTransform content = questionsScrollView.content;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            if (questEntries.Count == 0)
            {
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                return;
            }

            var totalHeight = 0f;
            var layoutGroup = content.GetComponent<VerticalLayoutGroup>();

            if (layoutGroup != null)
            {
                totalHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
                totalHeight += Mathf.Max(0, content.childCount - 1) * layoutGroup.spacing;
            }

            for (var i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i) is not RectTransform child)
                {
                    continue;
                }

                var preferredHeight = LayoutUtility.GetPreferredHeight(child);
                totalHeight += preferredHeight > 0f ? preferredHeight : child.rect.height;
            }

            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void ToggleQuestScrollVisibility()
        {
            isQuestScrollVisible = !isQuestScrollVisible;
            ApplyQuestScrollVisibility();
        }

        private void ApplyQuestScrollVisibility()
        {
            if (questionsScrollView == null)
            {
                return;
            }

            questionsScrollView.gameObject.SetActive(isQuestScrollVisible);
            if (isQuestScrollVisible)
            {
                questionsScrollView.verticalNormalizedPosition = 1f;
            }
        }

        private static string FormatQuestState(QuestProgress questProgress)
        {
            string questTitle = NormalizeSingleLine(GetQuestTitle(questProgress));
            string nodeTitle = NormalizeSingleLine(GetNodeTitle(questProgress));

            if (string.IsNullOrWhiteSpace(nodeTitle))
            {
                return $"\u00AB{questTitle}\u00BB";
            }

            return $"\u00AB{questTitle}\u00BB: {nodeTitle}";
        }

        private static string GetQuestTitle(QuestProgress questProgress)
        {
            if (questProgress?.QuestGraph == null)
            {
                return string.Empty;
            }

            string localizedTitle = questProgress.QuestGraph.Title.GetLocalizedStringCached();
            return string.IsNullOrWhiteSpace(localizedTitle)
                ? questProgress.QuestGraph.name
                : localizedTitle;
        }

        private static string GetNodeTitle(QuestProgress questProgress)
        {
            if (questProgress?.CurrentNode == null)
            {
                return string.Empty;
            }

            string localizedTitle = questProgress.CurrentNode.Name.GetLocalizedStringCached();
            return string.IsNullOrWhiteSpace(localizedTitle)
                ? questProgress.CurrentNode.EditorTitle
                : localizedTitle;
        }

        private static string GetQuestDescription(QuestProgress questProgress)
        {
            if (questProgress?.QuestGraph == null)
            {
                return string.Empty;
            }

            string localizedDescription = questProgress.QuestGraph.Description.GetLocalizedStringCached();
            return string.IsNullOrWhiteSpace(localizedDescription)
                ? string.Empty
                : localizedDescription.Trim();
        }

        private static string NormalizeSingleLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }
    }
}
