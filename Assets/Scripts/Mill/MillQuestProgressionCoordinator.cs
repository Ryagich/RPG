using System;
using System.Threading.Tasks;
using Dialogue;
using MessagePipe;
using Messages;
using Quests;
using Quests.Graph;
using Quests.Graph.Model;
using Saves;
using VContainer.Unity;

namespace Mill
{
    /// <summary>
    /// Coordinates authored mill dialogue events with the ordinary quest graph. Persistent
    /// mill state is represented exclusively by the current and completed nodes of that graph.
    /// </summary>
    public sealed class MillQuestProgressionCoordinator : IStartable, IDisposable
    {
        private const int LegacyGuardSupportFlag = 1 << 0;
        private const int LegacyBanditSupportFlag = 1 << 1;
        private const int LegacyGuardRewardClaimedFlag = 1 << 2;
        private const int LegacyBanditRewardClaimedFlag = 1 << 3;
        private const int LegacyBanditCampRewardClaimedFlag = 1 << 4;

        private readonly MillQuestProgressionConfig config;
        private readonly GameSaveController saveController;
        private readonly DialogueContext dialogueContext;
        private readonly DialogueRuntimeFlagRegistry runtimeFlags;
        private readonly ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents;
        private readonly TaskCompletionSource<bool> ready = new();
        private IDisposable subscription;
        private bool isSaveReady;
        private bool isInitialized;

        public MillQuestProgressionCoordinator(
            MillQuestProgressionConfig config,
            GameSaveController saveController,
            DialogueContext dialogueContext,
            DialogueRuntimeFlagRegistry runtimeFlags,
            ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents)
        {
            this.config = config;
            this.saveController = saveController;
            this.dialogueContext = dialogueContext;
            this.runtimeFlags = runtimeFlags;
            this.dialogueEvents = dialogueEvents;
        }

        public Task Ready => ready.Task;

        public async void Start()
        {
            if (config == null)
            {
                ready.TrySetResult(false);
                return;
            }

            subscription = dialogueEvents.Subscribe(OnDialogueEvent);
            dialogueContext.PlayerQuestControllerReady += OnPlayerQuestControllerReady;

            await saveController.Ready;
            isSaveReady = true;
            TryInitialize(dialogueContext.PlayerQuestController);
        }

        public void Dispose()
        {
            subscription?.Dispose();
            dialogueContext.PlayerQuestControllerReady -= OnPlayerQuestControllerReady;
        }

        private void OnDialogueEvent(DialogueGameplayEventRaisedMessage message)
        {
            if (!isInitialized)
                return;

            switch (message.Event)
            {
                case var eventId when eventId == config.LearnedMillerFateEvent:
                    LearnMillerFate();
                    break;
                case var eventId when eventId == config.AskedBlacksmithEvent:
                    AdvanceRumourInvestigation(config.VisitTavernNode);
                    break;
                case var eventId when eventId == config.AskedHalvarEvent:
                    AdvanceRumourInvestigation(config.ReportToGuardFromRumourNode);
                    break;
                case var eventId when eventId == config.ReportedToGuardEvent:
                    PrepareGuards();
                    break;
                case var eventId when eventId == config.RequestedRansomEvent:
                    RequestRansom();
                    break;
                case var eventId when eventId == config.PaidRansomPrincipalEvent:
                    PayRansomPrincipal();
                    break;
                case var eventId when eventId == config.PaidRansomInterestEvent:
                    PayRansomInterest();
                    break;
            }
        }

        private void LearnMillerFate()
        {
            QuestController quests = dialogueContext.PlayerQuestController;
            if (quests == null || config.IsFateKnown(quests))
                return;

            bool progressed = quests.HasQuest(config.GuardInvestigationQuest)
                ? quests.IsAtNode(config.GuardInvestigationQuest, config.ReportToGuardNode) ||
                  quests.TrySetCurrentNode(config.GuardInvestigationQuest, config.ReportToGuardNode)
                : quests.TryAddQuestAtNode(config.GuardInvestigationQuest, config.AskAroundNode);

            if (progressed)
                runtimeFlags.Activate(config.MillerFateKnownFlag);
        }

        private void AdvanceRumourInvestigation(QuestNodeData targetNode)
        {
            QuestController quests = dialogueContext.PlayerQuestController;
            if (quests == null || !config.IsFateKnown(quests) || !quests.HasQuest(config.GuardInvestigationQuest))
                return;

            quests.TrySetCurrentNode(config.GuardInvestigationQuest, targetNode);
        }

        private void PrepareGuards()
        {
            QuestController quests = dialogueContext.PlayerQuestController;
            MillQuestStage currentStage = config.GetStage(quests);
            if (quests == null || (currentStage != MillQuestStage.FateKnown && currentStage != MillQuestStage.RansomInterestDue))
                return;

            if (!quests.TrySetCurrentNode(config.GuardInvestigationQuest, config.GuardSquadAwaitingNode))
                return;

            runtimeFlags.Activate(config.MillerFateKnownFlag);
            runtimeFlags.Deactivate(config.RansomPrincipalDueFlag);
            runtimeFlags.Deactivate(config.RansomInterestDueFlag);
        }

        private void RequestRansom()
        {
            QuestController quests = dialogueContext.PlayerQuestController;
            MillQuestStage currentStage = config.GetStage(quests);
            if (quests == null || (currentStage != MillQuestStage.FateKnown && currentStage != MillQuestStage.BanditsHeldMill))
                return;

            if (!quests.TrySetCurrentNode(config.GuardInvestigationQuest, config.RansomPrincipalDueNode))
                return;

            runtimeFlags.Activate(config.MillerFateKnownFlag);
            runtimeFlags.Activate(config.RansomPrincipalDueFlag);
            runtimeFlags.Deactivate(config.RansomInterestDueFlag);
        }

        private void PayRansomPrincipal()
        {
            QuestController quests = dialogueContext.PlayerQuestController;
            if (quests == null || config.GetStage(quests) != MillQuestStage.RansomPrincipalDue)
                return;

            if (!quests.TrySetCurrentNode(config.GuardInvestigationQuest, config.RansomInterestDueNode))
                return;

            runtimeFlags.Deactivate(config.RansomPrincipalDueFlag);
            runtimeFlags.Activate(config.RansomInterestDueFlag);
        }

        private void PayRansomInterest()
        {
            QuestController quests = dialogueContext.PlayerQuestController;
            if (quests == null || config.GetStage(quests) != MillQuestStage.RansomInterestDue ||
                !quests.TrySetCurrentNode(config.GuardInvestigationQuest, config.RansomedNode))
                return;

            quests.TryCompleteNode(config.GuardInvestigationQuest, config.RansomedNode);
            runtimeFlags.Deactivate(config.RansomPrincipalDueFlag);
            runtimeFlags.Deactivate(config.RansomInterestDueFlag);
        }

        private void OnPlayerQuestControllerReady(QuestController quests)
        {
            TryInitialize(quests);
        }

        private void TryInitialize(QuestController quests)
        {
            if (isInitialized || !isSaveReady || quests == null || !dialogueContext.IsPlayerQuestControllerReady)
                return;

            MigrateLegacyQuest(quests);
            MigrateLegacyScenario(quests);
            RestoreDialogueState(quests);
            isInitialized = true;
            ready.TrySetResult(true);
        }

        private void RestoreDialogueState(QuestController quests)
        {
            if (config.IsFateKnown(quests))
                runtimeFlags.Activate(config.MillerFateKnownFlag);

            MillQuestStage restoredStage = config.GetStage(quests);
            SetRuntimeFlag(config.RansomPrincipalDueFlag, restoredStage == MillQuestStage.RansomPrincipalDue);
            SetRuntimeFlag(config.RansomInterestDueFlag, restoredStage == MillQuestStage.RansomInterestDue);
        }

        private void MigrateLegacyQuest(QuestController quests)
        {
            QuestGraph legacyQuest = config.LegacyTellMillerFateQuest;
            if (legacyQuest == null || !quests.HasQuest(legacyQuest) || quests.IsCompleted(legacyQuest))
                return;

            QuestNodeData legacyNode = quests.GetCurrentNode(legacyQuest);
            QuestNodeData targetNode = legacyNode == config.LegacyAskAroundNode
                ? config.AskAroundNode
                : legacyNode == config.LegacyVisitTavernNode
                    ? config.VisitTavernNode
                    : legacyNode == config.LegacyReportToGuardNode
                        ? config.ReportToGuardFromRumourNode
                        : legacyNode == config.LegacySquadAwaitingNode
                            ? config.GuardSquadAwaitingNode
                            : legacyNode == config.LegacyHelpRetakeMillNode
                                ? config.HelpRetakeMillNode
                                : null;

            if (targetNode != null && !quests.HasQuest(config.GuardInvestigationQuest))
                quests.TryReplaceActiveQuest(legacyQuest, config.GuardInvestigationQuest, targetNode);
        }

        private void MigrateLegacyScenario(QuestController quests)
        {
            if (!saveController.TryGetLegacyMillScenario(out int legacyStage, out int legacyOutcomeFlags) ||
                legacyStage <= (int)LegacyMillScenarioStage.Occupied)
                return;

            QuestNodeData targetNode = GetLegacyTargetNode(legacyStage, legacyOutcomeFlags);
            if (targetNode == null || !ResetQuestAtNode(quests, targetNode))
                return;

            if (legacyStage == (int)LegacyMillScenarioStage.Liberated ||
                legacyStage == (int)LegacyMillScenarioStage.Ransomed)
            {
                quests.TryCompleteNode(config.GuardInvestigationQuest, targetNode);
            }

            if ((legacyOutcomeFlags & LegacyGuardRewardClaimedFlag) != 0)
                quests.TryMarkNodeReached(config.GuardInvestigationQuest, config.GuardRewardClaimedNode);
            if ((legacyOutcomeFlags & LegacyBanditRewardClaimedFlag) != 0)
                quests.TryMarkNodeReached(config.GuardInvestigationQuest, config.BanditRewardClaimedNode);
            if ((legacyOutcomeFlags & LegacyBanditCampRewardClaimedFlag) != 0)
                quests.TryMarkNodeReached(config.GuardInvestigationQuest, config.BanditCampRewardClaimedNode);
        }

        private QuestNodeData GetLegacyTargetNode(int legacyStage, int legacyOutcomeFlags)
        {
            return (LegacyMillScenarioStage)legacyStage switch
            {
                LegacyMillScenarioStage.FateKnown => config.ReportToGuardNode,
                LegacyMillScenarioStage.GuardsPrepared => config.GuardSquadAwaitingNode,
                LegacyMillScenarioStage.GuardsMarching => config.HelpRetakeMillNode,
                LegacyMillScenarioStage.AssaultInProgress => config.HelpRetakeMillNode,
                LegacyMillScenarioStage.Liberated => (legacyOutcomeFlags & LegacyGuardSupportFlag) != 0
                    ? config.GuardVictoryWithPlayerNode
                    : config.GuardVictoryNode,
                LegacyMillScenarioStage.BanditsHeldMill => (legacyOutcomeFlags & LegacyBanditSupportFlag) != 0
                    ? config.BanditVictoryWithPlayerNode
                    : config.GuardAssaultFailedNode,
                LegacyMillScenarioStage.RansomPrincipalDue => config.RansomPrincipalDueNode,
                LegacyMillScenarioStage.RansomInterestDue => config.RansomInterestDueNode,
                LegacyMillScenarioStage.Ransomed => config.RansomedNode,
                _ => null
            };
        }

        private bool ResetQuestAtNode(QuestController quests, QuestNodeData targetNode)
        {
            if (quests.HasQuest(config.GuardInvestigationQuest))
                quests.TryRemoveQuest(config.GuardInvestigationQuest);

            return quests.TryAddQuestAtNode(config.GuardInvestigationQuest, targetNode);
        }

        private void SetRuntimeFlag(DialogueRuntimeFlag flag, bool isActive)
        {
            if (isActive)
                runtimeFlags?.Activate(flag);
            else
                runtimeFlags?.Deactivate(flag);
        }

        private enum LegacyMillScenarioStage
        {
            Occupied = 0,
            FateKnown = 1,
            GuardsPrepared = 2,
            GuardsMarching = 3,
            AssaultInProgress = 4,
            Liberated = 5,
            BanditsHeldMill = 6,
            RansomPrincipalDue = 7,
            RansomInterestDue = 8,
            Ransomed = 9
        }
    }
}
