using Combat;
using Container;
using Dialogue;
using Locations;
using MessagePipe;
using Messages;
using NPC;
using TargetLock;
using UnityEngine;
using VContainer;

namespace Forest.Bandits
{
    /// <summary>
    /// Owns the Bram Voss road-toll encounter only. It deliberately receives authored references
    /// to the roadside group, so faction-wide combat or the distant bandit camp are never affected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ForestTollEncounterController : MonoBehaviour
    {
        [Header("Roadside encounter")]
        [SerializeField] private NpcLifetimeScope bramVoss;
        [SerializeField] private NpcLifetimeScope[] roadsideBandits;
        [SerializeField] private GameObject invisibleWallToForest;
        [SerializeField] private GameObject invisibleWallToTavern;
        [Header("Dialogue contracts")]
        [SerializeField] private DialogueGameplayEvent paidTollEvent;
        [SerializeField] private DialogueGameplayEvent threatenBanditsEvent;
        [SerializeField] private DialogueRuntimeFlag tollPaidFlag;
        [SerializeField] private DialogueRuntimeFlag encounterClearedFlag;

        private LocationTransitionService locationTransitions;
        private DialogueRuntimeFlagRegistry runtimeFlags;
        private ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEventSubscriber;
        private System.IDisposable dialogueEventSubscription;
        private bool isResolved;

        [Inject]
        public void Construct(
            LocationTransitionService locationTransitions,
            DialogueRuntimeFlagRegistry runtimeFlags,
            ISubscriber<DialogueGameplayEventRaisedMessage> dialogueEventSubscriber)
        {
            this.locationTransitions = locationTransitions;
            this.runtimeFlags = runtimeFlags;
            this.dialogueEventSubscriber = dialogueEventSubscriber;
        }

        private void Start()
        {
            dialogueEventSubscription = dialogueEventSubscriber?.Subscribe(OnDialogueGameplayEvent);
            ApplyArrivalWalls();
        }

        private void Update()
        {
            if (!isResolved && IsRoadsideGroupDefeated())
            {
                ResolveEncounter();
            }
        }

        private void OnDestroy()
        {
            dialogueEventSubscription?.Dispose();
            dialogueEventSubscription = null;
        }

        private void OnDialogueGameplayEvent(DialogueGameplayEventRaisedMessage message)
        {
            if (message.Event == paidTollEvent)
            {
                runtimeFlags?.Activate(tollPaidFlag);
                ResolveEncounter();
            }
            else if (message.Event == threatenBanditsEvent)
            {
                MakeRoadsideGroupHostile();
            }
        }

        private void ApplyArrivalWalls()
        {
            if (runtimeFlags?.IsActive(tollPaidFlag) == true || runtimeFlags?.IsActive(encounterClearedFlag) == true)
            {
                ResolveEncounter();
                return;
            }

            // The current entrance belongs to the generic transition system. This encounter only
            // interprets its authored Forest entry IDs and never changes transition behaviour.
            bool enteredForestFromTavern = locationTransitions?.CurrentLocation?.Id == "Forest"
                                          && locationTransitions.CurrentEntrance?.Id == "To Tavern";
            SetWallState(enteredForestFromTavern, !enteredForestFromTavern);
        }

        private void ResolveEncounter()
        {
            isResolved = true;
            runtimeFlags?.Activate(encounterClearedFlag);
            SetWallState(false, false);
        }

        private void MakeRoadsideGroupHostile()
        {
            PlayerLifetimeScope playerScope = FindFirstObjectByType<PlayerLifetimeScope>();
            TargetLockTarget playerTarget = playerScope != null
                ? playerScope.Container.Resolve<TargetLockTarget>()
                : null;
            if (playerTarget == null)
            {
                Debug.LogError("Forest toll cannot start combat: player TargetLockTarget was not found.", this);
                return;
            }

            MakeHostile(bramVoss, playerTarget);
            if (roadsideBandits == null)
            {
                return;
            }

            foreach (var bandit in roadsideBandits)
            {
                MakeHostile(bandit, playerTarget);
            }
        }

        private static void MakeHostile(NpcLifetimeScope npc, TargetLockTarget playerTarget)
        {
            if (npc == null || !npc.gameObject.activeInHierarchy)
            {
                return;
            }

            npc.Container.Resolve<NpcCombatService>()?.ReceiveAggressionNotification(playerTarget, true);
        }

        private bool IsRoadsideGroupDefeated()
        {
            return IsDefeated(bramVoss) && AllRoadsideBanditsDefeated();
        }

        private bool AllRoadsideBanditsDefeated()
        {
            if (roadsideBandits == null || roadsideBandits.Length == 0)
            {
                return false;
            }

            foreach (var bandit in roadsideBandits)
            {
                if (bandit == null || !IsDefeated(bandit))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsDefeated(NpcLifetimeScope npc)
        {
            if (npc == null || !npc.gameObject.activeInHierarchy)
            {
                return true;
            }

            var receiver = npc.GetComponent<DamageReceiverHost>()?.Receiver;
            return receiver != null && !receiver.IsAlive;
        }

        private void SetWallState(bool forestWallActive, bool tavernWallActive)
        {
            if (invisibleWallToForest != null)
            {
                invisibleWallToForest.SetActive(forestWallActive);
            }

            if (invisibleWallToTavern != null)
            {
                invisibleWallToTavern.SetActive(tavernWallActive);
            }
        }
    }
}
