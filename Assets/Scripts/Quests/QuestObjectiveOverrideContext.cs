using System;
using Quests.Graph;

namespace Quests
{
    /// <summary>Runtime text for a system-owned current objective, without mutating authored quest data.</summary>
    public sealed class QuestObjectiveOverrideContext
    {
        public QuestGraph Quest { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public event Action Changed;

        public bool AppliesTo(QuestGraph quest) => quest != null && quest == Quest;
        public void Set(QuestGraph quest, string title, string description)
        {
            Quest = quest; Title = title; Description = description; Changed?.Invoke();
        }
        public void Clear(QuestGraph quest)
        {
            if (!AppliesTo(quest)) return;
            Quest = null; Title = null; Description = null; Changed?.Invoke();
        }
    }
}
