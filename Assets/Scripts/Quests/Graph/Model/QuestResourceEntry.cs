using System;
using Inventory.Item;
using UnityEngine;

namespace Quests.Graph.Model
{
    public enum QuestResourceEntryType
    {
        Money = 0,
        Item = 1
    }

    [Serializable]
    public class QuestResourceEntry
    {
        [SerializeField] private QuestResourceEntryType type;
        [SerializeField] private int moneyAmount;
        [SerializeField] private ItemConfig itemConfig;
        [SerializeField] private int itemCount = 1;

        public QuestResourceEntryType Type => type;
        public int MoneyAmount => moneyAmount;
        public ItemConfig ItemConfig => itemConfig;
        public int ItemCount => itemCount;
    }
}
