using System;
using Dialogue;
using MessagePipe;
using Messages;
using Quests;
using Quests.Graph;
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
        private readonly GameSaveController saveController;
        private readonly DialogueContext dialogueContext;
        private readonly DialogueRuntimeFlagRegistry runtimeFlags;
        private readonly ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents;
        private IDisposable subscription;
        private bool isSaveReady;

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

        public async void Start()
        {
            if (config != null)
            {
                subscription = dialogueEvents?.Subscribe(OnDialogueEvent);
                dialogueContext.PlayerQuestControllerReady += OnPlayerQuestControllerReady;
                await saveController.Ready;
                isSaveReady = true;
                RestoreDialogueState();
                if (dialogueContext.IsPlayerQuestControllerReady)
                {
                    MigrateLegacyQuest(dialogueContext.PlayerQuestController);
                }
            }
        }

        public void Dispose()
        {
            subscription?.Dispose();
            dialogueContext.PlayerQuestControllerReady -= OnPlayerQuestControllerReady;
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
                : TryStartMillQuestAt(config.AskAroundNode);
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
                !quests.HasQuest(config.GuardInvestigationQuest))
            {
                return;
            }

            quests.TrySetCurrentNode(config.GuardInvestigationQuest, node);
        }

        private void PrepareGuards()
        {
            MillScenarioStage stage = GetStage();
            QuestController quests = dialogueContext?.PlayerQuestController;
            if (quests == null || (stage != MillScenarioStage.FateKnown && stage != MillScenarioStage.RansomInterestDue))
            {
                return;
            }

            bool progressed = quests.TrySetCurrentNode(
                config.GuardInvestigationQuest,
                config.GuardSquadAwaitingNode);

            if (progressed)
            {
                SetStage(MillScenarioStage.GuardsPrepared);
                runtimeFlags?.Deactivate(config.RansomPrincipalDueFlag);
                runtimeFlags?.Deactivate(config.RansomInterestDueFlag);
            }
        }

        private void RequestRansom()
        {
            if (GetStage() is not (MillScenarioStage.FateKnown or MillScenarioStage.BanditsHeldMill))
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

        private void OnPlayerQuestControllerReady(QuestController _)
        {
            if (isSaveReady)
            {
                MigrateLegacyQuest(dialogueContext.PlayerQuestController);
            }
        }

        private void MigrateLegacyQuest(QuestController quests)
        {
            QuestGraph legacyQuest = config.LegacyTellMillerFateQuest;
            if (legacyQuest == null || !quests.HasQuest(legacyQuest) || quests.IsCompleted(legacyQuest))
            {
                return;
            }

            Quests.Graph.Model.QuestNodeData legacyNode = quests.GetCurrentNode(legacyQuest);
            Quests.Graph.Model.QuestNodeData targetNode = legacyNode == config.LegacyAskAroundNode
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

            if (targetNode != null)
            {
                quests.TryReplaceActiveQuest(legacyQuest, config.GuardInvestigationQuest, targetNode);
            }
        }

        private bool TryStartMillQuestAt(Quests.Graph.Model.QuestNodeData node)
        {
            QuestController quests = dialogueContext?.PlayerQuestController;
            if (quests == null)
            {
                return false;
            }

            return quests.TryAddQuestAtNode(config.GuardInvestigationQuest, node);
        }

        private MillScenarioStage GetStage()
        {
            int stage = saveController.GetMillScenarioStage();
            return Enum.IsDefined(typeof(MillScenarioStage), stage)
                ? (MillScenarioStage)stage
                : MillScenarioStage.Occupied;
        }

        private void SetStage(MillScenarioStage stage)
        {
            saveController.SetMillScenarioState((int)stage, new[] { saveController.GetMillOutcomeFlag(0) });
        }
    }
}
