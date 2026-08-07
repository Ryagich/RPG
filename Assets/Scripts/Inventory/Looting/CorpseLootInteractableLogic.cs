using System;
using Character;
using GameModes;
using Factions;
using MessagePipe;
using Messages;
using VContainer.Unity;

namespace Inventory.Looting
{
    public sealed class CorpseLootInteractableLogic : IStartable, IDisposable
    {
        private readonly Interactable.Interactable interactable;
        private readonly CorpseLootController corpseLootController;
        private readonly CharacterInfo characterInfo;
        private readonly FactionConfig faction;
        private readonly LootingContext lootingContext;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private bool isLootingInteractionActive;

        public CorpseLootInteractableLogic(
            Interactable.Interactable interactable,
            CorpseLootController corpseLootController,
            LootingContext lootingContext,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
            CharacterInfo characterInfo = null,
            FactionConfig faction = null)
        {
            this.interactable = interactable;
            this.corpseLootController = corpseLootController;
            this.characterInfo = characterInfo;
            this.faction = faction;
            this.lootingContext = lootingContext;
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
            if (corpseLootController?.IsLootable != true || corpseLootController.LootInventory == null)
            {
                return;
            }

            isLootingInteractionActive = true;
            lootingContext.SetTarget(corpseLootController.LootInventory, characterInfo, faction: faction);
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Looting));
        }

        private void OnEndInteracted(LifetimeScope _)
        {
            if (!isLootingInteractionActive)
            {
                return;
            }

            if (lootingContext.CurrentTargetInventory == corpseLootController?.LootInventory)
            {
                lootingContext.Clear();
            }

            isLootingInteractionActive = false;
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}
