namespace YG
{
    [System.Serializable]
    public struct SavedQuestProgress
    {
        public string questId;
        public string currentNodeId;
        public int state;
    }

    [System.Serializable]
    public struct SavedInventoryItem
    {
        public string itemId;
        public int count;
        public int x;
        public int y;
        public int rotated;
        public int slot;
    }

    [System.Serializable]
    public struct SavedFactionRelation
    {
        public string leftFactionId;
        public string rightFactionId;
        public int relation;
    }

    public partial class SavesYG
    {
        public bool GameReadyMetricSend;
        public int saveVersion;
        public string locationId;
        public string entranceId;
        public int money;
        public float health;
        public float stamina;
        public SavedQuestProgress[] quests;
        public SavedInventoryItem[] inventory;
        public int millScenarioStage;
        public int[] millOutcomeFlags;
        public SavedFactionRelation[] factionRelations;
    }
}
