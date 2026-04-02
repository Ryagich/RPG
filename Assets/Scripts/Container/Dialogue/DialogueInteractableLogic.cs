using System;
using Character;
using Dialogue;
using GameModes;
using Inventory.Inventories;
using MessagePipe;
using Messages;
using VContainer.Unity;

namespace Container.Dialogue
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class DialogueInteractableLogic : IStartable, IDisposable
    {
        private readonly Interactable.Interactable interactable;
        private readonly CharacterInfo characterInfo;
        private readonly IInventory inventory;
        private readonly DialogueContext dialogueContext;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        public DialogueInteractableLogic
            (
                Interactable.Interactable interactable,
                CharacterInfo characterInfo,
                IInventory inventory,
                DialogueContext dialogueContext,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher
            )
        {
            this.interactable = interactable;
            this.characterInfo = characterInfo;
            this.inventory = inventory;
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

        private void OnInteracted(LifetimeScope _)
        {
            dialogueContext.SetTarget(interactable, characterInfo, inventory);
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }

        private void OnEndInteracted(LifetimeScope _)
        {
            dialogueContext.Clear();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}