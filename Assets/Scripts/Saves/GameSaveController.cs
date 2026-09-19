using System;
using Inventory.Inventories;
using Inventory.Storage;
using Locations;
using Money;
using Quests;
using Stats;
using UnityEngine;
using YG;

namespace Saves
{
    /// <summary>
    /// Application boundary for complete game checkpoints. It owns the currently recreated
    /// player state and is the only runtime entry point that writes or restores a save.
    /// </summary>
    public sealed class GameSaveController
    {
        private readonly GameSaveService saveService;
        private PlayerInventory inventory;
        private MoneyStorage money;
        private StatsController stats;
        private QuestController quests;
        private ItemStorage itemStorage;
        private QuestCatalog questCatalog;
        private Transform playerTransform;

        public GameSaveController(GameSaveService saveService)
        {
            this.saveService = saveService;
        }

        public System.Threading.Tasks.Task Ready => saveService.Ready;
        public bool HasSavedData => saveService.HasSavedData;
        internal event Action CheckpointPreparing;
        public event Action ReadyStateChanged
        {
            add => saveService.ReadyStateChanged += value;
            remove => saveService.ReadyStateChanged -= value;
        }

        public void RegisterPlayerState(
            PlayerInventory playerInventory,
            MoneyStorage playerMoney,
            StatsController playerStats,
            QuestController playerQuests,
            ItemStorage storage,
            QuestCatalog catalog,
            Transform transform)
        {
            inventory = playerInventory;
            money = playerMoney;
            stats = playerStats;
            quests = playerQuests;
            itemStorage = storage;
            questCatalog = catalog;
            playerTransform = transform;
        }

        public void UnregisterPlayerState(QuestController playerQuests)
        {
            if (quests != playerQuests)
            {
                return;
            }

            inventory = null;
            money = null;
            stats = null;
            quests = null;
            itemStorage = null;
            questCatalog = null;
            playerTransform = null;
        }

        public bool RestoreRegisteredPlayer()
        {
            return saveService.TryRestorePlayer(inventory, money, stats, quests, itemStorage, questCatalog);
        }

        public bool SaveFullAtCurrentPlayerPosition(string locationId, string entranceId)
        {
            if (playerTransform == null)
            {
                return false;
            }

            return SaveFullAtPosition(locationId, entranceId, new Pose(playerTransform.position, playerTransform.rotation));
        }

        public bool SaveFullAtPosition(string locationId, string entranceId, Pose playerPose)
        {
            CheckpointPreparing?.Invoke();
            return saveService.SavePlayer(
                locationId,
                entranceId,
                ToSavedPlayerPose(playerPose),
                inventory,
                money,
                stats,
                quests);
        }

        public void RestoreLocationTransition(LocationTransitionContext transitionContext)
        {
            saveService.RestoreLocationTransition(transitionContext);
        }

        public bool TryGetSavedPlayerPose(out Pose pose)
        {
            return saveService.TryGetSavedPlayerPose(out pose);
        }

        public bool TryGetSavedNpc(string characterId, out YG.SavedCharacterState savedState)
        {
            return saveService.TryGetSavedNpc(characterId, out savedState);
        }

        public bool ResetToDefaults()
        {
            bool reset = saveService.ResetToDefaults();
            if (reset)
            {
                quests?.ClearRestoredProgress();
            }

            return reset;
        }

        internal bool TryGetLegacyMillScenario(out int stage, out int outcomeFlags)
        {
            return saveService.TryGetLegacyMillScenario(out stage, out outcomeFlags);
        }

        internal YG.SavedWorldItem[] GetWorldItems() => saveService.GetWorldItems();

        internal string[] GetRetiredSceneWorldItemIds() => saveService.GetRetiredSceneWorldItemIds();

        internal void SetWorldItemState(
            System.Collections.Generic.IEnumerable<YG.SavedWorldItem> worldItems,
            System.Collections.Generic.IEnumerable<string> retiredSceneWorldItemIds)
        {
            saveService.SetWorldItemState(worldItems, retiredSceneWorldItemIds);
        }

        private static SavedPlayerPose ToSavedPlayerPose(Pose pose)
        {
            return new SavedPlayerPose
            {
                isValid = 1,
                positionX = pose.position.x,
                positionY = pose.position.y,
                positionZ = pose.position.z,
                rotationX = pose.rotation.x,
                rotationY = pose.rotation.y,
                rotationZ = pose.rotation.z,
                rotationW = pose.rotation.w
            };
        }
    }
}
