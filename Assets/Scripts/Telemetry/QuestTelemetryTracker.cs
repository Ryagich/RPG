using System;
using System.Collections.Generic;
using Dialogue;
using Quests;
using Quests.Graph;
using VContainer.Unity;

namespace Telemetry
{
    /// <summary>
    /// Observes player quest facts after save restoration. QuestController remains the owner of
    /// progression; this application-level observer does not alter its state.
    /// </summary>
    public sealed class QuestTelemetryTracker : IStartable, IDisposable
    {
        private readonly DialogueContext dialogueContext;
        private readonly IGameTelemetry telemetry;
        private QuestController observedQuests;

        public QuestTelemetryTracker(DialogueContext dialogueContext, IGameTelemetry telemetry)
        {
            this.dialogueContext = dialogueContext;
            this.telemetry = telemetry;
        }

        public void Start()
        {
            dialogueContext.PlayerQuestControllerReady += Attach;
            if (dialogueContext.IsPlayerQuestControllerReady)
            {
                Attach(dialogueContext.PlayerQuestController);
            }
        }

        public void Dispose()
        {
            dialogueContext.PlayerQuestControllerReady -= Attach;
            Detach();
        }

        private void Attach(QuestController quests)
        {
            if (observedQuests == quests)
            {
                return;
            }

            Detach();
            observedQuests = quests;
            if (observedQuests != null)
            {
                observedQuests.Changed += OnQuestChanged;
            }
        }

        private void Detach()
        {
            if (observedQuests != null)
            {
                observedQuests.Changed -= OnQuestChanged;
                observedQuests = null;
            }
        }

        private void OnQuestChanged(QuestChangeInfo change)
        {
            string eventId = change.Type switch
            {
                QuestChangeType.Added => GameTelemetryEvents.QuestStarted,
                QuestChangeType.Completed => GameTelemetryEvents.QuestCompleted,
                _ => null
            };

            if (eventId == null || change.Quest == null || string.IsNullOrWhiteSpace(change.Quest.PersistentId))
            {
                return;
            }

            telemetry.Track(eventId, new Dictionary<string, string>
            {
                ["quest_id"] = change.Quest.PersistentId
            });
        }
    }
}
