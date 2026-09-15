using System;
using Dialogue;
using MessagePipe;
using Messages;
using Quests;
using Quests.Graph.Model;
using VContainer.Unity;

namespace Mill
{
    /// <summary>Owns dialogue-driven progression only; QuestController persists the state.</summary>
    public sealed class MillPropertyClaimQuestCoordinator : IStartable, IDisposable
    {
        private readonly MillPropertyClaimQuestConfig config;
        private readonly DialogueContext dialogueContext;
        private readonly ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents;
        private IDisposable subscription;

        public MillPropertyClaimQuestCoordinator(MillPropertyClaimQuestConfig config, DialogueContext dialogueContext,
            ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents)
        { this.config = config; this.dialogueContext = dialogueContext; this.dialogueEvents = dialogueEvents; }

        public void Start() { if (config != null) subscription = dialogueEvents?.Subscribe(OnDialogueEvent); }
        public void Dispose() => subscription?.Dispose();

        private void OnDialogueEvent(DialogueGameplayEventRaisedMessage message)
        {
            QuestController quests = dialogueContext?.PlayerQuestController;
            if (quests == null || message.Event == null) return;
            if (message.Event == config.StartedEvent)
            {
                if (!quests.HasQuest(config.Quest)) quests.TryAddQuestAtNode(config.Quest, config.AskAroundNode);
                return;
            }
            if (!quests.HasQuest(config.Quest) || quests.IsCompleted(config.Quest)) return;
            if (message.Event == config.BanditLeadLearnedEvent ||
                message.Event == config.VillageLeadLearnedEvent ||
                message.Event == config.CorvinIdentifiedEvent)
                quests.TrySetCurrentNode(config.Quest, config.ConfrontCorvinNode);
            else if ((message.Event == config.DocumentsRecoveredEvent || message.Event == config.BribeAcceptedEvent) &&
                     quests.IsAtNode(config.Quest, config.ConfrontCorvinNode))
            {
                QuestNodeData outcomeNode = message.Event == config.DocumentsRecoveredEvent
                    ? config.DocumentsReturnedNode
                    : config.BribeAcceptedNode;

                if (outcomeNode == null)
                {
                    return;
                }

                quests.TrySetCurrentNode(config.Quest, outcomeNode);
                quests.TryCompleteNode(config.Quest, outcomeNode);
            }
        }
    }
}
