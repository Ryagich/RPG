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

namespace Container.Dialogue
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class DialogueInteractableLogic : IStartable, IDisposable
    {
        private readonly Interactable.Interactable interactable;
        private readonly CharacterInfo characterInfo;
        private readonly DialogGraph dialog;
        private readonly IInventory inventory;
        private readonly MoneyStorage moneyStorage;
        private readonly DialogueContext dialogueContext;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        public DialogueInteractableLogic
            (
                Interactable.Interactable interactable,
                CharacterInfo characterInfo,
                IInventory inventory,
                MoneyStorage moneyStorage,
                DialogueContext dialogueContext,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
                DialogGraph dialog = null
            )
        {
            this.interactable = interactable;
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

        private void OnInteracted(LifetimeScope _)
        {
            dialogueContext.SetTarget(interactable, characterInfo, dialog, inventory, moneyStorage);
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }

        private void OnEndInteracted(LifetimeScope _)
        {
            dialogueContext.Clear();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}
