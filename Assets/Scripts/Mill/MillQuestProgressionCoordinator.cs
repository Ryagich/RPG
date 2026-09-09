using System;
using Dialogue;
using MessagePipe;
using Messages;
using Quests;
using Saves;
using VContainer.Unity;

namespace Mill
{
    /// <summary>
    /// Coordinates mill-investigation dialogue events while any location is active. Location
    /// composition and the assault remain owned by <see cref="MillScenarioController"/>.
    /// </summary>
    public sealed class MillQuestProgressionCoordinator : IStartable, IDisposable
    {
        private readonly MillQuestProgressionConfig config;
        private readonly GameSaveService saveService;
        private readonly DialogueContext dialogueContext;
        private readonly DialogueRuntimeFlagRegistry runtimeFlags;
        private readonly ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents;
        private IDisposable subscription;
        private bool isSaveReady;

        public MillQuestProgressionCoordinator(
            MillQuestProgressionConfig config,
            GameSaveService saveService,
            DialogueContext dialogueContext,
            DialogueRuntimeFlagRegistry runtimeFlags,
            ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents)
        {
            this.config = config;
            this.saveService = saveService;
            this.dialogueContext = dialogueContext;
            this.runtimeFlags = runtimeFlags;
            this.dialogueEvents = dialogueEvents;
        }

        public async void Start()
        {
            if (config != null)
            {
                subscription = dialogueEvents?.Subscribe(OnDialogueEvent);
                dialogueContext.PlayerQuestControllerAssigned += OnPlayerQuestControllerAssigned;
                await saveService.Ready;
                isSaveReady = true;
                RestoreDialogueState();
                ReconcileFateKnownQuest();
            }
        }

        public void Dispose()
        {
            subscription?.Dispose();
            dialogueContext.PlayerQuestControllerAssigned -= OnPlayerQuestControllerAssigned;
        }

        private void OnDialogueEvent(DialogueGameplayEventRaisedMessage message)
        {
            if (message.Event == config.LearnedMillerFateEvent)
            {
                LearnMillerFate();
            }
            else if (message.Event == config.AskedBlacksmithEvent)
            {
                AdvanceRumourQuest(config.VisitTavernNode);
            }
            else if (message.Event == config.AskedHalvarEvent)
            {
                AdvanceRumourQuest(config.ReportToGuardFromRumourNode);
            }
            else if (message.Event == config.ReportedToGuardEvent)
            {
                PrepareGuards();
            }
            else if (message.Event == config.RequestedRansomEvent)
            {
                RequestRansom();
            }
            else if (message.Event == config.PaidRansomPrincipalEvent)
            {
                PayRansomPrincipal();
            }
            else if (message.Event == config.PaidRansomInterestEvent)
            {
                PayRansomInterest();
            }
        }

        private void LearnMillerFate()
        {
            if (GetStage() >= MillScenarioStage.FateKnown)
            {
                return;
            }

            QuestController quests = dialogueContext?.PlayerQuestController;
            if (quests == null)
            {
                return;
            }

            bool isProgressed = quests.HasQuest(config.GuardInvestigationQuest)
                ? quests.IsAtNode(config.GuardInvestigationQuest, config.ReportToGuardNode) ||
                  quests.TrySetCurrentNode(config.GuardInvestigationQuest, config.ReportToGuardNode)
                : quests.HasQuest(config.TellMillerFateQuest) ||
                  quests.TryAddQuest(config.TellMillerFateQuest);
            if (!isProgressed)
            {
                return;
            }

            SetStage(MillScenarioStage.FateKnown);
            runtimeFlags?.Activate(config.MillerFateKnownFlag);
        }

        private void AdvanceRumourQuest(Quests.Graph.Model.QuestNodeData node)
        {
            QuestController quests = dialogueContext?.PlayerQuestController;
            if (GetStage() != MillScenarioStage.FateKnown || quests == null ||
                !quests.HasQuest(config.TellMillerFateQuest))
            {
                return;
            }

            quests.TrySetCurrentNode(config.TellMillerFateQuest, node);
        }

        private void PrepareGuards()
        {
            MillScenarioStage stage = GetStage();
            QuestController quests = dialogueContext?.PlayerQuestController;
            if (quests == null || (stage != MillScenarioStage.FateKnown && stage != MillScenarioStage.RansomInterestDue))
            {
                return;
            }

            bool progressed = quests.HasQuest(config.GuardInvestigationQuest)
                ? quests.TrySetCurrentNode(config.GuardInvestigationQuest, config.GuardSquadAwaitingNode)
                : quests.HasQuest(config.TellMillerFateQuest) &&
                  quests.TrySetCurrentNode(config.TellMillerFateQuest, config.RumourSquadAwaitingNode);

            if (progressed)
            {
                SetStage(MillScenarioStage.GuardsPrepared);
                runtimeFlags?.Deactivate(config.RansomPrincipalDueFlag);
                runtimeFlags?.Deactivate(config.RansomInterestDueFlag);
            }
        }

        private void RequestRansom()
        {
            if (GetStage() != MillScenarioStage.FateKnown)
            {
                return;
            }

            SetStage(MillScenarioStage.RansomPrincipalDue);
            runtimeFlags?.Replace(
                new[] { config.RansomPrincipalDueFlag, config.RansomInterestDueFlag },
                config.RansomPrincipalDueFlag);
        }

        private void PayRansomPrincipal()
        {
            if (GetStage() != MillScenarioStage.RansomPrincipalDue)
            {
                return;
            }

            SetStage(MillScenarioStage.RansomInterestDue);
            runtimeFlags?.Replace(
                new[] { config.RansomPrincipalDueFlag, config.RansomInterestDueFlag },
                config.RansomInterestDueFlag);
        }

        private void PayRansomInterest()
        {
            if (GetStage() != MillScenarioStage.RansomInterestDue)
            {
                return;
            }

            QuestController quests = dialogueContext?.PlayerQuestController;
            if (quests != null)
            {
                CompleteActiveMillQuest(quests, config.GuardInvestigationQuest);
                CompleteActiveMillQuest(quests, config.TellMillerFateQuest);
            }

            SetStage(MillScenarioStage.Ransomed);
            runtimeFlags?.Deactivate(config.RansomPrincipalDueFlag);
            runtimeFlags?.Deactivate(config.RansomInterestDueFlag);
        }

        private static void CompleteActiveMillQuest(QuestController quests, Quests.Graph.QuestGraph quest)
        {
            if (quests != null && quests.HasQuest(quest))
            {
                quests.TryCompleteNode(quest, quests.GetCurrentNode(quest));
            }
        }

        private void RestoreDialogueState()
        {
            MillScenarioStage stage = GetStage();
            if (stage >= MillScenarioStage.FateKnown)
            {
                runtimeFlags?.Activate(config.MillerFateKnownFlag);
            }

            if (stage == MillScenarioStage.RansomPrincipalDue)
            {
                runtimeFlags?.Activate(config.RansomPrincipalDueFlag);
            }
            else if (stage == MillScenarioStage.RansomInterestDue)
            {
                runtimeFlags?.Activate(config.RansomInterestDueFlag);
            }
        }

        private void OnPlayerQuestControllerAssigned(QuestController _)
        {
            if (isSaveReady)
            {
                ReconcileFateKnownQuest();
            }
        }

        private void ReconcileFateKnownQuest()
        {
            if (GetStage() != MillScenarioStage.FateKnown)
            {
                return;
            }

            QuestController quests = dialogueContext.PlayerQuestController;
            if (quests == null)
            {
                return;
            }

            if (quests.HasQuest(config.GuardInvestigationQuest))
            {
                quests.TrySetCurrentNode(config.GuardInvestigationQuest, config.ReportToGuardNode);
            }
            else if (!quests.HasQuest(config.TellMillerFateQuest))
            {
                quests.TryAddQuest(config.TellMillerFateQuest);
            }
        }

        private MillScenarioStage GetStage()
        {
            int stage = saveService.GetMillScenarioStage();
            return Enum.IsDefined(typeof(MillScenarioStage), stage)
                ? (MillScenarioStage)stage
                : MillScenarioStage.Occupied;
        }

        private void SetStage(MillScenarioStage stage)
        {
            saveService.SaveMillScenario((int)stage, new[] { saveService.GetMillOutcomeFlag(0) });
        }
    }
}
