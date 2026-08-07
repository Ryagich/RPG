using System;
using Character;
using Dialogue;
using Dialogs.Graph;
using Dialogs.Graph.Model;
using Factions;
using GameModes;
using Inventory.Inventories;
using MessagePipe;
using Messages;
using Money;
using VContainer;
using VContainer.Unity;

namespace NPC
{
    public sealed class NpcForcedDialogueInteractableLogic : IStartable, IDisposable
    {
        private readonly Interactable.Interactable interactable;
        private readonly NpcForcedDialogueAvailability availability;
        private readonly NpcDialogueController dialogueController;
        private readonly CharacterInfo characterInfo;
        private readonly DialogGraph dialog;
        private readonly IInventory inventory;
        private readonly MoneyStorage moneyStorage;
        private readonly FactionConfig faction;
        private readonly DialogueContext dialogueContext;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;
        private IDisposable gameModeChangedSubscription;

        public NpcForcedDialogueInteractableLogic(
            [Key("Forced Dialogue Interactable")] Interactable.Interactable interactable,
            NpcForcedDialogueAvailability availability,
            NpcDialogueController dialogueController,
            IInventory inventory,
            MoneyStorage moneyStorage,
            DialogueContext dialogueContext,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber,
            IObjectResolver resolver,
            CharacterInfo characterInfo = null,
            DialogGraph dialog = null)
        {
            this.interactable = interactable;
            this.availability = availability;
            this.dialogueController = dialogueController;
            this.characterInfo = characterInfo;
            this.dialog = dialog;
            this.inventory = inventory;
            this.moneyStorage = moneyStorage;
            faction = resolver.TryResolve<FactionConfig>(out var resolvedFaction) ? resolvedFaction : null;
            this.dialogueContext = dialogueContext;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
        }

        public void Start()
        {
            if (interactable == null)
            {
                return;
            }

            interactable.Interacted += OnInteracted;
            interactable.EndInteracted += OnEndInteracted;
            dialogueController.DialogueInterrupted += OnDialogueInterrupted;
            gameModeChangedSubscription = gameModeChangedSubscriber.Subscribe(OnGameModeChanged);
        }

        public void Dispose()
        {
            if (interactable != null)
            {
                interactable.Interacted -= OnInteracted;
                interactable.EndInteracted -= OnEndInteracted;
            }

            dialogueController.DialogueInterrupted -= OnDialogueInterrupted;
            gameModeChangedSubscription?.Dispose();
            gameModeChangedSubscription = null;
        }

        private void OnInteracted(LifetimeScope interactorScope)
        {
            if (!availability.TryGetForcedPhrase(interactorScope, out DialogPhrase forcedPhrase) ||
                !dialogueController.TryBeginDialogue(interactorScope))
            {
                return;
            }

            dialogueContext.SetTarget(
                interactable,
                characterInfo,
                dialog,
                inventory,
                moneyStorage,
                forcedPhrase,
                true,
                faction);
            availability.SuppressUntilZoneExit();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Dialogue));
        }

        private void OnEndInteracted(LifetimeScope _)
        {
            availability.NotifyInteractorLeftZone();

            if (dialogueContext.CurrentTarget != interactable || !dialogueContext.CanExitDialogue)
            {
                return;
            }

            dialogueContext.Clear();
            dialogueController.EndDialogue();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }

        private void OnGameModeChanged(GameModeChangedMessage message)
        {
            if (message.GameMode != GameMode.Game || dialogueContext.CurrentTarget != interactable ||
                !dialogueContext.CanExitDialogue)
            {
                return;
            }

            dialogueContext.Clear();
            dialogueController.EndDialogue();
        }

        private void OnDialogueInterrupted()
        {
            if (dialogueContext.CurrentTarget != interactable)
            {
                return;
            }

            dialogueContext.Clear();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}
