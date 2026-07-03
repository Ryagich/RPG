using Interactable;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace NPC
{
    [DisallowMultipleComponent]
    public sealed class NpcDialogueAvailability : MonoBehaviour, IInteractableAvailability
    {
        private NpcDialogueController dialogueController;

        [Inject]
        public void Construct(NpcDialogueController dialogueController)
        {
            this.dialogueController = dialogueController;
        }

        public bool IsInteractableAvailable(LifetimeScope interactorScope)
        {
            return dialogueController != null && dialogueController.CanStartDialogue(interactorScope);
        }
    }
}
