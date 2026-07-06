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
            return corpseLootController?.IsLootable == true
                || dialogueController != null && dialogueController.CanStartDialogue(interactorScope);
        }
    }
}
