using System;
using Character;
using Dialogue;
using Dialogs.Graph;
using Factions;
using GameModes;
using Inventory.Inventories;
using Inventory.Looting;
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
        private readonly FactionConfig faction;
        private readonly DialogueContext dialogueContext;
        private readonly LootingContext lootingContext;
        private readonly CorpseLootController corpseLootController;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private bool isLootingInteractionActive;

        public NpcDialogueInteractableLogic(
            Interactable.Interactable interactable,
            NpcDialogueController dialogueController,
            IInventory inventory,
            MoneyStorage moneyStorage,
            DialogueContext dialogueContext,
            LootingContext lootingContext,
            CorpseLootController corpseLootController,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
            CharacterInfo characterInfo = null,
            DialogGraph dialog = null,
            FactionConfig faction = null)
        {
            this.interactable = interactable;
            this.dialogueController = dialogueController;
            this.characterInfo = characterInfo;
            this.dialog = dialog;
            this.inventory = inventory;
            this.moneyStorage = moneyStorage;
            this.faction = faction;
            this.dialogueContext = dialogueContext;
            this.lootingContext = lootingContext;
            this.corpseLootController = corpseLootController;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
        }

        public void Start()
        {
            interactable.Interacted += OnInteracted;
            interactable.EndInteracted += OnEndInteracted;
            interactable.EndManualInteracted += OnEndInteracted;
            dialogueController.DialogueInterrupted += OnDialogueInterrupted;
        }

        public void Dispose()
        {
            interactable.Interacted -= OnInteracted;
            interactable.EndInteracted -= OnEndInteracted;
            interactable.EndManualInteracted -= OnEndInteracted;
            dialogueController.DialogueInterrupted -= OnDialogueInterrupted;
        }

        private void OnInteracted(LifetimeScope interactorScope)
        {
            if (corpseLootController?.IsLootable == true && corpseLootController.LootInventory != null)
            {
                isLootingInteractionActive = true;
                lootingContext.SetTarget(corpseLootController.LootInventory, characterInfo, faction: faction);
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Looting));
                return;
            }

            if (!dialogueController.TryBeginDialogue(interactorScope))
            {
                return;
            }

            dialogueContext.SetTarget(interactable, characterInfo, dialog, inventory, moneyStorage, faction: faction);
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }

        private void OnEndInteracted(LifetimeScope _)
        {
            if (isLootingInteractionActive)
            {
                if (lootingContext.CurrentTargetInventory == corpseLootController?.LootInventory)
                {
                    lootingContext.Clear();
                }

                isLootingInteractionActive = false;
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                return;
            }

            if (dialogueContext.CurrentTarget == interactable)
            {
                dialogueContext.Clear();
            }

            dialogueController.EndDialogue();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }

        private void OnDialogueInterrupted()
        {
            if (dialogueContext.CurrentTarget == interactable)
            {
                dialogueContext.Clear();
            }

            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}
