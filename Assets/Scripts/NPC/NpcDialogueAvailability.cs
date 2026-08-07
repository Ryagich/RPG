using Interactable;
using Inventory.Looting;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace NPC
{
    [DisallowMultipleComponent]
    public sealed class NpcDialogueAvailability : MonoBehaviour, IInteractableAvailability
    {
        private NpcDialogueController dialogueController;
        private CorpseLootController corpseLootController;

        [Inject]
        public void Construct(NpcDialogueController dialogueController, CorpseLootController corpseLootController)
        {
            this.dialogueController = dialogueController;
            this.corpseLootController = corpseLootController;
        }

        public bool IsInteractableAvailable(LifetimeScope interactorScope)
        {
            if (corpseLootController?.IsLootable == true)
            {
                return true;
            }

            // A manual interaction remains active while its dialogue is open. Without this,
            // CanStartDialogue returns false immediately after TryBeginDialogue, the player
            // interaction controller treats the NPC as out of range, and closes the dialogue
            // again on the next physics tick.
            return dialogueController != null &&
                   (dialogueController.IsDialogueRequested || dialogueController.CanStartDialogue(interactorScope));
        }
    }
}
