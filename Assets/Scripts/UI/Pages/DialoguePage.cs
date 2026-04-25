using Dialogue;
using Dialogs.Graph.Model;
using GameModes;
using Localization;
using MessagePipe;
using Messages;
using Stats;
using TMPro;
using UI;
using UI.Configs;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using CharacterInfo = Character.CharacterInfo;

namespace UI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class DialoguePage : BasePage
    {
        public override PageType Type { get; } = PageType.Dialogue;

        private readonly UIConfig uiConfig;
        private readonly StatsConfig statsConfig;
        private readonly StatsController statsController;
        private readonly StatFiller hpFiller;
        private readonly DialogueContext dialogueContext;
        private readonly CharacterInfo playerCharacterInfo;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        private RectTransform contentRect;
        private DialogueContainer dialogueContainer;
        private Image bloodScreen;
        private HeartbeatPulse heartbeatPulse;
        private BloodScreenController bloodScreenController;

        public DialoguePage(
            UIConfig uiConfig,
            StatsConfig statsConfig,
            StatsController statsController,
            StatFillers statFillers,
            DialogueContext dialogueContext,
            CharacterInfo playerCharacterInfo,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher)
        {
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            hpFiller = statFillers.Get(StatType.Hp);
            this.dialogueContext = dialogueContext;
            this.playerCharacterInfo = playerCharacterInfo;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            if (dialogueContext.CurrentTarget == null)
            {
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                return;
            }

            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            dialogueContainer = resolver.Instantiate(uiConfig.DialogueContainer, contentRect);
            dialogueContainer.TradeButton.onClick.AddListener(OpenTradePage);
            bloodScreen = PageUiUtilities.CreateBloodScreen(uiConfig, resolver, contentRect, Type);
            heartbeatPulse = new HeartbeatPulse(statsConfig, statsController.Hp, hpFiller);
            bloodScreenController = new BloodScreenController(statsConfig, statsController.Hp, hpFiller, heartbeatPulse, bloodScreen);

            OpenEntryPhrase();
        }

        public override void Hide()
        {
            bloodScreenController?.Dispose();
            bloodScreenController = null;

            heartbeatPulse?.Dispose();
            heartbeatPulse = null;

            if (dialogueContainer)
            {
                dialogueContainer.TradeButton.onClick.RemoveListener(OpenTradePage);
                dialogueContainer = null;
            }

            bloodScreen = null;

            if (contentRect)
            {
                Object.Destroy(contentRect.gameObject);
            }
        }

        private void OpenTradePage()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Trade));
        }

        private void OpenEntryPhrase()
        {
            ClearContent(dialogueContainer.DialogueContent);
            ClearContent(dialogueContainer.AnswerContent);

            var entryPhrase = dialogueContext.CurrentDialog?.EntryPhrase;
            if (entryPhrase == null)
            {
                return;
            }

            AddPhrase(
                GetCharacterName(dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTarget?.name),
                entryPhrase.Text.GetLocalizedStringCached());

            ShowAnswers(entryPhrase);
        }

        private void ShowAnswers(DialogPhrase phrase)
        {
            ClearContent(dialogueContainer.AnswerContent);

            if (phrase == null)
            {
                return;
            }

            for (var i = 0; i < phrase.Answers.Count; i++)
            {
                var answer = phrase.Answers[i];
                if (answer == null)
                {
                    continue;
                }

                var answerButton = resolver.Instantiate(uiConfig.AnswerButton, dialogueContainer.AnswerContent);
                answerButton.name = $"{uiConfig.AnswerButton.name} | {i + 1}";

                var answerText = answerButton.GetComponentInChildren<TMP_Text>(true);
                if (answerText != null)
                {
                    answerText.text = $"{i + 1}. {answer.Text.GetLocalizedStringCached()}";
                }

                answerButton.onClick.AddListener(() => SelectAnswer(answer));
            }

            RefreshContentLayout(dialogueContainer.AnswerContent);
            ScrollToTop(dialogueContainer.AnswerScroll);
        }

        private void SelectAnswer(DialogAnswer answer)
        {
            if (answer == null)
            {
                return;
            }

            AddPhrase(
                GetCharacterName(playerCharacterInfo, "Player"),
                answer.Text.GetLocalizedStringCached());

            if (answer.NextPhrase != null)
            {
                AddPhrase(
                    GetCharacterName(dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTarget?.name),
                    answer.NextPhrase.Text.GetLocalizedStringCached());
            }

            ShowAnswers(answer.NextPhrase);
        }

        private void AddPhrase(string speakerName, string phraseText)
        {
            var phraseContainer = resolver.Instantiate(uiConfig.PhraseContainer, dialogueContainer.DialogueContent);
            phraseContainer.name = $"{uiConfig.PhraseContainer.name} | {speakerName}";
            phraseContainer.SetContent(speakerName, phraseText);

            RefreshContentLayout(dialogueContainer.DialogueContent);
            ScrollToBottom(dialogueContainer.DialogueScroll);
        }

        private static string GetCharacterName(CharacterInfo characterInfo, string fallbackName)
        {
            if (characterInfo != null)
            {
                return characterInfo.Name.GetLocalizedStringCached();
            }

            return fallbackName ?? string.Empty;
        }

        private static void ClearContent(RectTransform content)
        {
            for (var i = content.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(content.GetChild(i).gameObject);
            }
        }

        private static void RefreshContentLayout(RectTransform content)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

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

        private static void ScrollToBottom(ScrollRect scrollRect)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private static void ScrollToTop(ScrollRect scrollRect)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }
}
