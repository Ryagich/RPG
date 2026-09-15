using Container;
using Dialogue;
using Dialogs.Graph;
using Dialogs.Graph.Model;
using Interactable;
using Inventory.Inventories;
using Money;
using Quests;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace NPC
{
    [DisallowMultipleComponent]
    public sealed class NpcForcedDialogueAvailability : MonoBehaviour, IInteractableAvailability
    {
        [SerializeField, Tooltip("Allows this automatic dialogue zone to open a forced dialogue only once per GameObject activation.")]
        private bool allowOnlyOneForcedDialoguePerActivation;

        private NpcDialogueController dialogueController;
        private DialogueContext dialogueContext;
        private DialogGraph dialog;
        private DialogueRuntimeFlagRegistry runtimeFlags;
        private bool isSuppressedUntilZoneExit;
        private bool hasOpenedForcedDialogueThisActivation;

        private void OnEnable()
        {
            hasOpenedForcedDialogueThisActivation = false;
        }

        [Inject]
        public void Construct(
            NpcDialogueController dialogueController,
            DialogueContext dialogueContext,
            DialogueRuntimeFlagRegistry runtimeFlags,
            IObjectResolver resolver)
        {
            this.dialogueController = dialogueController;
            this.dialogueContext = dialogueContext;
            this.runtimeFlags = runtimeFlags;
            // A forced dialogue is optional for an NPC. VContainer treats an [Inject]
            // method parameter as required even if it has a C# default value, therefore
            // resolving DialogGraph directly here could abort the whole NPC scope.
            dialog = resolver.TryResolve<DialogGraph>(out var resolvedDialog)
                ? resolvedDialog
                : null;
        }

        public bool IsInteractableAvailable(LifetimeScope interactorScope)
        {
            return TryGetForcedPhrase(interactorScope, out _);
        }

        public void SuppressUntilZoneExit()
        {
            isSuppressedUntilZoneExit = true;
            DialogueFlowTrace.ForcedZoneState("suppressed-until-exit");
        }

        public void NotifyInteractorLeftZone()
        {
            isSuppressedUntilZoneExit = false;
            DialogueFlowTrace.ForcedZoneState("interactor-left-zone");
        }

        public void AllowImmediateNextDialogue()
        {
            isSuppressedUntilZoneExit = false;
            DialogueFlowTrace.ForcedZoneState("immediate-next-dialogue-allowed");
        }

        public void NotifyForcedDialogueOpened()
        {
            if (!allowOnlyOneForcedDialoguePerActivation)
            {
                return;
            }

            hasOpenedForcedDialogueThisActivation = true;
            DialogueFlowTrace.ForcedZoneState("forced-dialogue-consumed-for-activation");
        }

        public bool TryGetForcedPhrase(LifetimeScope interactorScope, out DialogPhrase forcedPhrase)
        {
            forcedPhrase = null;
            if (isSuppressedUntilZoneExit ||
                (allowOnlyOneForcedDialoguePerActivation && hasOpenedForcedDialogueThisActivation) ||
                dialog == null || dialogueContext?.CurrentTarget != null ||
                dialogueController == null || !dialogueController.CanStartDialogue(interactorScope) ||
                interactorScope is not PlayerLifetimeScope)
            {
                return false;
            }

            PlayerInventory playerInventory = interactorScope.Container.Resolve<PlayerInventory>();
            MoneyStorage playerMoneyStorage = interactorScope.Container.Resolve<MoneyStorage>();
            // The dialogue UI owns the canonical quest controller used by the quest journal
            // and its notifications. A forced dialogue must test the same instance; otherwise
            // the trigger and the journal can observe different quest states.
            QuestController questController = dialogueContext.PlayerQuestController;
            if (questController == null)
            {
                return false;
            }

            if (!dialog.TryGetActiveForcedPhrase(
                    answer => DialogueAnswerAvailability.AreConditionsSatisfied(
                        answer.HasConditions,
                        answer.Conditions,
                        playerInventory,
                        playerMoneyStorage,
                        questController,
                        runtimeFlags),
                    out forcedPhrase))
            {
                return false;
            }

            // This availability belongs to the automatic forced-dialogue zone.  A graph's
            // regular entry phrase is valid only for the manual NPC interaction, even when
            // it is the only currently available phrase.
            if (forcedPhrase == null || !forcedPhrase.IsForcedDialoguePhrase)
            {
                DialogueFlowTrace.ForcedZoneState(
                    $"rejected-non-forced-phrase; phrase='{forcedPhrase?.name ?? "<none>"}'");
                forcedPhrase = null;
                return false;
            }

            return true;
        }
    }
}
