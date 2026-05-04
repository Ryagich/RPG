using System.Collections.Generic;
using GameModes;
using Localization;
using MessagePipe;
using Messages;
using Quests;
using Stats;
using UI.Configs;
using UI.Map;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace UI.Pages
{
    public class MapPage : BasePage
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

        public override void Hide()
        {
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

            questMarkers.Add(new MapQuestMarkerData(mapTarget, questProgress.CurrentNode.Icon));
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
