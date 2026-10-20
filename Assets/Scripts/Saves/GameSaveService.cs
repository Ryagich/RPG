using System;
using System.Collections.Generic;
using System.Linq;
using Dialogue;
using Inventory;
using Factions;
using Inventory.Inventories;
using Inventory.Storage;
using Localization;
using Money;
using Quests;
using Quests.Graph;
using Quests.Graph.Model;
using Locations;
using Stats;
using UnityEngine;
using VContainer.Unity;

namespace Saves
{
    /// <summary>
    /// Infrastructure adapter between runtime player state and the local save snapshot.
    /// It owns no quest or location rules; callers provide the state that their domain has
    /// already reached.
    /// </summary>
    public sealed class GameSaveService : IStartable
    {
        private const int MillQuestSaveVersion = 4;
        private const int SurvivalStatsSaveVersion = 5;
        private const int CharacterStateSaveVersion = 6;
        // Version 6 used a runtime hierarchy path as an NPC identity. Such records cannot be
        // safely mapped to the immutable scene IDs introduced in version 7, so only that
        // obsolete NPC portion is discarded during migration.
        private const int NpcPersistentIdSaveVersion = 7;
        private const int WorldItemsSaveVersion = 8;
        private const int PlantWorldSaveVersion = 9;
        private const int CurrentVersion = 10;
        private const string PlayerCharacterId = "player";
        private readonly BootCompletion bootCompletion;
        private readonly RuntimeFactionRelations factionRelations;
        private readonly DialogueRuntimeFlagRegistry dialogueRuntimeFlags;
        private readonly NpcCharacterSaveRegistry npcRegistry;
        private readonly LocalSaveStore saveStore;
        private readonly System.Threading.Tasks.TaskCompletionSource<bool> ready = new();

        public System.Threading.Tasks.Task Ready => ready.Task;
        public bool IsReady { get; private set; }
        public bool HasSavedData => IsReady && saveStore.Data.saveVersion > 0;

        public event Action ReadyStateChanged;

        public GameSaveService(
            BootCompletion bootCompletion,
            RuntimeFactionRelations factionRelations,
            DialogueRuntimeFlagRegistry dialogueRuntimeFlags,
            NpcCharacterSaveRegistry npcRegistry,
            LocalSaveStore saveStore)
        {
            this.bootCompletion = bootCompletion;
            this.factionRelations = factionRelations;
            this.dialogueRuntimeFlags = dialogueRuntimeFlags;
            this.npcRegistry = npcRegistry;
            this.saveStore = saveStore;
        }

        public async void Start()
        {
            await bootCompletion.WaitAsync();
            saveStore.Load();
            EnsureInitialized();
            MigrateNpcStatesToPersistentIds();
            RestoreFactionRelations(factionRelations, saveStore.Data.factionRelations);
            IsReady = true;
            ready.TrySetResult(true);
            ReadyStateChanged?.Invoke();
        }

        internal bool SavePlayer(
            string locationId,
            string entranceId,
            SavedPlayerPose playerPose,
            PlayerInventory inventory,
            MoneyStorage money,
            StatsController stats,
            QuestController quests)
        {
            if (!IsReady || inventory == null || money == null || stats == null || quests == null)
            {
                return false;
            }

            GameSaveData data = saveStore.Data;
            data.locationId = locationId ?? string.Empty;
            data.entranceId = entranceId ?? string.Empty;
            data.playerPose = playerPose;
            data.money = money.CurrentMoney.Value;
            data.health = stats.Hp.Value.Value;
            data.stamina = stats.GetStat(StatType.Stamina).Value.Value;
            data.water = stats.GetStat(StatType.Water).Value.Value;
            data.food = stats.GetStat(StatType.Food).Value.Value;
            data.inventory = SerializeInventory(inventory);
            data.playerCharacter = new SavedCharacterState
            {
                characterId = PlayerCharacterId,
                // Player death has its own lifecycle and never participates in NPC corpse restore.
                isAlive = true,
                health = data.health,
                stamina = data.stamina,
                water = data.water,
                food = data.food,
                inventory = data.inventory,
                deathPose = default
            };
            data.npcCharacters = MergeNpcStates(data.npcCharacters, npcRegistry?.CaptureStates());
            data.quests = SerializeQuests(quests);
            data.factionRelations = SerializeFactionRelations(factionRelations);
            // Version 4 stores the mill entirely in the regular quest snapshot. Keep the old
            // fields serialized only long enough for existing version-3 saves to migrate.
            data.millScenarioStage = 0;
            data.millOutcomeFlags = Array.Empty<int>();
            data.saveVersion = CurrentVersion;
            Persist();
            return true;
        }

        internal bool TryRestorePlayer(
            PlayerInventory inventory,
            MoneyStorage money,
            StatsController stats,
            QuestController quests,
            ItemStorage itemStorage,
            QuestCatalog questCatalog)
        {
            if (!IsReady || inventory == null || money == null || stats == null || quests == null || itemStorage == null)
            {
                return false;
            }

            GameSaveData data = saveStore.Data;
            if (data.saveVersion <= 0)
            {
                return false;
            }

            if (data.saveVersion >= CharacterStateSaveVersion && IsPlayerStateValid(data.playerCharacter))
            {
                RestoreInventory(inventory, itemStorage, data.playerCharacter.inventory);
                RestoreCharacterStats(data.playerCharacter, stats);
            }
            else
            {
                RestoreInventory(inventory, itemStorage, data.inventory);
                stats.ChangeValue(StatType.Hp, data.health);
                stats.ChangeValue(StatType.Stamina, data.stamina);
                if (data.saveVersion >= SurvivalStatsSaveVersion)
                {
                    stats.ChangeValue(StatType.Water, data.water);
                    stats.ChangeValue(StatType.Food, data.food);
                }
            }
            money.Set(data.money);
            RestoreQuests(quests, questCatalog, data.quests);
            return true;
        }

        internal bool TryGetSavedNpc(string characterId, out SavedCharacterState savedState)
        {
            savedState = null;
            if (!IsReady || string.IsNullOrWhiteSpace(characterId) || saveStore.Data.saveVersion < NpcPersistentIdSaveVersion ||
                saveStore.Data.npcCharacters == null)
            {
                return false;
            }

            savedState = saveStore.Data.npcCharacters.FirstOrDefault(candidate => candidate != null &&
                string.Equals(candidate.characterId, characterId, StringComparison.Ordinal));
            return savedState != null;
        }

        internal bool TryGetSavedLocation(out string locationId, out string entranceId)
        {
            locationId = null;
            entranceId = null;
            if (!IsReady || saveStore.Data.saveVersion <= 0 || string.IsNullOrWhiteSpace(saveStore.Data.locationId))
            {
                return false;
            }

            locationId = saveStore.Data.locationId;
            entranceId = saveStore.Data.entranceId;
            return true;
        }

        internal bool TryGetSavedPlayerPose(out Pose pose)
        {
            pose = default;
            if (!IsReady || saveStore.Data.saveVersion <= 0)
            {
                return false;
            }

            SavedPlayerPose savedPose = saveStore.Data.playerPose;
            if (savedPose.isValid == 0 ||
                !IsFinite(savedPose.positionX) || !IsFinite(savedPose.positionY) || !IsFinite(savedPose.positionZ) ||
                !IsFinite(savedPose.rotationX) || !IsFinite(savedPose.rotationY) ||
                !IsFinite(savedPose.rotationZ) || !IsFinite(savedPose.rotationW))
            {
                return false;
            }

            var rotation = new Quaternion(
                savedPose.rotationX,
                savedPose.rotationY,
                savedPose.rotationZ,
                savedPose.rotationW);
            float rotationLengthSquared = rotation.x * rotation.x + rotation.y * rotation.y +
                                          rotation.z * rotation.z + rotation.w * rotation.w;
            if (rotationLengthSquared < 0.0001f)
            {
                return false;
            }

            pose = new Pose(
                new Vector3(savedPose.positionX, savedPose.positionY, savedPose.positionZ),
                rotation.normalized);
            return true;
        }

        /// <summary>
        /// Replaces the persisted game snapshot with a fresh project-owned save object.
        /// Runtime faction state is reset as well because the project scope survives a return
        /// to the main menu.
        /// </summary>
        internal bool ResetToDefaults()
        {
            if (!IsReady)
            {
                return false;
            }

            GameSaveData data = new GameSaveData
            {
                languageCode = saveStore.Data.languageCode
            };
            saveStore.Replace(data);
            data.saveVersion = 0;
            data.locationId = string.Empty;
            data.entranceId = string.Empty;
            data.playerPose = default;
            data.money = 0;
            data.health = 0f;
            data.stamina = 0f;
            data.water = 0f;
            data.food = 0f;
            data.playerCharacter = null;
            data.npcCharacters = Array.Empty<SavedCharacterState>();
            data.quests = Array.Empty<SavedQuestProgress>();
            data.inventory = Array.Empty<SavedInventoryItem>();
            data.millScenarioStage = 0;
            data.millOutcomeFlags = Array.Empty<int>();
            data.factionRelations = Array.Empty<SavedFactionRelation>();
            data.worldItems = Array.Empty<SavedWorldItem>();
            data.retiredSceneWorldItemIds = Array.Empty<string>();
            data.farmPlants = Array.Empty<SavedFarmPlant>();
            data.fruitTrees = Array.Empty<SavedFruitTree>();
            npcRegistry?.Clear();
            factionRelations.ResetToDefaults();
            dialogueRuntimeFlags?.Clear();
            Persist();
            return true;
        }

        internal void RestoreLocationTransition(LocationTransitionContext transitionContext)
        {
            if (transitionContext == null || transitionContext.HasPendingTransition ||
                !TryGetSavedLocation(out string locationId, out string entranceId))
            {
                return;
            }

            transitionContext.SetPendingTransition(locationId, entranceId);
        }

        internal SavedWorldItem[] GetWorldItems() => saveStore.Data.worldItems ?? Array.Empty<SavedWorldItem>();

        internal string[] GetRetiredSceneWorldItemIds() =>
            saveStore.Data.retiredSceneWorldItemIds ?? Array.Empty<string>();

        /// <summary>
        /// Updates only the in-memory checkpoint. Complete checkpoint coordinators retain
        /// ownership of when PluginYG persists it.
        /// </summary>
        internal void SetWorldItemState(
            IEnumerable<SavedWorldItem> worldItems,
            IEnumerable<string> retiredSceneWorldItemIds)
        {
            saveStore.Data.worldItems = worldItems?.ToArray() ?? Array.Empty<SavedWorldItem>();
            saveStore.Data.retiredSceneWorldItemIds = retiredSceneWorldItemIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
        }

        internal SavedFarmPlant[] GetFarmPlants() => saveStore.Data.farmPlants ?? Array.Empty<SavedFarmPlant>();

        internal SavedFruitTree[] GetFruitTrees() => saveStore.Data.fruitTrees ?? Array.Empty<SavedFruitTree>();

        /// <summary>
        /// Changes only the in-memory save snapshot. Plant growth never decides when the game
        /// checkpoint is written; that remains GameSaveController's responsibility.
        /// </summary>
        internal void SetPlantWorldState(
            IEnumerable<SavedFarmPlant> farmPlants,
            IEnumerable<SavedFruitTree> fruitTrees)
        {
            saveStore.Data.farmPlants = farmPlants?.ToArray() ?? Array.Empty<SavedFarmPlant>();
            saveStore.Data.fruitTrees = fruitTrees?.ToArray() ?? Array.Empty<SavedFruitTree>();
        }

        /// <summary>
        /// Exposes the obsolete mill payload only while loading a save created before the
        /// quest-based format. New saves never read or write this data.
        /// </summary>
        internal bool TryGetLegacyMillScenario(out int stage, out int outcomeFlags)
        {
            stage = 0;
            outcomeFlags = 0;
            if (!IsReady || saveStore.Data.saveVersion <= 0 || saveStore.Data.saveVersion >= MillQuestSaveVersion)
            {
                return false;
            }

            stage = saveStore.Data.millScenarioStage;
            int[] savedFlags = saveStore.Data.millOutcomeFlags;
            outcomeFlags = savedFlags != null && savedFlags.Length > 0 ? savedFlags[0] : 0;
            return stage != 0 || outcomeFlags != 0;
        }

        internal static SavedInventoryItem[] SerializeInventory(PlayerInventory inventory)
        {
            return inventory.GetPersistenceSnapshot()
                .Where(entry => entry.ItemConfig != null && !string.IsNullOrWhiteSpace(entry.ItemConfig.Id) && entry.Count > 0)
                .Select(entry => new SavedInventoryItem
                {
                    itemId = entry.ItemConfig.Id,
                    count = entry.Count,
                    x = entry.X,
                    y = entry.Y,
                    rotated = entry.IsRotated ? 1 : 0,
                    slot = (int)entry.Container
                })
                .ToArray();
        }

        private static SavedQuestProgress[] SerializeQuests(QuestController quests)
        {
            return quests.Progress
                .Where(progress => progress?.QuestGraph != null && progress.CurrentNode != null &&
                                   !string.IsNullOrWhiteSpace(progress.QuestGraph.PersistentId) &&
                                   !string.IsNullOrWhiteSpace(progress.CurrentNode.PersistentId))
                .Select(progress => new SavedQuestProgress
                {
                    questId = progress.QuestGraph.PersistentId,
                    currentNodeId = progress.CurrentNode.PersistentId,
                    completedNodeIds = progress.CompletedNodes
                        .Where(node => node != null && !string.IsNullOrWhiteSpace(node.PersistentId))
                        .Select(node => node.PersistentId)
                        .ToArray(),
                    state = progress.IsCompleted ? 1 : 0
                })
                .ToArray();
        }

        private static SavedFactionRelation[] SerializeFactionRelations(RuntimeFactionRelations relations)
        {
            return relations?.GetPersistenceSnapshot()
                .Where(entry => !string.IsNullOrWhiteSpace(entry.LeftFaction.PersistentId) &&
                                !string.IsNullOrWhiteSpace(entry.RightFaction.PersistentId))
                .Select(entry => new SavedFactionRelation
                {
                    leftFactionId = entry.LeftFaction.PersistentId,
                    rightFactionId = entry.RightFaction.PersistentId,
                    relation = entry.Relation
                })
                .ToArray() ?? Array.Empty<SavedFactionRelation>();
        }

        private static void RestoreFactionRelations(
            RuntimeFactionRelations relations,
            IEnumerable<SavedFactionRelation> savedRelations)
        {
            if (relations == null || savedRelations == null)
            {
                return;
            }

            var restored = new List<FactionRelationState>();
            foreach (SavedFactionRelation entry in savedRelations)
            {
                if (string.IsNullOrWhiteSpace(entry.leftFactionId) || string.IsNullOrWhiteSpace(entry.rightFactionId) ||
                    !relations.TryGetFaction(entry.leftFactionId, out FactionConfig left) ||
                    !relations.TryGetFaction(entry.rightFactionId, out FactionConfig right))
                {
                    continue;
                }

                restored.Add(new FactionRelationState(left, right, entry.relation));
            }

            relations.Restore(restored);
        }

        internal static void RestoreInventory(
            PlayerInventory inventory,
            ItemStorage itemStorage,
            IEnumerable<SavedInventoryItem> savedEntries)
        {
            var placements = new List<InventoryItemPlacement>();
            if (savedEntries != null)
            {
                foreach (SavedInventoryItem entry in savedEntries)
                {
                    if (entry.count <= 0 || !Enum.IsDefined(typeof(PlayerInventoryContainer), entry.slot) ||
                        !itemStorage.TryGetById(entry.itemId, out var itemConfig))
                    {
                        continue;
                    }

                    placements.Add(new InventoryItemPlacement(
                        itemConfig,
                        entry.count,
                        entry.x,
                        entry.y,
                        entry.rotated != 0,
                        (PlayerInventoryContainer)entry.slot));
                }
            }

            IReadOnlyList<InventoryItemPlacement> rejected = inventory.RestorePersistenceSnapshot(placements);
            foreach (InventoryItemPlacement entry in rejected)
            {
                Debug.LogWarning($"Saved inventory entry '{entry.ItemConfig?.Id}' could not be restored.");
            }
        }

        private static void RestoreQuests(
            QuestController quests,
            QuestCatalog questCatalog,
            IEnumerable<SavedQuestProgress> savedEntries)
        {
            quests.ClearRestoredProgress();
            if (questCatalog == null || savedEntries == null)
            {
                return;
            }

            foreach (SavedQuestProgress entry in savedEntries)
            {
                if (!questCatalog.TryGet(entry.questId, out QuestGraph questGraph))
                {
                    Debug.LogWarning($"Saved quest '{entry.questId}' is not present in the quest catalogue.");
                    continue;
                }

                QuestNodeData node = questGraph.Nodes
                    .Select(questNode => questNode?.NodeData)
                    .FirstOrDefault(candidate => candidate != null && candidate.PersistentId == entry.currentNodeId);
                IReadOnlyList<QuestNodeData> completedNodes = ResolveCompletedNodes(questGraph, entry.completedNodeIds);
                if (node == null || !quests.TryRestoreQuest(questGraph, node, completedNodes, entry.state != 0))
                {
                    Debug.LogWarning($"Saved state for quest '{questGraph.name}' could not be restored.");
                }
            }
        }

        private static IReadOnlyList<QuestNodeData> ResolveCompletedNodes(
            QuestGraph questGraph,
            IEnumerable<string> completedNodeIds)
        {
            if (questGraph == null || completedNodeIds == null)
            {
                return Array.Empty<QuestNodeData>();
            }

            var nodesById = questGraph.Nodes
                .Select(node => node?.NodeData)
                .Where(node => node != null && !string.IsNullOrWhiteSpace(node.PersistentId))
                .ToDictionary(node => node.PersistentId);
            var completedNodes = new List<QuestNodeData>();

            foreach (string nodeId in completedNodeIds)
            {
                if (!string.IsNullOrWhiteSpace(nodeId) && nodesById.TryGetValue(nodeId, out QuestNodeData node))
                {
                    completedNodes.Add(node);
                }
            }

            return completedNodes;
        }

        private void EnsureInitialized()
        {
            saveStore.Data.quests ??= Array.Empty<SavedQuestProgress>();
            saveStore.Data.inventory ??= Array.Empty<SavedInventoryItem>();
            saveStore.Data.npcCharacters ??= Array.Empty<SavedCharacterState>();
            saveStore.Data.millOutcomeFlags ??= Array.Empty<int>();
            saveStore.Data.factionRelations ??= Array.Empty<SavedFactionRelation>();
            saveStore.Data.worldItems ??= Array.Empty<SavedWorldItem>();
            saveStore.Data.retiredSceneWorldItemIds ??= Array.Empty<string>();
            saveStore.Data.farmPlants ??= Array.Empty<SavedFarmPlant>();
            saveStore.Data.fruitTrees ??= Array.Empty<SavedFruitTree>();
        }

        private void MigrateNpcStatesToPersistentIds()
        {
            if (saveStore.Data.saveVersion <= 0 || saveStore.Data.saveVersion >= NpcPersistentIdSaveVersion)
            {
                return;
            }

            saveStore.Data.npcCharacters = Array.Empty<SavedCharacterState>();
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static SavedCharacterPose ToSavedCharacterPose(Pose pose)
        {
            return new SavedCharacterPose
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

        internal static bool TryToPose(SavedCharacterPose savedPose, out Pose pose)
        {
            pose = default;
            if (savedPose.isValid == 0 ||
                !IsFinite(savedPose.positionX) || !IsFinite(savedPose.positionY) || !IsFinite(savedPose.positionZ) ||
                !IsFinite(savedPose.rotationX) || !IsFinite(savedPose.rotationY) ||
                !IsFinite(savedPose.rotationZ) || !IsFinite(savedPose.rotationW))
            {
                return false;
            }

            var rotation = new Quaternion(
                savedPose.rotationX,
                savedPose.rotationY,
                savedPose.rotationZ,
                savedPose.rotationW);
            float rotationLengthSquared = rotation.x * rotation.x + rotation.y * rotation.y +
                                          rotation.z * rotation.z + rotation.w * rotation.w;
            if (rotationLengthSquared < 0.0001f)
            {
                return false;
            }

            pose = new Pose(new Vector3(savedPose.positionX, savedPose.positionY, savedPose.positionZ), rotation.normalized);
            return true;
        }

        private static bool IsPlayerStateValid(SavedCharacterState state)
        {
            return state != null && string.Equals(state.characterId, PlayerCharacterId, StringComparison.Ordinal);
        }

        private static void RestoreCharacterStats(SavedCharacterState state, StatsController stats)
        {
            stats.ChangeValue(StatType.Hp, state.health);
            stats.ChangeValue(StatType.Stamina, state.stamina);
            stats.ChangeValue(StatType.Water, state.water);
            stats.ChangeValue(StatType.Food, state.food);
        }

        private static SavedCharacterState[] MergeNpcStates(
            IEnumerable<SavedCharacterState> existingStates,
            IEnumerable<SavedCharacterState> currentStates)
        {
            var states = new Dictionary<string, SavedCharacterState>(StringComparer.Ordinal);
            if (existingStates != null)
            {
                foreach (SavedCharacterState state in existingStates)
                {
                    if (state != null && !string.IsNullOrWhiteSpace(state.characterId))
                    {
                        states[state.characterId] = state;
                    }
                }
            }

            if (currentStates != null)
            {
                foreach (SavedCharacterState state in currentStates)
                {
                    if (state != null && !string.IsNullOrWhiteSpace(state.characterId))
                    {
                        states[state.characterId] = state;
                    }
                }
            }

            return states.Values.ToArray();
        }

        private void Persist()
        {
            saveStore.Save();
        }
    }
}
