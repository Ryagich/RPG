using System;
using Character;
using Dialogue;
using Dialogs.Graph;
using GameModes;
using Inventory.Inventories;
using MessagePipe;
using Messages;
using Money;
using VContainer.Unity;

namespace NPC
{
    public sealed class NpcDialogueInteractableLogic : IStartable, IDisposable
    {
        private readonly Interactable.Interactable interactable;
        private readonly NpcDialogueController dialogueController;
        private readonly CharacterInfo characterInfo;
        private readonly DialogGraph dialog;
        private readonly IInventory inventory;
        private readonly MoneyStorage moneyStorage;
        private readonly DialogueContext dialogueContext;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        public NpcDialogueInteractableLogic(
            Interactable.Interactable interactable,
            NpcDialogueController dialogueController,
            IInventory inventory,
            MoneyStorage moneyStorage,
            DialogueContext dialogueContext,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
            CharacterInfo characterInfo = null,
            DialogGraph dialog = null)
        {
            this.interactable = interactable;
            this.dialogueController = dialogueController;
            this.characterInfo = characterInfo;
            this.dialog = dialog;
            this.inventory = inventory;
            this.moneyStorage = moneyStorage;
            this.dialogueContext = dialogueContext;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
        }

        public void Start()
        {
            interactable.Interacted += OnInteracted;
            interactable.EndInteracted += OnEndInteracted;
            interactable.EndManualInteracted += OnEndInteracted;
        }

        public void Dispose()
        {
            interactable.Interacted -= OnInteracted;
            interactable.EndInteracted -= OnEndInteracted;
            interactable.EndManualInteracted -= OnEndInteracted;
        }

        private void OnInteracted(LifetimeScope interactorScope)
        {
            if (!dialogueController.TryBeginDialogue(interactorScope))
            {
                return;
            }

            dialogueContext.SetTarget(interactable, characterInfo, dialog, inventory, moneyStorage);
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }

        private void OnEndInteracted(LifetimeScope _)
        {
            if (dialogueContext.CurrentTarget == interactable)
            {
                dialogueContext.Clear();
            }

            dialogueController.EndDialogue();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}
