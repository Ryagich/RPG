using System;
using Character;
using GameModes;
using Factions;
using Inventory.Inventories;
using Inventory.Looting;
using MessagePipe;
using Messages;
using VContainer.Unity;

namespace Container.Chest
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ChestInteractableLogic : IStartable, IDisposable
    {
        private readonly Interactable.Interactable interactable;
        private readonly ChestInventory chestInventory;
        private readonly CharacterInfo characterInfo;
        private readonly FactionConfig faction;
        private readonly LootingContext lootingContext;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        public ChestInteractableLogic
            (
                Interactable.Interactable interactable,
                ChestInventory chestInventory,
                CharacterInfo characterInfo,
                LootingContext lootingContext,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
                FactionConfig faction = null
            )
        {
            this.interactable = interactable;
            this.chestInventory = chestInventory;
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
            lootingContext.SetTarget(chestInventory, characterInfo, faction: faction);
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Looting));
        }

        private void OnEndInteracted(LifetimeScope _)
        {
            lootingContext.Clear();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}
