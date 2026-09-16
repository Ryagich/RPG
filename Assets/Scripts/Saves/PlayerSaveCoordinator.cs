using Inventory.Inventories;
using Inventory.Storage;
using Dialogue;
using Money;
using Quests;
using Stats;
using UnityEngine;
using VContainer.Unity;
using Container.Game;

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
        private readonly GameSceneSessionConfiguration sceneSessionConfiguration;
        private bool isPlayerStateRegistered;

        public PlayerSaveCoordinator(
            GameSaveController saveController,
            ItemStorage itemStorage,
            VContainer.IObjectResolver resolver,
            PlayerInventory inventory,
            MoneyStorage money,
            StatsController stats,
            QuestController quests,
            DialogueContext dialogueContext,
            Transform playerTransform,
            GameSceneSessionConfiguration sceneSessionConfiguration)
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
            this.sceneSessionConfiguration = sceneSessionConfiguration;
        }

        public async void Start()
        {
            dialogueContext.SetPlayerQuestController(quests);
            if (!sceneSessionConfiguration.IsGameplayScene)
            {
                dialogueContext.NotifyPlayerQuestControllerReady(quests);
                return;
            }

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
            isPlayerStateRegistered = true;
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
            if (isPlayerStateRegistered)
            {
                saveController.UnregisterPlayerState(quests);
            }
        }
    }
}
