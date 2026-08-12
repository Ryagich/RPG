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
        private NpcDialogueController dialogueController;
        private DialogueContext dialogueContext;
        private DialogGraph dialog;
        private DialogueRuntimeFlagRegistry runtimeFlags;
        private bool isSuppressedUntilZoneExit;

        [Inject]
        public void Construct(
            NpcDialogueController dialogueController,
            DialogueContext dialogueContext,
            DialogueRuntimeFlagRegistry runtimeFlags,
            DialogGraph dialog = null)
        {
            this.dialogueController = dialogueController;
            this.dialogueContext = dialogueContext;
            this.runtimeFlags = runtimeFlags;
            this.dialog = dialog;
        }

        public bool IsInteractableAvailable(LifetimeScope interactorScope)
        {
            return TryGetForcedPhrase(interactorScope, out _);
        }

        public void SuppressUntilZoneExit()
        {
            isSuppressedUntilZoneExit = true;
        }

        public void NotifyInteractorLeftZone()
        {
            isSuppressedUntilZoneExit = false;
        }

        public void AllowImmediateNextDialogue()
        {
            isSuppressedUntilZoneExit = false;
        }

        public bool TryGetForcedPhrase(LifetimeScope interactorScope, out DialogPhrase forcedPhrase)
        {
            forcedPhrase = null;
            if (isSuppressedUntilZoneExit || dialog == null || dialogueContext?.CurrentTarget != null ||
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

            return dialog.TryGetActiveForcedPhrase(
                answer => DialogueAnswerAvailability.AreConditionsSatisfied(
                    answer.HasConditions,
                    answer.Conditions,
                    playerInventory,
                    playerMoneyStorage,
                    questController,
                    runtimeFlags),
                out forcedPhrase);
        }
    }
}
