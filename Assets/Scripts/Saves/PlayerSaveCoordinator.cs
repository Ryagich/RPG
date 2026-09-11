using Inventory.Inventories;
using Inventory.Storage;
using Dialogue;
using Money;
using Quests;
using Stats;
using UnityEngine;
using VContainer.Unity;

namespace Saves
{
    /// <summary>Registers a recreated player scope before its saved state is restored.</summary>
    public sealed class PlayerSaveCoordinator : IStartable, System.IDisposable
    {
        private readonly GameSaveController saveController;
        private readonly ItemStorage itemStorage;
        private readonly VContainer.IObjectResolver resolver;
        private readonly PlayerInventory inventory;
        private readonly MoneyStorage money;
        private readonly StatsController stats;
        private readonly QuestController quests;
        private readonly DialogueContext dialogueContext;
        private readonly Transform playerTransform;

        public PlayerSaveCoordinator(
            GameSaveController saveController,
            ItemStorage itemStorage,
            VContainer.IObjectResolver resolver,
            PlayerInventory inventory,
            MoneyStorage money,
            StatsController stats,
            QuestController quests,
            DialogueContext dialogueContext,
            Transform playerTransform)
        {
            this.saveController = saveController;
            this.itemStorage = itemStorage;
            this.resolver = resolver;
            this.inventory = inventory;
            this.money = money;
            this.stats = stats;
            this.quests = quests;
            this.dialogueContext = dialogueContext;
            this.playerTransform = playerTransform;
        }

        public async void Start()
        {
            dialogueContext.SetPlayerQuestController(quests);
            await saveController.Ready;
            if (!await itemStorage.Ready)
            {
                Debug.LogError("Player state was not registered for saving because the item catalogue failed to load.");
                return;
            }
            QuestCatalog questCatalog = resolver.TryResolve(typeof(QuestCatalog), out object catalog, null)
                ? catalog as QuestCatalog
                : null;
            saveController.RegisterPlayerState(inventory, money, stats, quests, itemStorage, questCatalog, playerTransform);
            bool restored = saveController.RestoreRegisteredPlayer();
            if (restored)
            {
                while (quests.TryExecuteAvailableAutomaticTransition())
                {
                }

                quests.NotifyStateRestored();
            }
            if (!object.ReferenceEquals(dialogueContext.PlayerQuestController, quests))
            {
                Debug.LogError("Dialogue and player save scopes resolved different quest controllers.");
                return;
            }

            dialogueContext.NotifyPlayerQuestControllerReady(quests);

        }

        public void Dispose()
        {
            saveController.UnregisterPlayerState(quests);
        }
    }
}
