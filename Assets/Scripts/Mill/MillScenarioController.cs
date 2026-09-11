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
using Quests.Graph;
using Quests.Graph.Model;
using Saves;
using TargetLock;
using UnityEngine;
using VContainer;

namespace Mill
{
    /// <summary>
    /// Owns the mill occupation as a location-specific domain scenario. Dialogue only raises
    /// authored events; this component owns stage transitions, encounter composition and battle
    /// resolution. All references are explicit so no other bandits, guards or fields are touched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MillScenarioController : MonoBehaviour
    {
        private const int GuardSupportFlag = (int)MillScenarioOutcomeFlags.PlayerSupportedGuards;
        private const int BanditSupportFlag = (int)MillScenarioOutcomeFlags.PlayerSupportedBandits;
        private const int GuardRewardClaimedFlag = (int)MillScenarioOutcomeFlags.GuardRewardClaimed;
        private const int BanditRewardClaimedFlag = (int)MillScenarioOutcomeFlags.BanditRewardClaimed;
        private const int BanditCampRewardClaimedFlag = (int)MillScenarioOutcomeFlags.BanditCampRewardClaimed;

        [Header("Mill residents")]
        // Scene instances are recreated whenever the location is loaded.  Keep anchors as
        // transforms rather than serializing nested prefab components: this makes ownership
        // explicit while still resolving the NPC scope only at the point where a service is
        // needed.
        [SerializeField] private Transform ulrik;
        [SerializeField] private Transform varekHolt;
        [SerializeField] private Transform[] bandits = Array.Empty<Transform>();
        [SerializeField] private Transform squadLeader;
        [SerializeField] private Transform[] guards = Array.Empty<Transform>();
        [SerializeField] private FarmField[] millFields = Array.Empty<FarmField>();
        [SerializeField] private GameObject[] locationExitZones = Array.Empty<GameObject>();
        [SerializeField] private Transform[] guardAttackDestinations = Array.Empty<Transform>();

        [Header("Quest progression")]
        [SerializeField] private QuestGraph guardInvestigationQuest;
        [SerializeField] private QuestNodeData guardHelpRetakeMillNode;
        [SerializeField] private QuestNodeData guardAssaultFailedNode;

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

        private GameSaveController saveController;
        private DialogueRuntimeFlagRegistry runtimeFlags;
        private DialogueContext dialogueContext;
        private IFactionRelations factionRelations;
        private ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents;
        private ISubscriber<CharacterDamagedMessage> characterDamaged;
        private IDisposable dialogueSubscription;
        private IDisposable damageSubscription;
        private MillScenarioStage stage;
        private int outcomeFlags;
        private bool assaultOrdersIssued;
        private readonly TaskCompletionSource<bool> constructed = new();

        [Inject]
        public void Construct(
            GameSaveController saveController,
            IFactionRelations factionRelations,
            DialogueRuntimeFlagRegistry runtimeFlags,
            DialogueContext dialogueContext,
            ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEvents,
            ISubscriber<CharacterDamagedMessage> characterDamaged)
        {
            this.saveController = saveController;
            this.factionRelations = factionRelations;
            this.runtimeFlags = runtimeFlags;
            this.dialogueContext = dialogueContext;
            this.dialogueEvents = dialogueEvents;
            this.characterDamaged = characterDamaged;
            constructed.TrySetResult(true);
        }

        private void Awake()
        {
            ApplyWorldState(MillScenarioStage.Occupied, false);
        }

        private async void Start()
        {
            await constructed.Task;
            await saveController.Ready;
            stage = (MillScenarioStage)saveController.GetMillScenarioStage();
            if (!Enum.IsDefined(typeof(MillScenarioStage), stage))
            {
                stage = MillScenarioStage.Occupied;
            }

            outcomeFlags = saveController.GetMillOutcomeFlag(0);
            EnsurePreAssaultNeutrality();
            dialogueSubscription = dialogueEvents?.Subscribe(OnDialogueEvent);
            damageSubscription = characterDamaged?.Subscribe(OnCharacterDamaged);
            ApplyWorldState(stage, false);
            RestoreOutcomeFlags();
        }

        private void Update()
        {
            if (stage == MillScenarioStage.GuardsMarching)
            {
                TryStartAssaultAfterArrival();
            }

            if (stage is MillScenarioStage.GuardsMarching or MillScenarioStage.AssaultInProgress or MillScenarioStage.BanditsHeldMill)
            {
                ResolveBattleIfFinished();
            }
        }

        private void OnDestroy()
        {
            dialogueSubscription?.Dispose();
            damageSubscription?.Dispose();
        }

        private void OnDialogueEvent(DialogueGameplayEventRaisedMessage message)
        {
            if (message.Event == beginAssaultEvent)
            {
                BeginAssault();
            }
            else if (message.Event == claimGuardRewardEvent)
            {
                ClaimReward(GuardRewardClaimedFlag, guardRewardClaimedFlag, guardVictoryRewards, false);
            }
            else if (message.Event == claimBanditRewardEvent)
            {
                bool playerSupportedBandits = (outcomeFlags & BanditSupportFlag) != 0;
                ClaimReward(
                    BanditRewardClaimedFlag,
                    banditRewardClaimedFlag,
                    playerSupportedBandits ? banditVictoryRewards : banditPassiveVictoryRewards,
                    playerSupportedBandits);
            }
            else if (message.Event == claimBanditCampRewardEvent)
            {
                ClaimReward(BanditCampRewardClaimedFlag, banditCampRewardClaimedFlag, banditCampRewards, false);
            }
        }

        private void BeginAssault()
        {
            if (stage != MillScenarioStage.GuardsPrepared)
            {
                return;
            }

            QuestController quests = dialogueContext?.PlayerQuestController;
            if (quests != null)
            {
                quests.TrySetCurrentNode(guardInvestigationQuest, guardHelpRetakeMillNode);
            }

            SetStage(MillScenarioStage.GuardsMarching);
            SetExitAvailability(false);
            IssueMarchOrders();
        }

        private void IssueMarchOrders()
        {
            if (assaultOrdersIssued)
            {
                return;
            }

            assaultOrdersIssued = true;
            int index = 0;
            foreach (Transform guard in GetGuardParticipants())
            {
                Transform destination = index < guardAttackDestinations.Length
                    ? guardAttackDestinations[index]
                    : varekHolt;
                index++;
                if (guard == null || destination == null || !guard.gameObject.activeInHierarchy)
                {
                    continue;
                }

                GetScope(guard)?.Container.Resolve<NpcNavMeshController>()?.MoveTo(destination.position, stoppingDistance: 2.5f);
            }
        }

        private void TryStartAssaultAfterArrival()
        {
            if (!assaultOrdersIssued || !AllLivingGuardsAtDestinations())
            {
                return;
            }

            TargetLockTarget banditTarget = GetTarget(varekHolt);
            foreach (Transform guard in GetGuardParticipants())
            {
                if (IsAlive(guard))
                {
                    GetScope(guard)?.Container.Resolve<NpcCombatService>()?.ReceiveAggressionNotification(banditTarget, true);
                }
            }

            SetStage(MillScenarioStage.AssaultInProgress);
        }

        private bool AllLivingGuardsAtDestinations()
        {
            bool anyLivingGuard = false;
            foreach (Transform guard in GetGuardParticipants())
            {
                if (!IsAlive(guard))
                {
                    continue;
                }

                anyLivingGuard = true;
                NpcNavMeshController movement = GetScope(guard)?.Container.Resolve<NpcNavMeshController>();
                if (movement != null && !movement.HasReachedDestination)
                {
                    return false;
                }
            }

            return anyLivingGuard;
        }

        private void ResolveBattleIfFinished()
        {
            if (AllDefeated(bandits, varekHolt))
            {
                SetStage(MillScenarioStage.Liberated);
                CompleteActiveMillQuest();
                runtimeFlags?.Activate(guardVictoryFlag);
                SetExitAvailability(true);
                return;
            }

            if (AllDefeated(guards, squadLeader) && stage != MillScenarioStage.BanditsHeldMill)
            {
                SetStage(MillScenarioStage.BanditsHeldMill);
                if ((outcomeFlags & GuardSupportFlag) == 0)
                {
                    dialogueContext?.PlayerQuestController?.TrySetCurrentNode(
                        guardInvestigationQuest,
                        guardAssaultFailedNode);
                }
                runtimeFlags?.Activate(banditVictoryFlag);
                SetExitAvailability(true);
            }
        }

        private void OnCharacterDamaged(CharacterDamagedMessage message)
        {
            if (stage is not (MillScenarioStage.GuardsMarching or MillScenarioStage.AssaultInProgress) || message.Attacker == null)
            {
                return;
            }

            if (message.Attacker.OwnerTransform?.GetComponentInParent<PlayerLifetimeScope>() == null)
            {
                return;
            }

            if (BelongsTo(message.CharacterTransform, bandits, varekHolt))
            {
                outcomeFlags |= GuardSupportFlag;
                runtimeFlags?.Activate(playerSupportedGuardsFlag);
            }
            else if (BelongsTo(message.CharacterTransform, guards, squadLeader))
            {
                outcomeFlags |= BanditSupportFlag;
                runtimeFlags?.Activate(playerSupportedBanditsFlag);
            }
        }

        private void SetStage(MillScenarioStage nextStage)
        {
            if (stage == nextStage)
            {
                return;
            }

            stage = nextStage;
            ApplyWorldState(stage, true);
            saveController.SetMillScenarioState((int)stage, new[] { outcomeFlags });
        }

        private void ApplyWorldState(MillScenarioStage currentStage, bool isRuntimeTransition)
        {
            bool liberated = currentStage is MillScenarioStage.Liberated or MillScenarioStage.Ransomed;
            bool guardEncounterVisible = currentStage is MillScenarioStage.GuardsPrepared or MillScenarioStage.GuardsMarching or MillScenarioStage.AssaultInProgress;
            bool retainRuntimeParticipants = isRuntimeTransition &&
                                             (currentStage is MillScenarioStage.Liberated or MillScenarioStage.BanditsHeldMill);
            bool banditsVisible = !liberated || retainRuntimeParticipants;

            SetActive(ulrik, liberated);
            SetActive(varekHolt, banditsVisible);
            foreach (Transform bandit in bandits)
            {
                SetActive(bandit, banditsVisible);
            }

            SetActive(squadLeader, guardEncounterVisible || retainRuntimeParticipants);
            foreach (Transform guard in guards)
            {
                SetActive(guard, guardEncounterVisible || retainRuntimeParticipants);
            }

            foreach (FarmField field in millFields)
            {
                if (field != null)
                {
                    field.enabled = liberated;
                }
            }

            if (!isRuntimeTransition && (currentStage is MillScenarioStage.GuardsMarching or MillScenarioStage.AssaultInProgress))
            {
                SetExitAvailability(false);
                assaultOrdersIssued = false;
                IssueMarchOrders();
            }
            else if (currentStage is MillScenarioStage.Liberated or MillScenarioStage.BanditsHeldMill or MillScenarioStage.Ransomed)
            {
                SetExitAvailability(true);
            }
        }

        private void RestoreOutcomeFlags()
        {
            if ((outcomeFlags & GuardSupportFlag) != 0)
            {
                runtimeFlags?.Activate(playerSupportedGuardsFlag);
            }

            if ((outcomeFlags & BanditSupportFlag) != 0)
            {
                runtimeFlags?.Activate(playerSupportedBanditsFlag);
            }

            if ((outcomeFlags & GuardRewardClaimedFlag) != 0)
            {
                runtimeFlags?.Activate(guardRewardClaimedFlag);
            }

            if ((outcomeFlags & BanditRewardClaimedFlag) != 0)
            {
                runtimeFlags?.Activate(banditRewardClaimedFlag);
            }

            if ((outcomeFlags & BanditCampRewardClaimedFlag) != 0)
            {
                runtimeFlags?.Activate(banditCampRewardClaimedFlag);
            }

            if (stage == MillScenarioStage.Liberated)
            {
                runtimeFlags?.Activate(guardVictoryFlag);
            }
            else if (stage == MillScenarioStage.BanditsHeldMill)
            {
                runtimeFlags?.Activate(banditVictoryFlag);
            }
        }

        private void EnsurePreAssaultNeutrality()
        {
            if (stage > MillScenarioStage.GuardsPrepared || outcomeFlags != 0 ||
                playerFaction == null || banditFaction == null || factionRelations == null)
            {
                return;
            }

            int relation = factionRelations.GetRelation(playerFaction, banditFaction);
            if (relation != 0)
            {
                factionRelations.TryChangeRelation(playerFaction, banditFaction, -relation);
            }
        }

        private void ClaimReward(
            int rewardFlag,
            DialogueRuntimeFlag claimedFlag,
            IReadOnlyList<QuestResourceEntry> rewards,
            bool improveBanditStanding)
        {
            if ((outcomeFlags & rewardFlag) != 0)
            {
                return;
            }

            QuestController quests = dialogueContext?.PlayerQuestController;
            if (quests == null || !quests.TryGrantResources(rewards))
            {
                return;
            }

            if (improveBanditStanding)
            {
                factionRelations?.TryChangeRelation(playerFaction, banditFaction, banditReputationGain);
            }

            outcomeFlags |= rewardFlag;
            runtimeFlags?.Activate(claimedFlag);
            saveController.SetMillScenarioState((int)stage, new[] { outcomeFlags });
        }

        private void CompleteActiveMillQuest()
        {
            QuestController quests = dialogueContext?.PlayerQuestController;
            if (quests == null)
            {
                return;
            }

            QuestNodeData currentNode = quests.GetCurrentNode(guardInvestigationQuest);
            if (currentNode != null)
            {
                quests.TryCompleteNode(guardInvestigationQuest, currentNode);
            }
        }

        private void SetExitAvailability(bool isAvailable)
        {
            foreach (GameObject exitZone in locationExitZones)
            {
                if (exitZone != null)
                {
                    exitZone.SetActive(isAvailable);
                }
            }
        }

        private static bool BelongsTo(Transform character, Transform[] group, Transform leader)
        {
            if (character == null)
            {
                return false;
            }

            if (leader != null && character.IsChildOf(leader))
            {
                return true;
            }

            foreach (Transform member in group)
            {
                if (member != null && character.IsChildOf(member))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AllDefeated(Transform[] group, Transform leader)
        {
            if (!IsDefeated(leader))
            {
                return false;
            }

            foreach (Transform member in group)
            {
                if (!IsDefeated(member))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAlive(Transform transform) => !IsDefeated(transform);

        private static bool IsDefeated(Transform transform)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy)
            {
                return true;
            }

            CharacterDamageReceiver receiver = transform.GetComponentInChildren<DamageReceiverHost>(true)?.Receiver;
            return receiver != null && !receiver.IsAlive;
        }

        private System.Collections.Generic.IEnumerable<Transform> GetGuardParticipants()
        {
            if (squadLeader != null)
            {
                yield return squadLeader;
            }

            foreach (Transform guard in guards)
            {
                if (guard != null && guard != squadLeader)
                {
                    yield return guard;
                }
            }
        }

        private static TargetLockTarget GetTarget(Transform transform) =>
            transform != null ? transform.GetComponent<TargetLockTarget>() : null;

        private static NpcLifetimeScope GetScope(Transform transform) =>
            transform != null ? transform.GetComponent<NpcLifetimeScope>() : null;

        private static void SetActive(Transform transform, bool isActive)
        {
            if (transform != null)
            {
                transform.gameObject.SetActive(isActive);
            }
        }
    }

    public enum MillScenarioStage
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

    [Flags]
    public enum MillScenarioOutcomeFlags
    {
        None = 0,
        PlayerSupportedGuards = 1 << 0,
        PlayerSupportedBandits = 1 << 1,
        GuardRewardClaimed = 1 << 2,
        BanditRewardClaimed = 1 << 3,
        BanditCampRewardClaimed = 1 << 4
    }
}
