using System;
using System.Collections.Generic;
using Character;
using Factions;
using GameModes;
using Inventory;
using Inventory.Inventories;
using Inventory.Looting;
using MessagePipe;
using Messages;
using VContainer;
using VContainer.Unity;

namespace Container.Chest
{
    public class ChestInteractableLogic : IStartable, IDisposable
    {
        private readonly Interactable.Interactable interactable;
        private readonly IInventory chestInventory;
        private readonly CharacterInfo characterInfo;
        private readonly FactionConfig faction;
        private readonly LootingContext lootingContext;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly ChestLidAnimator lidAnimator;
        private readonly HashSet<LifetimeScope> activeInteractors = new();

        public ChestInteractableLogic(Interactable.Interactable interactable, IInventory chestInventory, CharacterInfo characterInfo,
            LootingContext lootingContext, IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher, IObjectResolver resolver)
        {
            this.interactable = interactable;
            this.chestInventory = chestInventory;
            this.characterInfo = characterInfo;
            faction = resolver.TryResolve<FactionConfig>(out var resolvedFaction) ? resolvedFaction : null;
            lidAnimator = resolver.TryResolve<ChestLidAnimator>(out var resolvedLidAnimator) ? resolvedLidAnimator : null;
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
            activeInteractors.Clear();
        }

        private void OnInteracted(LifetimeScope interactorScope)
        {
            if (interactorScope == null || !activeInteractors.Add(interactorScope)) return;
            if (activeInteractors.Count == 1) lidAnimator?.Open();
            lootingContext.SetTarget(chestInventory, characterInfo, faction: faction);
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Looting));
        }

        private void OnEndInteracted(LifetimeScope interactorScope)
        {
            if (interactorScope == null || !activeInteractors.Remove(interactorScope)) return;
            if (activeInteractors.Count == 0) lidAnimator?.Close();
            lootingContext.Clear();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }
    }
}
