using System.Collections.Generic;
using Dialogs.Graph.Model;
using Inventory.Inventories;
using Money;
using Quests;
using UnityEngine;
using Dialogue;

namespace Dialogs.Graph
{
    public static class DialogueAnswerAvailability
    {
        public static bool AreConditionsSatisfied(
            bool hasConditions,
            IReadOnlyList<DialogAnswerCondition> conditions,
            PlayerInventory playerInventory,
            MoneyStorage playerMoneyStorage,
            QuestController questController,
            DialogueRuntimeFlagRegistry runtimeFlags = null)
        {
            if (!hasConditions || conditions == null)
            {
                return true;
            }

            if (playerInventory == null || playerMoneyStorage == null || questController == null)
            {
                return false;
            }

            var simulatedMoney = playerMoneyStorage.CurrentMoney.Value;
            var simulatedItems = new Dictionary<global::Inventory.Item.ItemConfig, int>();

            foreach (DialogAnswerCondition condition in conditions)
            {
                if (condition == null)
                {
                    continue;
                }

                switch (condition.Type)
                {
                    case DialogAnswerConditionType.GiveMoney:
                        simulatedMoney += Mathf.Abs(condition.MoneyAmount);
                        break;
                    case DialogAnswerConditionType.TakeMoney:
                    {
                        int moneyAmount = Mathf.Abs(condition.MoneyAmount);
                        if (simulatedMoney < moneyAmount)
                        {
                            return false;
                        }

                        simulatedMoney -= moneyAmount;
                        break;
                    }
                    case DialogAnswerConditionType.TakeMoneyMax:
                        simulatedMoney = Mathf.Max(0, simulatedMoney - Mathf.Abs(condition.MoneyAmount));
                        break;
                    case DialogAnswerConditionType.TakeItemIfHas:
                    {
                        if (condition.ItemConfig == null)
                        {
                            return false;
                        }

                        int requiredCount = Mathf.Abs(condition.ItemCount);
                        if (!simulatedItems.TryGetValue(condition.ItemConfig, out int currentCount))
                        {
                            currentCount = playerInventory.GetInventoryItemCount(condition.ItemConfig);
                        }

                        if (currentCount < requiredCount)
                        {
                            return false;
                        }

                        simulatedItems[condition.ItemConfig] = currentCount - requiredCount;
                        break;
                    }
                    case DialogAnswerConditionType.CheckQuestStep:
                    case DialogAnswerConditionType.DoQuestStep:
                        if (!questController.CanExecuteTransition(condition.QuestGraph, condition.QuestTransition))
                        {
                            return false;
                        }

                        break;
                    case DialogAnswerConditionType.DoQuestEnd:
                        if (!questController.CanCompleteNode(condition.QuestGraph, condition.QuestNode))
                        {
                            return false;
                        }

                        break;
                    case DialogAnswerConditionType.AddQuest:
                    case DialogAnswerConditionType.CanAddQuest:
                        if (!questController.CanAddQuest(condition.QuestGraph))
                        {
                            return false;
                        }

                        break;
                    case DialogAnswerConditionType.RequireRuntimeFlag:
                        if (runtimeFlags == null || !runtimeFlags.IsActive(condition.RuntimeFlag))
                        {
                            return false;
                        }

                        break;
                    case DialogAnswerConditionType.ClearRuntimeFlag:
                        break;
                    case DialogAnswerConditionType.RequireInactiveRuntimeFlag:
                        if (runtimeFlags == null || runtimeFlags.IsActive(condition.RuntimeFlag))
                        {
                            return false;
                        }

                        break;
                    case DialogAnswerConditionType.SetRuntimeFlag:
                        break;
                }
            }

            return true;
        }
    }
}
