using Dialogue;
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
            PlayerInventory playerInventory,
            MoneyStorage playerMoneyStorage,
            QuestController questController,
            LocalizationConfig localizationConfig,
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
            this.playerInventory = playerInventory;
            this.playerMoneyStorage = playerMoneyStorage;
            this.questController = questController;
            this.localizationConfig = localizationConfig;
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
                if (answer == null || !AreConditionsSatisfied(answer.HasConditions, answer.Conditions))
                {
                    continue;
                }

                visibleAnswers.Add(new DisplayedAnswerData(
                    answer.Text.GetLocalizedStringCached(),
                    answer.NextPhrase,
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
                if (questPhrase == null ||
                    questAnswer == null ||
                    visibleAnswers.Exists(visibleAnswer => visibleAnswer.NextPhrase == questPhrase) ||
                    !AreConditionsSatisfied(questAnswer.HasConditions, questAnswer.Conditions))
                {
                    continue;
                }

                visibleAnswers.Add(new DisplayedAnswerData(
                    questAnswer.Text.GetLocalizedStringCached(),
                    questPhrase,
                    questAnswer.HasConditions,
                    questAnswer.Conditions));
            }

            return visibleAnswers;
        }

        private void SelectAnswer(DisplayedAnswerData answer)
        {
            if (string.IsNullOrWhiteSpace(answer.Text) && answer.NextPhrase == null)
            {
                return;
            }

            if (!TryExecuteConditions(
                    answer.HasConditions,
                    answer.Conditions,
                    out var immediateNotifications,
                    out var deferredNotifications))
            {
                return;
            }

            AddPhrase(
                GetCharacterName(playerCharacterInfo, "Player"),
                answer.Text);

            AddNotifications(immediateNotifications);

            if (answer.NextPhrase != null)
            {
                AddPhrase(
                    GetCharacterName(dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTarget?.name),
                    answer.NextPhrase.Text.GetLocalizedStringCached());
            }

            AddNotifications(deferredNotifications);

            ShowAnswers(answer.NextPhrase);
        }

        private bool AreConditionsSatisfied(bool hasConditions, System.Collections.Generic.IReadOnlyList<DialogAnswerCondition> conditions)
        {
            if (!hasConditions || conditions == null)
            {
                return true;
            }

            var simulatedMoney = playerMoneyStorage.CurrentMoney.Value;
            var simulatedItems = new System.Collections.Generic.Dictionary<global::Inventory.Item.ItemConfig, int>();

            foreach (var condition in conditions)
            {
                if (condition == null)
                {
                    continue;
                }

                switch (condition.Type)
                {
                    case DialogAnswerConditionType.GiveMoney:
                        simulatedMoney += Mathf.Abs(condition.MoneyAmount);
                        break;
                    case DialogAnswerConditionType.TakeMoney:
                    {
                        var moneyAmount = Mathf.Abs(condition.MoneyAmount);
                        if (simulatedMoney < moneyAmount)
                        {
                            return false;
                        }

                        simulatedMoney -= moneyAmount;
                        break;
                    }
                    case DialogAnswerConditionType.TakeMoneyMax:
                        simulatedMoney = Mathf.Max(0, simulatedMoney - Mathf.Abs(condition.MoneyAmount));
                        break;
                    case DialogAnswerConditionType.TakeItemIfHas:
                    {
                        if (condition.ItemConfig == null)
                        {
                            return false;
                        }

                        var requiredCount = Mathf.Abs(condition.ItemCount);
                        if (!simulatedItems.TryGetValue(condition.ItemConfig, out var currentCount))
                        {
                            currentCount = playerInventory.GetInventoryItemCount(condition.ItemConfig);
                        }

                        if (currentCount < requiredCount)
                        {
                            return false;
                        }

                        simulatedItems[condition.ItemConfig] = currentCount - requiredCount;
                        break;
                    }
                    case DialogAnswerConditionType.CheckQuestStep:
                    case DialogAnswerConditionType.DoQuestStep:
                        if (!questController.CanExecuteTransition(condition.QuestGraph, condition.QuestTransition))
                        {
                            return false;
                        }

                        break;
                    case DialogAnswerConditionType.AddQuest:
                        break;
                    case DialogAnswerConditionType.DoQuestEnd:
                        if (!questController.CanCompleteNode(condition.QuestGraph, condition.QuestNode))
                        {
                            return false;
                        }

                        break;
                }
            }

            return true;
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
                        break;
                    }
                    case DialogAnswerConditionType.TakeMoneyMax:
                    {
                        var amount = playerMoneyStorage.SpendUpTo(Mathf.Abs(condition.MoneyAmount));
                        immediateNotifications.Add(CreateMoneyNotification(localizationConfig.MoneyLost.GetLocalizedStringCached(), amount));
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
                        break;
                    }
                    case DialogAnswerConditionType.CheckQuestStep:
                        if (!questController.CanExecuteTransition(condition.QuestGraph, condition.QuestTransition))
                        {
                            return false;
                        }

                        break;
                    case DialogAnswerConditionType.AddQuest:
                        if (questController.TryAddQuest(condition.QuestGraph))
                        {
                            deferredNotifications.Add(CreateQuestNotification(QuestNotificationType.Update, condition.QuestGraph));
                        }

                        break;
                    case DialogAnswerConditionType.DoQuestStep:
                        if (!questController.TryExecuteTransition(condition.QuestGraph, condition.QuestTransition))
                        {
                            return false;
                        }

                        deferredNotifications.Add(CreateQuestNotification(QuestNotificationType.Update, condition.QuestGraph));
                        break;
                    case DialogAnswerConditionType.DoQuestEnd:
                        if (!questController.TryCompleteNode(condition.QuestGraph, condition.QuestNode))
                        {
                            return false;
                        }

                        deferredNotifications.Add(CreateQuestNotification(QuestNotificationType.Completed, condition.QuestGraph));
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
                QuestNotificationType.Completed => localizationConfig.QuestCompleted.GetLocalizedStringCached(),
                QuestNotificationType.Failed => localizationConfig.QuestFailed.GetLocalizedStringCached(),
                _ => localizationConfig.QuestUpdate.GetLocalizedStringCached()
            };

            string questName = GetQuestDisplayName(questGraph);
            return new DialogNotificationData(title, $"{title}: {questName}");
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

            public DialogNotificationData(string name, string description)
            {
                Name = name ?? string.Empty;
                Description = description ?? string.Empty;
            }
        }

        private readonly struct DisplayedAnswerData
        {
            public readonly string Text;
            public readonly DialogPhrase NextPhrase;
            public readonly bool HasConditions;
            public readonly System.Collections.Generic.IReadOnlyList<DialogAnswerCondition> Conditions;

            public DisplayedAnswerData(
                string text,
                DialogPhrase nextPhrase,
                bool hasConditions,
                System.Collections.Generic.IReadOnlyList<DialogAnswerCondition> conditions)
            {
                Text = text ?? string.Empty;
                NextPhrase = nextPhrase;
                HasConditions = hasConditions;
                Conditions = conditions;
            }
        }

        private enum QuestNotificationType
        {
            Update = 0,
            Completed = 1,
            Failed = 2
        }
    }
}
