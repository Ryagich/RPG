using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Combat;
using Container;
using Dialogue;
using Factions;
using Landings.Fields;
using MessagePipe;
using Messages;
using NPC;
using Quests;
using Quests.Graph.Model;
using TargetLock;
using UnityEngine;
using VContainer;

namespace Mill
{
    /// <summary>
    /// Owns the live mill encounter. Its persistent state belongs to the mill quest; this
    /// component only reconstructs and advances the scene from that quest state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MillScenarioController : MonoBehaviour
    {
        [Header("Mill residents")]
        [SerializeField] private Transform ulrik;
        [SerializeField] private Transform varekHolt;
        [SerializeField] private Transform[] bandits = Array.Empty<Transform>();
        [SerializeField] private Transform squadLeader;
        [SerializeField] private Transform[] guards = Array.Empty<Transform>();
        [SerializeField] private FarmField[] millFields = Array.Empty<FarmField>();
        [SerializeField] private GameObject[] locationExitZones = Array.Empty<GameObject>();
        [SerializeField] private Transform[] guardAttackDestinations = Array.Empty<Transform>();

        [Header("Dialogue contracts")]
        [SerializeField] private DialogueGameplayEvent beginAssaultEvent;
        [SerializeField] private DialogueGameplayEvent claimGuardRewardEvent;
        [SerializeField] private DialogueGameplayEvent claimBanditRewardEvent;
        [SerializeField] private DialogueGameplayEvent claimBanditCampRewardEvent;

        [Header("Dialogue outcome flags")]
        [SerializeField] private DialogueRuntimeFlag guardVictoryFlag;
        [SerializeField] private DialogueRuntimeFlag banditVictoryFlag;
        [SerializeField] private DialogueRuntimeFlag playerSupportedGuardsFlag;
        [SerializeField] private DialogueRuntimeFlag playerSupportedBanditsFlag;
        [SerializeField] private DialogueRuntimeFlag guardRewardClaimedFlag;
        [SerializeField] private DialogueRuntimeFlag banditRewardClaimedFlag;
        [SerializeField] private DialogueRuntimeFlag banditCampRewardClaimedFlag;

        [Header("Outcome rewards")]
        [SerializeField] private List<QuestResourceEntry> guardVictoryRewards = new();
        [SerializeField] private List<QuestResourceEntry> banditPassiveVictoryRewards = new();
        [SerializeField] private List<QuestResourceEntry> banditVictoryRewards = new();
        [SerializeField] private List<QuestResourceEntry> banditCampRewards = new();
        [SerializeField] private FactionConfig playerFaction;
        [SerializeField] private FactionConfig banditFaction;
        [SerializeField, Min(1)] private int banditReputationGain = 100;

        private MillQuestProgressionConfig config;
        private MillQuestProgressionCoordinator progressionCoordinator;
        private IFactionRelations factionRelations;
        private DialogueRuntimeFlagRegistry runtimeFlags;
        private DialogueContext dialogueContext;
        private ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents;
        private ISubscriber<CharacterDamagedMessage> characterDamaged;
        private QuestController quests;
        private IDisposable dialogueSubscription;
        private IDisposable damageSubscription;
        private MillQuestStage stage;
        private bool playerSupportedGuards;
        private bool playerSupportedBandits;
        private bool assaultOrdersIssued;
        private bool assaultStarted;
        private bool millLiberatedWithoutQuest;
        private readonly TaskCompletionSource<bool> constructed = new();

        [Inject]
        public void Construct(
            MillQuestProgressionConfig config,
            MillQuestProgressionCoordinator progressionCoordinator,
            IFactionRelations factionRelations,
            DialogueRuntimeFlagRegistry runtimeFlags,
            DialogueContext dialogueContext,
            ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents,
            ISubscriber<CharacterDamagedMessage> characterDamaged)
        {
            this.config = config;
            this.progressionCoordinator = progressionCoordinator;
            this.factionRelations = factionRelations;
            this.runtimeFlags = runtimeFlags;
            this.dialogueContext = dialogueContext;
            this.dialogueEvents = dialogueEvents;
            this.characterDamaged = characterDamaged;
            constructed.TrySetResult(true);
        }

        private void Awake()
        {
            ApplyWorldState(MillQuestStage.Occupied, false);
        }

        private async void Start()
        {
            await constructed.Task;
            await progressionCoordinator.Ready;

            quests = dialogueContext.PlayerQuestController;
            if (quests == null)
                return;

            quests.Changed += OnQuestChanged;
            dialogueSubscription = dialogueEvents?.Subscribe(OnDialogueEvent);
            damageSubscription = characterDamaged?.Subscribe(OnCharacterDamaged);
            UpdateStage(false);
            RestoreOutcomeFlags();
        }

        private void Update()
        {
            if (TryLiberateMillWithoutQuest())
            {
                return;
            }

            if (stage != MillQuestStage.AssaultInProgress)
                return;

            if (!assaultStarted)
                TryStartAssaultAfterArrival();

            if (assaultStarted)
                ResolveBattleIfFinished();
        }

        private void OnDestroy()
        {
            if (quests != null)
                quests.Changed -= OnQuestChanged;

            dialogueSubscription?.Dispose();
            damageSubscription?.Dispose();
        }

        private void OnQuestChanged(QuestChangeInfo change)
        {
            if (change.Quest == config.GuardInvestigationQuest)
                UpdateStage(true);
        }

        private void OnDialogueEvent(DialogueGameplayEventRaisedMessage message)
        {
            if (message.Event == beginAssaultEvent)
            {
                BeginAssault();
            }
            else if (message.Event == claimGuardRewardEvent)
            {
                ClaimGuardReward();
            }
            else if (message.Event == claimBanditRewardEvent)
            {
                ClaimBanditReward();
            }
            else if (message.Event == claimBanditCampRewardEvent)
            {
                ClaimBanditCampReward();
            }
        }

        private void BeginAssault()
        {
            if (stage != MillQuestStage.GuardsPrepared ||
                !quests.TrySetCurrentNode(config.GuardInvestigationQuest, config.HelpRetakeMillNode))
                return;

            SetExitAvailability(false);
            IssueMarchOrders();
        }

        private void IssueMarchOrders()
        {
            if (assaultOrdersIssued)
                return;

            assaultOrdersIssued = true;
            int index = 0;
            foreach (Transform guard in GetGuardParticipants())
            {
                Transform destination = index < guardAttackDestinations.Length
                    ? guardAttackDestinations[index]
                    : varekHolt;
                index++;
                if (guard == null || destination == null || !guard.gameObject.activeInHierarchy)
                    continue;

                GetScope(guard)?.Container.Resolve<NpcNavMeshController>()?.MoveTo(destination.position, stoppingDistance: 2.5f);
            }
        }

        private void TryStartAssaultAfterArrival()
        {
            if (!assaultOrdersIssued || !AllLivingGuardsAtDestinations())
                return;

            TargetLockTarget banditTarget = GetTarget(varekHolt);
            foreach (Transform guard in GetGuardParticipants())
            {
                if (IsAlive(guard))
                    GetScope(guard)?.Container.Resolve<NpcCombatService>()?.ReceiveAggressionNotification(banditTarget, true);
            }

            assaultStarted = true;
        }

        private bool AllLivingGuardsAtDestinations()
        {
            bool anyLivingGuard = false;
            foreach (Transform guard in GetGuardParticipants())
            {
                if (!IsAlive(guard))
                    continue;

                anyLivingGuard = true;
                NpcNavMeshController movement = GetScope(guard)?.Container.Resolve<NpcNavMeshController>();
                if (movement != null && !movement.HasReachedDestination)
                    return false;
            }

            return anyLivingGuard;
        }

        private void ResolveBattleIfFinished()
        {
            if (AllDefeated(bandits, varekHolt))
            {
                if (CompleteScenario(playerSupportedGuards
                        ? config.GuardVictoryWithPlayerNode
                        : config.GuardVictoryNode,
                        completeQuest: true))
                    runtimeFlags?.Activate(guardVictoryFlag);
                return;
            }

            if (AllDefeated(guards, squadLeader))
            {
                if (CompleteScenario(playerSupportedBandits
                        ? config.BanditVictoryWithPlayerNode
                        : config.GuardAssaultFailedNode,
                        completeQuest: false))
                    runtimeFlags?.Activate(banditVictoryFlag);
            }
        }

        private bool TryLiberateMillWithoutQuest()
        {
            // Clearing the encounter before taking its quest is still a valid world action.
            // The quest remains absent, while the mill's local residents and fields return to
            // their ordinary state. Saved NPC deaths reach this check after their scopes restore.
            if (millLiberatedWithoutQuest || stage != MillQuestStage.Occupied ||
                quests == null || quests.HasQuest(config.GuardInvestigationQuest) ||
                !AllDefeated(bandits, varekHolt))
            {
                return false;
            }

            millLiberatedWithoutQuest = true;
            SetActive(ulrik, true);
            foreach (FarmField field in millFields)
            {
                if (field != null)
                {
                    field.enabled = true;
                }
            }

            SetExitAvailability(true);
            return true;
        }

        private bool CompleteScenario(Quests.Graph.Model.QuestNodeData outcomeNode, bool completeQuest)
        {
            if (!quests.TrySetCurrentNode(config.GuardInvestigationQuest, outcomeNode))
                return false;

            if (completeQuest)
                quests.TryCompleteNode(config.GuardInvestigationQuest, outcomeNode);

            SetExitAvailability(true);
            return true;
        }

        private void OnCharacterDamaged(CharacterDamagedMessage message)
        {
            if (stage != MillQuestStage.AssaultInProgress || message.Attacker == null ||
                message.Attacker.OwnerTransform?.GetComponentInParent<PlayerLifetimeScope>() == null)
                return;

            if (BelongsTo(message.CharacterTransform, bandits, varekHolt))
            {
                playerSupportedGuards = true;
                runtimeFlags?.Activate(playerSupportedGuardsFlag);
            }
            else if (BelongsTo(message.CharacterTransform, guards, squadLeader))
            {
                playerSupportedBandits = true;
                runtimeFlags?.Activate(playerSupportedBanditsFlag);
            }
        }

        private void UpdateStage(bool isRuntimeTransition)
        {
            MillQuestStage nextStage = config.GetStage(quests);
            if (stage == nextStage)
                return;

            stage = nextStage;
            assaultStarted = false;
            ApplyWorldState(stage, isRuntimeTransition);
        }

        private void ApplyWorldState(MillQuestStage currentStage, bool isRuntimeTransition)
        {
            bool liberated = currentStage == MillQuestStage.Liberated;
            bool guardEncounterVisible = currentStage is MillQuestStage.GuardsPrepared or MillQuestStage.AssaultInProgress;
            bool retainRuntimeParticipants = isRuntimeTransition &&
                                             (currentStage is MillQuestStage.Liberated or MillQuestStage.BanditsHeldMill);
            bool banditsVisible = !liberated || retainRuntimeParticipants;

            SetActive(ulrik, liberated);
            SetActive(varekHolt, banditsVisible);
            foreach (Transform bandit in bandits)
                SetActive(bandit, banditsVisible);

            SetActive(squadLeader, guardEncounterVisible || retainRuntimeParticipants);
            foreach (Transform guard in guards)
                SetActive(guard, guardEncounterVisible || retainRuntimeParticipants);

            foreach (FarmField field in millFields)
            {
                if (field != null)
                    field.enabled = liberated;
            }

            if (currentStage == MillQuestStage.AssaultInProgress)
            {
                SetExitAvailability(false);
                assaultOrdersIssued = false;
                IssueMarchOrders();
            }
            else if (currentStage is MillQuestStage.Liberated or MillQuestStage.BanditsHeldMill)
            {
                SetExitAvailability(true);
            }
        }

        private void RestoreOutcomeFlags()
        {
            playerSupportedGuards = config.HasReachedNode(quests, config.GuardVictoryWithPlayerNode);
            playerSupportedBandits = config.HasReachedNode(quests, config.BanditVictoryWithPlayerNode);

            SetRuntimeFlag(playerSupportedGuardsFlag, playerSupportedGuards);
            SetRuntimeFlag(playerSupportedBanditsFlag, playerSupportedBandits);
            SetRuntimeFlag(guardRewardClaimedFlag, config.HasReachedNode(quests, config.GuardRewardClaimedNode));
            SetRuntimeFlag(banditRewardClaimedFlag, config.HasReachedNode(quests, config.BanditRewardClaimedNode));
            SetRuntimeFlag(banditCampRewardClaimedFlag, config.HasReachedNode(quests, config.BanditCampRewardClaimedNode));
            SetRuntimeFlag(guardVictoryFlag, IsGuardVictory());
            SetRuntimeFlag(banditVictoryFlag, stage == MillQuestStage.BanditsHeldMill);

            EnsurePreAssaultNeutrality();
        }

        private void EnsurePreAssaultNeutrality()
        {
            if (stage is not (MillQuestStage.Occupied or MillQuestStage.FateKnown or MillQuestStage.GuardsPrepared) ||
                playerFaction == null || banditFaction == null || factionRelations == null)
                return;

            int relation = factionRelations.GetRelation(playerFaction, banditFaction);
            if (relation != 0)
                factionRelations.TryChangeRelation(playerFaction, banditFaction, -relation);
        }

        private void ClaimGuardReward()
        {
            if (!IsGuardVictory() ||
                config.HasReachedNode(quests, config.GuardRewardClaimedNode) ||
                !quests.TryGrantResources(guardVictoryRewards))
                return;

            quests.TryMarkNodeReached(config.GuardInvestigationQuest, config.GuardRewardClaimedNode);
            runtimeFlags?.Activate(guardRewardClaimedFlag);
        }

        private bool IsGuardVictory()
        {
            return config.HasReachedNode(quests, config.GuardVictoryNode) ||
                   config.HasReachedNode(quests, config.GuardVictoryWithPlayerNode);
        }

        private void ClaimBanditReward()
        {
            if (stage != MillQuestStage.BanditsHeldMill ||
                config.HasReachedNode(quests, config.BanditRewardClaimedNode))
                return;

            bool helpedBandits = config.HasReachedNode(quests, config.BanditVictoryWithPlayerNode);
            IReadOnlyList<QuestResourceEntry> rewards = helpedBandits
                ? banditVictoryRewards
                : banditPassiveVictoryRewards;
            if (!quests.TryGrantResources(rewards))
                return;

            if (helpedBandits)
                factionRelations?.TryChangeRelation(playerFaction, banditFaction, banditReputationGain);

            quests.TryMarkNodeReached(config.GuardInvestigationQuest, config.BanditRewardClaimedNode);
            runtimeFlags?.Activate(banditRewardClaimedFlag);
        }

        private void ClaimBanditCampReward()
        {
            if (stage != MillQuestStage.BanditsHeldMill ||
                config.HasReachedNode(quests, config.BanditCampRewardClaimedNode) ||
                !quests.TryGrantResources(banditCampRewards))
                return;

            quests.TryMarkNodeReached(config.GuardInvestigationQuest, config.BanditCampRewardClaimedNode);
            runtimeFlags?.Activate(banditCampRewardClaimedFlag);
        }

        private void SetExitAvailability(bool isAvailable)
        {
            foreach (GameObject exitZone in locationExitZones)
            {
                if (exitZone != null)
                    exitZone.SetActive(isAvailable);
            }
        }

        private void SetRuntimeFlag(DialogueRuntimeFlag flag, bool isActive)
        {
            if (isActive)
                runtimeFlags?.Activate(flag);
            else
                runtimeFlags?.Deactivate(flag);
        }

        private static bool BelongsTo(Transform character, Transform[] group, Transform leader)
        {
            if (character == null)
                return false;

            if (leader != null && character.IsChildOf(leader))
                return true;

            foreach (Transform member in group)
            {
                if (member != null && character.IsChildOf(member))
                    return true;
            }

            return false;
        }

        private static bool AllDefeated(Transform[] group, Transform leader)
        {
            if (!IsDefeated(leader))
                return false;

            foreach (Transform member in group)
            {
                if (!IsDefeated(member))
                    return false;
            }

            return true;
        }

        private static bool IsAlive(Transform transform) => !IsDefeated(transform);

        private static bool IsDefeated(Transform transform)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy)
                return true;

            CharacterDamageReceiver receiver = transform.GetComponentInChildren<DamageReceiverHost>(true)?.Receiver;
            return receiver != null && !receiver.IsAlive;
        }

        private IEnumerable<Transform> GetGuardParticipants()
        {
            if (squadLeader != null)
                yield return squadLeader;

            foreach (Transform guard in guards)
            {
                if (guard != null && guard != squadLeader)
                    yield return guard;
            }
        }

        private static TargetLockTarget GetTarget(Transform transform) =>
            transform != null ? transform.GetComponent<TargetLockTarget>() : null;

        private static NpcLifetimeScope GetScope(Transform transform) =>
            transform != null ? transform.GetComponent<NpcLifetimeScope>() : null;

        private static void SetActive(Transform transform, bool isActive)
        {
            if (transform != null)
                transform.gameObject.SetActive(isActive);
        }
    }
}
