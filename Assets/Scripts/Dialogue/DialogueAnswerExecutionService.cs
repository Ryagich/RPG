using System;
using System.Collections.Generic;
using Dialogs.Graph;
using Dialogs.Graph.Model;
using Inventory.Inventories;
using Money;
using Quests;
using Quests.Graph;
using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// Application-layer executor for the gameplay contract authored on a dialogue answer.
    /// It changes gameplay state but deliberately returns presentation-neutral effects; UI
    /// decides how those effects are displayed.
    /// </summary>
    public sealed class DialogueAnswerExecutionService
    {
        private readonly PlayerInventory playerInventory;
        private readonly MoneyStorage playerMoneyStorage;
        private readonly QuestController questController;
        private readonly DialogueRuntimeFlagRegistry runtimeFlags;

        public DialogueAnswerExecutionService(
            PlayerInventory playerInventory,
            MoneyStorage playerMoneyStorage,
            QuestController questController,
            DialogueRuntimeFlagRegistry runtimeFlags)
        {
            this.playerInventory = playerInventory;
            this.playerMoneyStorage = playerMoneyStorage;
            this.questController = questController;
            this.runtimeFlags = runtimeFlags;
        }

        public bool TryExecute(
            bool hasConditions,
            IReadOnlyList<DialogAnswerCondition> conditions,
            out IReadOnlyList<DialogueAnswerExecutionEffect> effects)
        {
            var result = new List<DialogueAnswerExecutionEffect>();
            effects = result;
            if (!hasConditions || conditions == null)
            {
                return true;
            }

            // Availability is checked before applying the first effect so a stale or
            // programmatic selection cannot partially spend resources and then fail.
            if (!DialogueAnswerAvailability.AreConditionsSatisfied(
                    hasConditions,
                    conditions,
                    playerInventory,
                    playerMoneyStorage,
                    questController,
                    runtimeFlags))
            {
                return false;
            }

            foreach (DialogAnswerCondition condition in conditions)
            {
                if (condition == null)
                {
                    continue;
                }

                if (!TryExecuteCondition(condition, result))
                {
                    effects = Array.Empty<DialogueAnswerExecutionEffect>();
                    return false;
                }
            }

            return true;
        }

        private bool TryExecuteCondition(DialogAnswerCondition condition, List<DialogueAnswerExecutionEffect> effects)
        {
            switch (condition.Type)
            {
                case DialogAnswerConditionType.GiveMoney:
                {
                    int amount = Mathf.Abs(condition.MoneyAmount);
                    playerMoneyStorage.Add(amount);
                    effects.Add(DialogueAnswerExecutionEffect.DeferredMoneyReceived(amount));
                    break;
                }
                case DialogAnswerConditionType.TakeMoney:
                {
                    int amount = Mathf.Abs(condition.MoneyAmount);
                    if (!playerMoneyStorage.TrySpend(amount)) return false;
                    effects.Add(DialogueAnswerExecutionEffect.ImmediateMoneyLost(amount));
                    break;
                }
                case DialogAnswerConditionType.TakeMoneyMax:
                {
                    int amount = playerMoneyStorage.SpendUpTo(Mathf.Abs(condition.MoneyAmount));
                    effects.Add(DialogueAnswerExecutionEffect.ImmediateMoneyLost(amount));
                    break;
                }
                case DialogAnswerConditionType.TakeItemIfHas:
                {
                    int count = Mathf.Abs(condition.ItemCount);
                    if (!playerInventory.TryConsumeItemCount(condition.ItemConfig, count)) return false;
                    effects.Add(DialogueAnswerExecutionEffect.ImmediateItemLost(condition.ItemConfig, count));
                    break;
                }
                case DialogAnswerConditionType.CheckQuestStep:
                    if (!questController.CanExecuteTransition(condition.QuestGraph, condition.QuestTransition)) return false;
                    break;
                case DialogAnswerConditionType.AddQuest:
                    if (questController.TryAddQuest(condition.QuestGraph))
                        effects.Add(DialogueAnswerExecutionEffect.DeferredQuest(DialogueQuestEffectType.Added, condition.QuestGraph));
                    break;
                case DialogAnswerConditionType.CanAddQuest:
                    if (!questController.CanAddQuest(condition.QuestGraph)) return false;
                    break;
                case DialogAnswerConditionType.DoQuestStep:
                    if (!questController.TryExecuteTransition(condition.QuestGraph, condition.QuestTransition)) return false;
                    effects.Add(DialogueAnswerExecutionEffect.DeferredQuest(DialogueQuestEffectType.Updated, condition.QuestGraph));
                    break;
                case DialogAnswerConditionType.DoQuestEnd:
                    if (!questController.TryCompleteNode(condition.QuestGraph, condition.QuestNode)) return false;
                    effects.Add(DialogueAnswerExecutionEffect.DeferredQuest(DialogueQuestEffectType.Completed, condition.QuestGraph));
                    break;
                case DialogAnswerConditionType.ClearRuntimeFlag:
                    runtimeFlags?.Deactivate(condition.RuntimeFlag);
                    break;
                case DialogAnswerConditionType.SetRuntimeFlag:
                    runtimeFlags?.Activate(condition.RuntimeFlag);
                    break;
                default:
                    return false;
            }

            DialogueFlowTrace.ConditionApplied(condition);
            return true;
        }
    }

    public readonly struct DialogueAnswerExecutionEffect
    {
        public DialogueAnswerExecutionEffectType Type { get; }
        public bool IsDeferred { get; }
        public int Amount { get; }
        public global::Inventory.Item.ItemConfig Item { get; }
        public DialogueQuestEffectType QuestEffectType { get; }
        public QuestGraph Quest { get; }

        private DialogueAnswerExecutionEffect(
            DialogueAnswerExecutionEffectType type,
            bool isDeferred,
            int amount = 0,
            global::Inventory.Item.ItemConfig item = null,
            DialogueQuestEffectType questEffectType = default,
            QuestGraph quest = null)
        {
            Type = type;
            IsDeferred = isDeferred;
            Amount = amount;
            Item = item;
            QuestEffectType = questEffectType;
            Quest = quest;
        }

        public static DialogueAnswerExecutionEffect DeferredMoneyReceived(int amount) =>
            new(DialogueAnswerExecutionEffectType.MoneyReceived, true, amount);
        public static DialogueAnswerExecutionEffect ImmediateMoneyLost(int amount) =>
            new(DialogueAnswerExecutionEffectType.MoneyLost, false, amount);
        public static DialogueAnswerExecutionEffect ImmediateItemLost(global::Inventory.Item.ItemConfig item, int amount) =>
            new(DialogueAnswerExecutionEffectType.ItemLost, false, amount, item);
        public static DialogueAnswerExecutionEffect DeferredQuest(DialogueQuestEffectType type, QuestGraph quest) =>
            new(DialogueAnswerExecutionEffectType.QuestChanged, true, questEffectType: type, quest: quest);
    }

    public enum DialogueAnswerExecutionEffectType
    {
        MoneyReceived,
        MoneyLost,
        ItemLost,
        QuestChanged
    }

    public enum DialogueQuestEffectType
    {
        Added,
        Updated,
        Completed
    }
}
