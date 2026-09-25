using Dialogue;
using Dialogs.Graph;
using Dialogs.Graph.Model;
using GameModes;
using Inventory.Inventories;
using Input;
using Localization;
using MessagePipe;
using Messages;
using Money;
using Quests;
using Stats;
using TMPro;
using UI;
using UI.Configs;
using UI.UIElements;
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
        private readonly PlayerStatsHud playerStatsHud;
        private readonly PlayerStatsHudContinuity playerStatsHudContinuity;
        private readonly DialogueContext dialogueContext;
        private readonly DialogueAnswerProvider dialogueAnswerProvider;
        private readonly DialogueAnswerExecutionService dialogueAnswerExecutionService;
        private readonly CharacterInfo playerCharacterInfo;
        private readonly PlayerInventory playerInventory;
        private readonly QuestController questController;
        private readonly LocalizationConfig localizationConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly IPublisher<DialogueExitRequestedMessage> dialogueExitRequestedPublisher;
        private readonly IPublisher<DialogueGameplayEventRaisedMessage> dialogueGameplayEventPublisher;

        private RectTransform contentRect;
        private DialogueContainer dialogueContainer;
        private StatsHolder dialogueStatsHolder;
        private Image bloodScreen;
        private HeartbeatPulse heartbeatPulse;
        private BloodScreenController bloodScreenController;
        private int gameplayEventPublicationDepth;
        private bool isQuestChangeSubscribed;

        public DialoguePage(
            UIConfig uiConfig,
            StatsConfig statsConfig,
            StatsController statsController,
            StatFillers statFillers,
            global::Inventory.InventoryConfig inventoryConfig,
            InputConfig inputConfig,
            PlayerStatsHudContinuity playerStatsHudContinuity,
            DialogueContext dialogueContext,
            DialogueAnswerProvider dialogueAnswerProvider,
            DialogueAnswerExecutionService dialogueAnswerExecutionService,
            CharacterInfo playerCharacterInfo,
            PlayerInventory playerInventory,
            QuestController questController,
            LocalizationConfig localizationConfig,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
            IPublisher<DialogueExitRequestedMessage> dialogueExitRequestedPublisher,
            IPublisher<DialogueGameplayEventRaisedMessage> dialogueGameplayEventPublisher,
            ISubscriber<ShowStatsInputMessage> showStatsInputSubscriber)
        {
            this.uiConfig = uiConfig;
            this.statsConfig = statsConfig;
            this.statsController = statsController;
            hpFiller = statFillers.Get(StatType.Hp);
            playerStatsHud = new PlayerStatsHud(
                statsConfig,
                statsController,
                statFillers,
                inventoryConfig,
                playerInventory,
                inputConfig,
                uiConfig.FastSlotLabelFont,
                showStatsInputSubscriber);
            this.playerStatsHudContinuity = playerStatsHudContinuity;
            this.dialogueContext = dialogueContext;
            this.dialogueAnswerProvider = dialogueAnswerProvider;
            this.dialogueAnswerExecutionService = dialogueAnswerExecutionService;
            this.playerCharacterInfo = playerCharacterInfo;
            this.playerInventory = playerInventory;
            this.questController = questController;
            this.localizationConfig = localizationConfig;
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

            SubscribeToQuestChanges();

            contentRect = resolver.Instantiate(uiConfig.ContentPref, canvasRect);
            contentRect.name = $"{uiConfig.ContentPref.name} | {Type}";

            dialogueContainer = resolver.Instantiate(uiConfig.DialogueContainer, contentRect);
            dialogueContainer.TradeButton.onClick.AddListener(OpenTradePage);
            CreateDialogueStatsHud();
            bloodScreen = PageUiUtilities.CreateBloodScreen(uiConfig, resolver, contentRect, Type);
            heartbeatPulse = new HeartbeatPulse(statsConfig, statsController.Hp, hpFiller);
            bloodScreenController = new BloodScreenController(statsConfig, statsController.Hp, hpFiller, heartbeatPulse, bloodScreen);

            OpenEntryPhrase();
        }

        public override void Hide()
        {
            UnsubscribeFromQuestChanges();
            playerStatsHud.Detach();
            dialogueStatsHolder = null;

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

        private void CreateDialogueStatsHud()
        {
            if (uiConfig.StatsHolder == null)
            {
                return;
            }

            dialogueStatsHolder = resolver.Instantiate(uiConfig.StatsHolder, contentRect);
            dialogueStatsHolder.name = $"{uiConfig.StatsHolder.name} | {Type}";
            playerStatsHud.Attach(dialogueStatsHolder, playerStatsHudContinuity.Consume(Type));
        }

        public override void PrepareForTransition(PageType nextPageType)
        {
            if (nextPageType == PageType.MainGame)
            {
                playerStatsHudContinuity.Store(Type, playerStatsHud.CaptureState());
                return;
            }

            playerStatsHudContinuity.Clear();
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

            DialogueFlowTrace.PhraseChanged(
                null,
                null,
                entryPhrase,
                dialogueContext.CurrentPhraseText,
                dialogueContext.CanExitDialogue);

            AddPhrase(
                GetCharacterName(dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTarget?.name),
                dialogueContext.CurrentPhraseText);

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

            var visibleAnswers = dialogueAnswerProvider.GetVisibleAnswers(phrase);
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

        private void SelectAnswer(DialogueAnswerOption answer)
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

            if (!dialogueAnswerExecutionService.TryExecute(
                    answer.HasConditions,
                    answer.Conditions,
                    out var executionEffects))
            {
                DialogueFlowTrace.AnswerRejected(answer.Text, answer.Conditions);
                return;
            }

            AddPhrase(
                GetCharacterName(playerCharacterInfo, "Player"),
                answer.Text);

            AddNotifications(CreateExecutionNotifications(executionEffects, false));

            if (answer.NextPhrase != null)
            {
                dialogueContext.SetCurrentPhrase(answer.NextPhrase);
                AddPhrase(
                    GetCharacterName(dialogueContext.CurrentTargetCharacterInfo, dialogueContext.CurrentTarget?.name),
                    dialogueContext.CurrentPhraseText);
            }

            AddNotifications(CreateExecutionNotifications(executionEffects, true));

            PublishGameplayEvents(answer.GameplayEvents, $"answer:{dialogueContext.CurrentPhrase?.name}");

            if (answer.NextPhrase != null)
            {
                PublishGameplayEvents(answer.NextPhrase.GameplayEvents, $"next-phrase:{answer.NextPhrase.name}");
            }

            if (answer.ForceExitAfterAnswer)
            {
                dialogueExitRequestedPublisher.Publish(
                    new DialogueExitRequestedMessage(answer.ContinueForcedDialogueAfterExit));
            }

            if (answer.ForceExitAfterAnswer)
            {
                return;
            }

            ShowAnswers(answer.NextPhrase);
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
                    gameplayEventPublicationDepth++;
                    try
                    {
                        DialogueFlowTrace.GameplayEventPublished(gameplayEvent, source);
                        dialogueGameplayEventPublisher.Publish(new DialogueGameplayEventRaisedMessage(gameplayEvent));
                    }
                    finally
                    {
                        gameplayEventPublicationDepth--;
                    }
                }
            }
        }

        private void OnQuestChanged(QuestChangeInfo change)
        {
            if (gameplayEventPublicationDepth <= 0 || dialogueContainer == null)
            {
                return;
            }

            AddNotification(CreateQuestNotification(change.Type, change.Quest));
        }

        private void SubscribeToQuestChanges()
        {
            if (isQuestChangeSubscribed)
            {
                return;
            }

            questController.Changed += OnQuestChanged;
            isQuestChangeSubscribed = true;
        }

        private void UnsubscribeFromQuestChanges()
        {
            if (!isQuestChangeSubscribed)
            {
                return;
            }

            questController.Changed -= OnQuestChanged;
            isQuestChangeSubscribed = false;
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

        private System.Collections.Generic.List<DialogNotificationData> CreateExecutionNotifications(
            System.Collections.Generic.IReadOnlyList<DialogueAnswerExecutionEffect> effects,
            bool deferred)
        {
            var notifications = new System.Collections.Generic.List<DialogNotificationData>();
            if (effects == null)
            {
                return notifications;
            }

            foreach (DialogueAnswerExecutionEffect effect in effects)
            {
                if (effect.IsDeferred != deferred)
                {
                    continue;
                }

                switch (effect.Type)
                {
                    case DialogueAnswerExecutionEffectType.MoneyReceived:
                        notifications.Add(CreateMoneyNotification(localizationConfig.MoneyReceived.GetLocalizedStringCached(), effect.Amount));
                        break;
                    case DialogueAnswerExecutionEffectType.MoneyLost:
                        notifications.Add(CreateMoneyNotification(localizationConfig.MoneyLost.GetLocalizedStringCached(), effect.Amount));
                        break;
                    case DialogueAnswerExecutionEffectType.ItemLost:
                        notifications.Add(CreateItemNotification(localizationConfig.ItemLost.GetLocalizedStringCached(), effect.Item, effect.Amount));
                        break;
                    case DialogueAnswerExecutionEffectType.QuestChanged:
                        notifications.Add(CreateQuestNotification(ToQuestNotificationType(effect.QuestEffectType), effect.Quest));
                        break;
                }
            }

            return notifications;
        }

        private static QuestNotificationType ToQuestNotificationType(DialogueQuestEffectType effectType)
        {
            return effectType switch
            {
                DialogueQuestEffectType.Added => QuestNotificationType.New,
                DialogueQuestEffectType.Updated => QuestNotificationType.Update,
                DialogueQuestEffectType.Completed => QuestNotificationType.Completed,
                _ => QuestNotificationType.Update
            };
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

        private DialogNotificationData CreateQuestNotification(QuestChangeType changeType, Quests.Graph.QuestGraph questGraph)
        {
            QuestNotificationType notificationType = changeType switch
            {
                QuestChangeType.Added => QuestNotificationType.New,
                QuestChangeType.Updated => QuestNotificationType.Update,
                QuestChangeType.Completed => QuestNotificationType.Completed,
                QuestChangeType.Failed => QuestNotificationType.Failed,
                QuestChangeType.Removed => QuestNotificationType.Canceled,
                _ => QuestNotificationType.Update
            };

            return CreateQuestNotification(notificationType, questGraph);
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
