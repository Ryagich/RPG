using System;
using GameModes;
using Interactable;
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
        private readonly LootingContext lootingContext;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;

        public ChestInteractableLogic
            (
                Interactable.Interactable interactable,
                ChestInventory chestInventory,
                LootingContext lootingContext,
                IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher
            )
        {
            this.interactable = interactable;
            this.chestInventory = chestInventory;
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
            lootingContext.SetTarget(chestInventory);
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Looting));
        }

        private void OnEndInteracted(LifetimeScope _)
        {
            lootingContext.Clear();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}