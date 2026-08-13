using Dialogue;
using Dialogs.Graph;
using Dialogs.Graph.Model;
using GameModes;
using Inventory.Inventories;
using Localization;
using MessagePipe;
using Messages;
using Money;
using Quests;
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
        private readonly PlayerInventory playerInventory;
        private readonly MoneyStorage playerMoneyStorage;
        private readonly QuestController questController;
        private readonly LocalizationConfig localizationConfig;
        private readonly DialogueRuntimeFlagRegistry runtimeFlags;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly IPublisher<DialogueExitRequestedMessage> dialogueExitRequestedPublisher;
        private readonly IPublisher<DialogueGameplayEventRaisedMessage> dialogueGameplayEventPublisher;

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
            PlayerInventory playerInventory,
            MoneyStorage playerMoneyStorage,
            QuestController questController,
            LocalizationConfig localizationConfig,
            DialogueRuntimeFlagRegistry runtimeFlags,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
            IPublisher<DialogueExitRequestedMessage> dialogueExitRequestedPublisher,
            IPublisher<DialogueGameplayEventRaisedMessage> dialogueGameplayEventPublisher)
        {
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            hpFiller = statFillers.Get(StatType.Hp);
            this.dialogueContext = dialogueContext;
            this.playerCharacterInfo = playerCharacterInfo;
            this.playerInventory = playerInventory;
            this.playerMoneyStorage = playerMoneyStorage;
            this.questController = questController;
            dialogueContext.SetPlayerQuestController(questController);
            this.localizationConfig = localizationConfig;
            this.runtimeFlags = runtimeFlags;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.dialogueExitRequestedPublisher = dialogueExitRequestedPublisher;
            this.dialogueGameplayEventPublisher = dialogueGameplayEventPublisher;

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

            var entryPhrase = dialogueContext.CurrentPhrase;
            if (entryPhrase == null)
            {
                return;
            }

            DialogueFlowTrace.PhraseChanged(null, entryPhrase, dialogueContext.CanExitDialogue);

            AddPhrase(
                GetCharacterName(dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTarget?.name),
                entryPhrase.Text.GetLocalizedStringCached());

            PublishGameplayEvents(entryPhrase.GameplayEvents, $"entry:{entryPhrase.name}");

            ShowAnswers(entryPhrase);
        }

        private void ShowAnswers(DialogPhrase phrase)
        {
            ClearContent(dialogueContainer.AnswerContent);

            if (phrase == null)
            {
                return;
            }

            var visibleAnswers = BuildVisibleAnswers(phrase);
            for (var i = 0; i < visibleAnswers.Count; i++)
            {
                var answer = visibleAnswers[i];
                var visibleAnswerIndex = i + 1;
                var answerButton = resolver.Instantiate(uiConfig.AnswerButton, dialogueContainer.AnswerContent);
                answerButton.name = $"{uiConfig.AnswerButton.name} | {visibleAnswerIndex}";

                var answerText = answerButton.GetComponentInChildren<TMP_Text>(true);
                if (answerText != null)
                {
                    answerText.text = $"{visibleAnswerIndex}. {answer.Text}";

                    var answerLayout = answerButton.GetComponent<LayoutElement>();
                    if (answerLayout == null)
                    {
                        answerLayout = answerButton.gameObject.AddComponent<LayoutElement>();
                    }

                    // The outer layout has not yet assigned a width to the newly created
                    // button. Calculate against the known final content width instead.
                    var contentWidth = dialogueContainer.AnswerContent.rect.width;
                    var requiredHeight = Mathf.Max(
                        answerText.GetPreferredValues(answerText.text, contentWidth, 0f).y + 8f,
                        40f);
                    answerLayout.preferredHeight = requiredHeight;
                    answerButton.GetComponent<RectTransform>()
                        .SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, requiredHeight);
                }

                answerButton.onClick.AddListener(() => SelectAnswer(answer));
            }

            RefreshContentLayout(dialogueContainer.AnswerContent);
            ScrollToTop(dialogueContainer.AnswerScroll);
        }

        private System.Collections.Generic.List<DisplayedAnswerData> BuildVisibleAnswers(DialogPhrase phrase)
        {
            var visibleAnswers = new System.Collections.Generic.List<DisplayedAnswerData>();

            if (phrase == null)
            {
                return visibleAnswers;
            }

            for (var i = 0; i < phrase.Answers.Count; i++)
            {
                var answer = phrase.Answers[i];
                if (answer == null)
                {
                    continue;
                }

                bool isAvailable = AreConditionsSatisfied(answer.HasConditions, answer.Conditions);
                DialogueFlowTrace.AnswerEvaluated(
                    phrase,
                    "phrase-answer",
                    answer.Text.GetLocalizedStringCached(),
                    answer.NextPhrase,
                    isAvailable,
                    answer.ForceExitAfterAnswer,
                    answer.Conditions);
                if (!isAvailable)
                {
                    continue;
                }

                visibleAnswers.Add(new DisplayedAnswerData(
                    answer.Text.GetLocalizedStringCached(),
                    answer.NextPhrase,
                    answer.ForceExitAfterAnswer,
                    answer.ContinueForcedDialogueAfterExit,
                    answer.GameplayEvents,
                    answer.HasConditions,
                    answer.Conditions));
            }

            var currentDialog = dialogueContext.CurrentDialog;
            if (currentDialog == null || !currentDialog.IsEntryPhrase(phrase))
            {
                return visibleAnswers;
            }

            foreach (var questPhrase in currentDialog.GetQuestPhrases())
            {
                var questAnswer = questPhrase?.QuestAnswer;
                bool isAvailable = questPhrase != null && questAnswer != null &&
                                   AreConditionsSatisfied(questAnswer.HasConditions, questAnswer.Conditions);
                if (questPhrase != null && questAnswer != null)
                {
                    DialogueFlowTrace.AnswerEvaluated(
                        phrase,
                        $"regular-quest-answer:{questPhrase.name}",
                        questAnswer.Text.GetLocalizedStringCached(),
                        questPhrase,
                        isAvailable,
                        false,
                        questAnswer.Conditions);
                }

                if (questPhrase == null ||
                    questAnswer == null ||
                    visibleAnswers.Exists(visibleAnswer => visibleAnswer.NextPhrase == questPhrase) ||
                    !isAvailable)
                {
                    continue;
                }

                visibleAnswers.Add(new DisplayedAnswerData(
                    questAnswer.Text.GetLocalizedStringCached(),
                    questPhrase,
                    false,
                    true,
                    questAnswer.GameplayEvents,
                    questAnswer.HasConditions,
                    questAnswer.Conditions));
            }

            // The farewell is a navigation action owned by the dialogue UI. It is shown
            // only at the root of a regular conversation, never inside a dialogue branch
            // or a forced conversation whose exit has not been explicitly restored.
            if (currentDialog.IsEntryPhrase(phrase) && dialogueContext.CanExitDialogue)
            {
                visibleAnswers.Add(new DisplayedAnswerData(
                    localizationConfig.DialogueFarewell.GetLocalizedStringCached(),
                    null,
                    true,
                    true,
                    null,
                    false,
                    null));
            }

            return visibleAnswers;
        }

        private void SelectAnswer(DisplayedAnswerData answer)
        {
            if (string.IsNullOrWhiteSpace(answer.Text) && answer.NextPhrase == null && !answer.ForceExitAfterAnswer)
            {
                return;
            }

            DialogueFlowTrace.AnswerSelected(
                dialogueContext.CurrentPhrase,
                answer.Text,
                answer.NextPhrase,
                answer.ForceExitAfterAnswer,
                answer.ContinueForcedDialogueAfterExit,
                answer.Conditions);

            if (!TryExecuteConditions(
                    answer.HasConditions,
                    answer.Conditions,
                    out var immediateNotifications,
                    out var deferredNotifications))
            {
                DialogueFlowTrace.AnswerRejected(answer.Text, answer.Conditions);
                return;
            }

            AddPhrase(
                GetCharacterName(playerCharacterInfo, "Player"),
                answer.Text);

            AddNotifications(immediateNotifications);

            if (answer.NextPhrase != null)
            {
                dialogueContext.SetCurrentPhrase(answer.NextPhrase);
                AddPhrase(
                    GetCharacterName(dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTarget?.name),
                    answer.NextPhrase.Text.GetLocalizedStringCached());
            }

            AddNotifications(deferredNotifications);

            if (answer.ForceExitAfterAnswer)
            {
                dialogueExitRequestedPublisher.Publish(
                    new DialogueExitRequestedMessage(answer.ContinueForcedDialogueAfterExit));
            }

            PublishGameplayEvents(answer.GameplayEvents, $"answer:{dialogueContext.CurrentPhrase?.name}");

            if (answer.NextPhrase != null)
            {
                PublishGameplayEvents(answer.NextPhrase.GameplayEvents, $"next-phrase:{answer.NextPhrase.name}");
            }

            if (answer.ForceExitAfterAnswer)
            {
                return;
            }

            ShowAnswers(answer.NextPhrase);
        }

        private bool AreConditionsSatisfied(bool hasConditions, System.Collections.Generic.IReadOnlyList<DialogAnswerCondition> conditions)
        {
            return DialogueAnswerAvailability.AreConditionsSatisfied(
                hasConditions,
                conditions,
                playerInventory,
                playerMoneyStorage,
                questController,
                runtimeFlags);
        }

        private void PublishGameplayEvents(
            System.Collections.Generic.IReadOnlyList<DialogueGameplayEvent> events,
            string source)
        {
            if (events == null)
            {
                return;
            }

            foreach (var gameplayEvent in events)
            {
                if (gameplayEvent != null)
                {
                    DialogueFlowTrace.GameplayEventPublished(gameplayEvent, source);
                    dialogueGameplayEventPublisher.Publish(new DialogueGameplayEventRaisedMessage(gameplayEvent));
                }
            }
        }

        private bool TryExecuteConditions(
            bool hasConditions,
            System.Collections.Generic.IReadOnlyList<DialogAnswerCondition> conditions,
            out System.Collections.Generic.List<DialogNotificationData> immediateNotifications,
            out System.Collections.Generic.List<DialogNotificationData> deferredNotifications)
        {
            immediateNotifications = new System.Collections.Generic.List<DialogNotificationData>();
            deferredNotifications = new System.Collections.Generic.List<DialogNotificationData>();

            if (!hasConditions || conditions == null)
            {
                return true;
            }

            foreach (var condition in conditions)
            {
                if (condition == null)
                {
                    continue;
                }

                switch (condition.Type)
                {
                    case DialogAnswerConditionType.GiveMoney:
                    {
                        var amount = Mathf.Abs(condition.MoneyAmount);
                        playerMoneyStorage.Add(amount);
                        deferredNotifications.Add(CreateMoneyNotification(localizationConfig.MoneyReceived.GetLocalizedStringCached(), amount));
                        DialogueFlowTrace.ConditionApplied(condition);
                        break;
                    }
                    case DialogAnswerConditionType.TakeMoney:
                    {
                        var amount = Mathf.Abs(condition.MoneyAmount);
                        if (!playerMoneyStorage.TrySpend(amount))
                        {
                            return false;
                        }

                        immediateNotifications.Add(CreateMoneyNotification(localizationConfig.MoneyLost.GetLocalizedStringCached(), amount));
                        DialogueFlowTrace.ConditionApplied(condition);
                        break;
                    }
                    case DialogAnswerConditionType.TakeMoneyMax:
                    {
                        var amount = playerMoneyStorage.SpendUpTo(Mathf.Abs(condition.MoneyAmount));
                        immediateNotifications.Add(CreateMoneyNotification(localizationConfig.MoneyLost.GetLocalizedStringCached(), amount));
                        DialogueFlowTrace.ConditionApplied(condition);
                        break;
                    }
                    case DialogAnswerConditionType.TakeItemIfHas:
                    {
                        var itemCount = Mathf.Abs(condition.ItemCount);
                        if (!playerInventory.TryConsumeItemCount(condition.ItemConfig, itemCount))
                        {
                            return false;
                        }

                        immediateNotifications.Add(CreateItemNotification(localizationConfig.ItemLost.GetLocalizedStringCached(), condition.ItemConfig, itemCount));
                        DialogueFlowTrace.ConditionApplied(condition);
                        break;
                    }
                    case DialogAnswerConditionType.CheckQuestStep:
                        if (!questController.CanExecuteTransition(condition.QuestGraph, condition.QuestTransition))
                        {
                            return false;
                        }

                        DialogueFlowTrace.ConditionApplied(condition);
                        break;
                    case DialogAnswerConditionType.AddQuest:
                        if (questController.TryAddQuest(condition.QuestGraph))
                        {
                            deferredNotifications.Add(CreateQuestNotification(QuestNotificationType.New, condition.QuestGraph));
                        }

                        DialogueFlowTrace.ConditionApplied(condition);
                        break;
                    case DialogAnswerConditionType.DoQuestStep:
                        if (!questController.TryExecuteTransition(condition.QuestGraph, condition.QuestTransition))
                        {
                            return false;
                        }

                        deferredNotifications.Add(CreateQuestNotification(QuestNotificationType.Update, condition.QuestGraph));
                        DialogueFlowTrace.ConditionApplied(condition);
                        break;
                    case DialogAnswerConditionType.DoQuestEnd:
                        if (!questController.TryCompleteNode(condition.QuestGraph, condition.QuestNode))
                        {
                            return false;
                        }

                        deferredNotifications.Add(CreateQuestNotification(QuestNotificationType.Completed, condition.QuestGraph));
                        DialogueFlowTrace.ConditionApplied(condition);
                        break;
                    case DialogAnswerConditionType.ClearRuntimeFlag:
                        runtimeFlags?.Deactivate(condition.RuntimeFlag);
                        DialogueFlowTrace.ConditionApplied(condition);
                        break;
                    case DialogAnswerConditionType.SetRuntimeFlag:
                        runtimeFlags?.Activate(condition.RuntimeFlag);
                        DialogueFlowTrace.ConditionApplied(condition);
                        break;
                }
            }

            return true;
        }

        private void AddPhrase(string speakerName, string phraseText)
        {
            var phraseContainer = resolver.Instantiate(uiConfig.PhraseContainer, dialogueContainer.DialogueContent);
            phraseContainer.name = $"{uiConfig.PhraseContainer.name} | {speakerName}";
            phraseContainer.SetContent(speakerName, phraseText);

            RefreshContentLayout(dialogueContainer.DialogueContent);
            ScrollToBottom(dialogueContainer.DialogueScroll);
        }

        private void AddNotifications(System.Collections.Generic.IReadOnlyList<DialogNotificationData> notifications)
        {
            if (notifications == null)
            {
                return;
            }

            foreach (var notification in notifications)
            {
                AddNotification(notification);
            }
        }

        private void AddNotification(DialogNotificationData notification)
        {
            if (uiConfig.NotificationInDialog == null)
            {
                return;
            }

            var notificationInDialog = resolver.Instantiate(uiConfig.NotificationInDialog, dialogueContainer.DialogueContent);
            notificationInDialog.name = $"{uiConfig.NotificationInDialog.name} | {notification.Name}";

            if (notificationInDialog.Name != null)
            {
                notificationInDialog.Name.text = notification.Name;
            }

            if (notificationInDialog.Phrase != null)
            {
                notificationInDialog.Phrase.text = notification.Description;
            }

            if (notificationInDialog.Icon != null)
            {
                notificationInDialog.Icon.sprite = notification.Icon;
                notificationInDialog.Icon.enabled = notification.Icon != null;
            }

            RefreshContentLayout(dialogueContainer.DialogueContent);
            ScrollToBottom(dialogueContainer.DialogueScroll);
        }

        private DialogNotificationData CreateMoneyNotification(string title, int amount)
        {
            return new DialogNotificationData(title, amount.ToString());
        }

        private DialogNotificationData CreateItemNotification(string title, global::Inventory.Item.ItemConfig itemConfig, int itemCount)
        {
            string itemName = itemConfig != null
                ? itemConfig.Name.GetLocalizedStringCached()
                : string.Empty;
            string description = itemCount > 1
                ? $"{itemName} x{itemCount}"
                : itemName;
            return new DialogNotificationData(title, description);
        }

        private DialogNotificationData CreateQuestNotification(QuestNotificationType notificationType, Quests.Graph.QuestGraph questGraph)
        {
            string title = notificationType switch
            {
                QuestNotificationType.New => localizationConfig.QuestNew.GetLocalizedStringCached(),
                QuestNotificationType.Update => localizationConfig.QuestUpdate.GetLocalizedStringCached(),
                QuestNotificationType.Completed => localizationConfig.QuestCompleted.GetLocalizedStringCached(),
                QuestNotificationType.Failed => localizationConfig.QuestFailed.GetLocalizedStringCached(),
                QuestNotificationType.Canceled => localizationConfig.QuestCanceled.GetLocalizedStringCached(),
                _ => string.Empty
            };

            string questName = GetQuestDisplayName(questGraph);
            return new DialogNotificationData(title, $"{title}: {questName}", questController.GetQuestSprite(questGraph));
        }

        private static string GetQuestDisplayName(Quests.Graph.QuestGraph questGraph)
        {
            if (questGraph == null)
            {
                return string.Empty;
            }

            string localizedTitle = questGraph.Title.GetLocalizedStringCached();
            if (!string.IsNullOrWhiteSpace(localizedTitle))
            {
                return localizedTitle;
            }

            return questGraph.name;
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

        private readonly struct DialogNotificationData
        {
            public readonly string Name;
            public readonly string Description;
            public readonly Sprite Icon;

            public DialogNotificationData(string name, string description, Sprite icon = null)
            {
                Name = name ?? string.Empty;
                Description = description ?? string.Empty;
                Icon = icon;
            }
        }

        private readonly struct DisplayedAnswerData
        {
            public readonly string Text;
            public readonly DialogPhrase NextPhrase;
            public readonly bool ForceExitAfterAnswer;
            public readonly bool ContinueForcedDialogueAfterExit;
            public readonly System.Collections.Generic.IReadOnlyList<DialogueGameplayEvent> GameplayEvents;
            public readonly bool HasConditions;
            public readonly System.Collections.Generic.IReadOnlyList<DialogAnswerCondition> Conditions;

            public DisplayedAnswerData(
                string text,
                DialogPhrase nextPhrase,
                bool forceExitAfterAnswer,
                bool continueForcedDialogueAfterExit,
                System.Collections.Generic.IReadOnlyList<DialogueGameplayEvent> gameplayEvents,
                bool hasConditions,
                System.Collections.Generic.IReadOnlyList<DialogAnswerCondition> conditions)
            {
                Text = text ?? string.Empty;
                NextPhrase = nextPhrase;
                ForceExitAfterAnswer = forceExitAfterAnswer && nextPhrase == null;
                ContinueForcedDialogueAfterExit = continueForcedDialogueAfterExit;
                GameplayEvents = gameplayEvents;
                HasConditions = hasConditions;
                Conditions = conditions;
            }
        }

        private enum QuestNotificationType
        {
            New = 0,
            Update = 1,
            Completed = 2,
            Failed = 3,
            Canceled = 4
        }
    }
}
