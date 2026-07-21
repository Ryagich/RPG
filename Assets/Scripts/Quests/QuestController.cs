using System.Collections.Generic;
using Inventory.Inventories;
using Inventory.Item;
using Money;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEngine;

namespace Quests
{
    public enum QuestChangeType { Added, Updated, Completed, Removed, Failed }

    public readonly struct QuestChangeInfo
    {
        public QuestChangeType Type { get; }
        public QuestGraph Quest { get; }
        public QuestChangeInfo(QuestChangeType type, QuestGraph quest) { Type = type; Quest = quest; }
    }

    public class QuestController
    {
        private readonly PlayerInventory playerInventory;
        private readonly MoneyStorage moneyStorage;
        private readonly Dictionary<QuestGraph, QuestProgress> progressByQuest = new();
        private readonly List<QuestProgress> progress = new();

        public IReadOnlyList<QuestProgress> Progress => progress;
        public QuestProgress CurrentQuest
        {
            get
            {
                foreach (var questProgress in progress)
                {
                    if (questProgress is { IsCompleted: false })
                    {
                        return questProgress;
                    }
                }

                return null;
            }
        }
        public event System.Action<QuestChangeInfo> Changed;

        public QuestController(PlayerInventory playerInventory, MoneyStorage moneyStorage)
        {
            this.playerInventory = playerInventory;
            this.moneyStorage = moneyStorage;
        }

        public bool HasQuest(QuestGraph questGraph)
        {
            return questGraph != null && progressByQuest.ContainsKey(questGraph);
        }

        public bool IsCompleted(QuestGraph questGraph)
        {
            return TryGetProgress(questGraph, out QuestProgress questProgress) && questProgress.IsCompleted;
        }

        public bool CanAddQuest(QuestGraph questGraph)
        {
            if (questGraph == null || progressByQuest.ContainsKey(questGraph))
            {
                return false;
            }

            QuestNodeData entryNode = questGraph.GetEntryNode();
            if (entryNode == null)
            {
                return false;
            }

            return AreRequirementsMet(entryNode.HasAvailabilityRequirements
                ? entryNode.AvailabilityRequirements
                : null);
        }

        public bool TryAddQuest(QuestGraph questGraph)
        {
            if (!CanAddQuest(questGraph))
            {
                return false;
            }

            QuestNodeData entryNode = questGraph.GetEntryNode();
            var questProgress = new QuestProgress(questGraph, entryNode);
            progressByQuest.Add(questGraph, questProgress);
            progress.Add(questProgress);
            Changed?.Invoke(new QuestChangeInfo(QuestChangeType.Added, questGraph));
            return true;
        }

        public QuestNodeData GetCurrentNode(QuestGraph questGraph)
        {
            return TryGetProgress(questGraph, out QuestProgress questProgress)
                ? questProgress.CurrentNode
                : null;
        }

        public QuestNodeData GetDisplayNode(QuestGraph questGraph)
        {
            if (questGraph == null)
            {
                return null;
            }

            if (TryGetProgress(questGraph, out QuestProgress questProgress) && questProgress.CurrentNode != null)
            {
                return questProgress.CurrentNode;
            }

            return questGraph.GetEntryNode();
        }

        public Sprite GetQuestSprite(QuestGraph questGraph)
        {
            return GetDisplayNode(questGraph)?.Icon ?? questGraph?.Icon;
        }

        public bool TrySetCurrentQuest(QuestGraph questGraph)
        {
            if (!TryGetActiveProgress(questGraph, out QuestProgress questProgress))
            {
                return false;
            }

            int currentIndex = progress.IndexOf(questProgress);
            if (currentIndex <= 0)
            {
                return true;
            }

            progress.RemoveAt(currentIndex);
            progress.Insert(0, questProgress);
            Changed?.Invoke(new QuestChangeInfo(QuestChangeType.Updated, questGraph));
            return true;
        }

        public bool CanExecuteTransition(QuestGraph questGraph, QuestTransition transition)
        {
            if (!TryGetActiveProgress(questGraph, out QuestProgress questProgress) || transition == null)
            {
                return false;
            }

            QuestNodeData currentNode = questProgress.CurrentNode;
            if (currentNode == null ||
                currentNode.Transitions == null ||
                !currentNode.Transitions.Contains(transition) ||
                transition.TargetNode == null ||
                !questGraph.ContainsNode(transition.TargetNode))
            {
                return false;
            }

            return AreRequirementsMet(transition.HasConditions ? transition.Conditions : null);
        }

        public bool TryExecuteTransition(QuestGraph questGraph, QuestTransition transition)
        {
            if (!CanExecuteTransition(questGraph, transition))
            {
                return false;
            }

            if (!ApplyResults(transition.HasResults ? transition.Results : null))
            {
                return false;
            }

            QuestProgress questProgress = progressByQuest[questGraph];
            questProgress.SetCurrentNode(transition.TargetNode);
            Changed?.Invoke(new QuestChangeInfo(QuestChangeType.Updated, questGraph));
            return true;
        }

        public bool CanCompleteNode(QuestGraph questGraph, QuestNodeData nodeData)
        {
            if (!TryGetActiveProgress(questGraph, out QuestProgress questProgress) ||
                nodeData == null ||
                questProgress.CurrentNode != nodeData ||
                !questGraph.IsTerminalNode(nodeData))
            {
                return false;
            }

            return true;
        }

        public bool TryCompleteNode(QuestGraph questGraph, QuestNodeData nodeData)
        {
            if (!CanCompleteNode(questGraph, nodeData))
            {
                return false;
            }

            if (!ApplyResults(nodeData.HasCompletionResults ? nodeData.CompletionResults : null))
            {
                return false;
            }

            QuestProgress questProgress = progressByQuest[questGraph];
            questProgress.Complete(nodeData);
            Changed?.Invoke(new QuestChangeInfo(QuestChangeType.Completed, questGraph));
            return true;
        }

        public bool TryRemoveQuest(QuestGraph questGraph)
        {
            if (!TryGetProgress(questGraph, out var questProgress)) return false;
            progressByQuest.Remove(questGraph);
            progress.Remove(questProgress);
            Changed?.Invoke(new QuestChangeInfo(QuestChangeType.Removed, questGraph));
            return true;
        }

        public bool TryFailQuest(QuestGraph questGraph)
        {
            if (!TryGetProgress(questGraph, out var questProgress)) return false;
            progressByQuest.Remove(questGraph);
            progress.Remove(questProgress);
            Changed?.Invoke(new QuestChangeInfo(QuestChangeType.Failed, questGraph));
            return true;
        }

        private bool TryGetProgress(QuestGraph questGraph, out QuestProgress questProgress)
        {
            if (questGraph != null && progressByQuest.TryGetValue(questGraph, out questProgress))
            {
                return true;
            }

            questProgress = null;
            return false;
        }

        private bool TryGetActiveProgress(QuestGraph questGraph, out QuestProgress questProgress)
        {
            if (TryGetProgress(questGraph, out questProgress) && !questProgress.IsCompleted)
            {
                return true;
            }

            questProgress = null;
            return false;
        }

        private bool AreRequirementsMet(IReadOnlyList<QuestResourceEntry> entries)
        {
            if (entries == null)
            {
                return true;
            }

            var simulatedMoney = moneyStorage.CurrentMoney.Value;
            var simulatedItemCounts = new Dictionary<ItemConfig, int>();

            foreach (QuestResourceEntry entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                switch (entry.Type)
                {
                    case QuestResourceEntryType.Money:
                    {
                        int requiredMoney = entry.MoneyAmount < 0 ? -entry.MoneyAmount : entry.MoneyAmount;
                        if (simulatedMoney < requiredMoney)
                        {
                            return false;
                        }

                        simulatedMoney -= requiredMoney;
                        break;
                    }
                    case QuestResourceEntryType.Item:
                    {
                        if (entry.ItemConfig == null)
                        {
                            return false;
                        }

                        int requiredItemCount = entry.ItemCount < 0 ? -entry.ItemCount : entry.ItemCount;
                        int currentCount = GetSimulatedItemCount(simulatedItemCounts, entry.ItemConfig);
                        if (currentCount < requiredItemCount)
                        {
                            return false;
                        }

                        simulatedItemCounts[entry.ItemConfig] = currentCount - requiredItemCount;
                        break;
                    }
                }
            }

            return true;
        }

        private bool ApplyResults(IReadOnlyList<QuestResourceEntry> entries)
        {
            if (entries == null)
            {
                return true;
            }

            foreach (QuestResourceEntry entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                switch (entry.Type)
                {
                    case QuestResourceEntryType.Money:
                    {
                        if (entry.MoneyAmount >= 0)
                        {
                            moneyStorage.Add(entry.MoneyAmount);
                        }
                        else if (!moneyStorage.TrySpend(-entry.MoneyAmount))
                        {
                            return false;
                        }

                        break;
                    }
                    case QuestResourceEntryType.Item:
                    {
                        if (entry.ItemConfig == null)
                        {
                            return false;
                        }

                        if (entry.ItemCount >= 0)
                        {
                            if (entry.ItemCount > 0)
                            {
                                playerInventory.TryAdd(new ItemStack(entry.ItemConfig, entry.ItemCount));
                            }
                        }
                        else if (!playerInventory.TryConsumeItemCount(entry.ItemConfig, -entry.ItemCount))
                        {
                            return false;
                        }

                        break;
                    }
                }
            }

            return true;
        }

        private int GetSimulatedItemCount(Dictionary<ItemConfig, int> simulatedItemCounts, ItemConfig itemConfig)
        {
            if (simulatedItemCounts.TryGetValue(itemConfig, out int currentCount))
            {
                return currentCount;
            }

            currentCount = playerInventory.GetInventoryItemCount(itemConfig);
            simulatedItemCounts[itemConfig] = currentCount;
            return currentCount;
        }
    }

    public sealed class QuestProgress
    {
        private readonly List<QuestNodeData> completedNodes = new();

        public QuestGraph QuestGraph { get; }
        public QuestNodeData CurrentNode { get; private set; }
        public bool IsCompleted { get; private set; }
        public IReadOnlyList<QuestNodeData> CompletedNodes => completedNodes;

        public QuestProgress(QuestGraph questGraph, QuestNodeData currentNode)
        {
            QuestGraph = questGraph ?? throw new System.ArgumentNullException(nameof(questGraph));
            CurrentNode = currentNode ?? throw new System.ArgumentNullException(nameof(currentNode));
        }

        public void SetCurrentNode(QuestNodeData nodeData)
        {
            AddCompletedNode(CurrentNode);
            CurrentNode = nodeData ?? throw new System.ArgumentNullException(nameof(nodeData));
        }

        public void Complete(QuestNodeData nodeData)
        {
            CurrentNode = nodeData ?? throw new System.ArgumentNullException(nameof(nodeData));
            AddCompletedNode(CurrentNode);
            IsCompleted = true;
        }

        private void AddCompletedNode(QuestNodeData nodeData)
        {
            if (nodeData != null && !completedNodes.Contains(nodeData))
            {
                completedNodes.Add(nodeData);
            }
        }
    }
}
