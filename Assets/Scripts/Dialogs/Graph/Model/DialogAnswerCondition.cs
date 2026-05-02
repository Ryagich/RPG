using System;
using Inventory.Item;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEngine;

namespace Dialogs.Graph.Model
{
    public enum DialogAnswerConditionType
    {
        GiveMoney = 0,
        TakeMoney = 1,
        TakeMoneyMax = 2,
        TakeItemIfHas = 3,
        CheckQuestStep = 4,
        AddQuest = 5,
        DoQuestStep = 6,
        DoQuestEnd = 7
    }

    [Serializable]
    public class DialogAnswerCondition
    {
        [SerializeField] private DialogAnswerConditionType type;
        [SerializeField] private int moneyAmount;
        [SerializeField] private ItemConfig itemConfig;
        [SerializeField] private int itemCount = 1;
        [SerializeField] private QuestGraph questGraph;
        [SerializeField] private QuestNodeData questSourceNode;
        [SerializeField] private QuestTransition questTransition;
        [SerializeField] private QuestNodeData questNode;

        public DialogAnswerConditionType Type => type;
        public int MoneyAmount => moneyAmount;
        public ItemConfig ItemConfig => itemConfig;
        public int ItemCount => itemCount;
        public QuestGraph QuestGraph => questGraph;
        public QuestNodeData QuestSourceNode => questSourceNode;
        public QuestTransition QuestTransition => questTransition;
        public QuestNodeData QuestNode => questNode;
    }
}
