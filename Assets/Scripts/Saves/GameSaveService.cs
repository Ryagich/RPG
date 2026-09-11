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
using YG;

namespace Saves
{
    /// <summary>
    /// Infrastructure adapter between runtime player state and PluginYG's primitive save object.
    /// It owns no quest or location rules; callers provide the state that their domain has
    /// already reached.
    /// </summary>
    public sealed class GameSaveService : IStartable
    {
        private const int CurrentVersion = 3;
        private readonly BootCompletion bootCompletion;
        private readonly RuntimeFactionRelations factionRelations;
        private readonly DialogueRuntimeFlagRegistry dialogueRuntimeFlags;
        private readonly System.Threading.Tasks.TaskCompletionSource<bool> ready = new();

        public System.Threading.Tasks.Task Ready => ready.Task;
        public bool IsReady { get; private set; }
        public bool HasSavedData => IsReady && YG2.saves.saveVersion > 0;

        public event Action ReadyStateChanged;

        public GameSaveService(
            BootCompletion bootCompletion,
            RuntimeFactionRelations factionRelations,
            DialogueRuntimeFlagRegistry dialogueRuntimeFlags)
        {
            this.bootCompletion = bootCompletion;
            this.factionRelations = factionRelations;
            this.dialogueRuntimeFlags = dialogueRuntimeFlags;
        }

        public async void Start()
        {
            await bootCompletion.WaitAsync();
            EnsureInitialized();
            RestoreFactionRelations(factionRelations, YG2.saves.factionRelations);
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

            SavesYG data = YG2.saves;
            data.locationId = locationId ?? string.Empty;
            data.entranceId = entranceId ?? string.Empty;
            data.playerPose = playerPose;
            data.money = money.CurrentMoney.Value;
            data.health = stats.Hp.Value.Value;
            data.stamina = stats.GetStat(StatType.Stamina).Value.Value;
            data.inventory = SerializeInventory(inventory);
            data.quests = SerializeQuests(quests);
            data.factionRelations = SerializeFactionRelations(factionRelations);
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

            SavesYG data = YG2.saves;
            if (data.saveVersion <= 0)
            {
                return false;
            }

            RestoreInventory(inventory, itemStorage, data.inventory);
            money.Set(data.money);
            stats.ChangeValue(StatType.Hp, data.health);
            stats.ChangeValue(StatType.Stamina, data.stamina);
            RestoreQuests(quests, questCatalog, data.quests);
            return true;
        }

        internal bool TryGetSavedLocation(out string locationId, out string entranceId)
        {
            locationId = null;
            entranceId = null;
            if (!IsReady || YG2.saves.saveVersion <= 0 || string.IsNullOrWhiteSpace(YG2.saves.locationId))
            {
                return false;
            }

            locationId = YG2.saves.locationId;
            entranceId = YG2.saves.entranceId;
            return true;
        }

        internal bool TryGetSavedPlayerPose(out Pose pose)
        {
            pose = default;
            if (!IsReady || YG2.saves.saveVersion <= 0)
            {
                return false;
            }

            SavedPlayerPose savedPose = YG2.saves.playerPose;
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
        /// Replaces the persisted game snapshot with PluginYG's default save object.
        /// Runtime faction state is reset as well because the project scope survives a return
        /// to the main menu.
        /// </summary>
        internal bool ResetToDefaults()
        {
            if (!IsReady)
            {
                return false;
            }

            YG2.SetDefaultSaves();
            EnsureInitialized();
            YG2.saves.quests = Array.Empty<SavedQuestProgress>();
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

        internal int GetMillScenarioStage() => IsReady ? YG2.saves.millScenarioStage : 0;

        internal int GetMillOutcomeFlag(int index)
        {
            int[] flags = IsReady ? YG2.saves.millOutcomeFlags : null;
            return flags != null && index >= 0 && index < flags.Length ? flags[index] : 0;
        }

        /// <summary>
        /// Updates state that belongs in the next complete checkpoint. Persisting remains the
        /// responsibility of <see cref="GameSaveController"/>.
        /// </summary>
        internal void SetMillScenarioState(int stage, IReadOnlyList<int> outcomeFlags)
        {
            if (!IsReady)
            {
                return;
            }

            YG2.saves.millScenarioStage = Mathf.Max(0, stage);
            YG2.saves.millOutcomeFlags = outcomeFlags?.ToArray() ?? Array.Empty<int>();
        }

        private static SavedInventoryItem[] SerializeInventory(PlayerInventory inventory)
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

        private static void RestoreInventory(
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

        private static void EnsureInitialized()
        {
            YG2.saves.quests ??= Array.Empty<SavedQuestProgress>();
            YG2.saves.inventory ??= Array.Empty<SavedInventoryItem>();
            YG2.saves.millOutcomeFlags ??= Array.Empty<int>();
            YG2.saves.factionRelations ??= Array.Empty<SavedFactionRelation>();
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Persist()
        {
            YG2.SaveProgress();
        }
    }
}
