namespace YG
{
    [System.Serializable]
    public struct SavedQuestProgress
    {
        public string questId;
        public string currentNodeId;
        public string[] completedNodeIds;
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

    [System.Serializable]
    public struct SavedWorldItem
    {
        public string persistentId;
        public string itemId;
        public int count;
        public string locationId;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float rotationW;
        public bool hasLifetime;
        public float remainingLifetimeSeconds;
        public bool isSceneAuthored;
    }

    [System.Serializable]
    public struct SavedPlayerPose
    {
        public int isValid;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float rotationW;
    }

    [System.Serializable]
    public struct SavedCharacterPose
    {
        public int isValid;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float rotationW;
    }

    /// <summary>
    /// Persistent state shared by the player and scene NPCs. Runtime objects and Unity asset
    /// references deliberately do not cross this boundary: items are restored through itemId.
    /// </summary>
    [System.Serializable]
    public class SavedCharacterState
    {
        public string characterId;
        public bool isAlive;
        public float health;
        public float stamina;
        public float water;
        public float food;
        public SavedInventoryItem[] inventory;
        public SavedCharacterPose deathPose;
    }

    public partial class SavesYG
    {
        public bool GameReadyMetricSend;
        public int saveVersion;
        public string locationId;
        public string entranceId;
        public SavedPlayerPose playerPose;
        public int money;
        public float health;
        public float stamina;
        public float water;
        public float food;
        // Canonical player state since v6. The primitive fields above are retained only to load
        // saves written before character state was introduced.
        public SavedCharacterState playerCharacter;
        public SavedCharacterState[] npcCharacters;
        public SavedQuestProgress[] quests;
        public SavedInventoryItem[] inventory;
        // Read only by the v3-to-v4 migration. Mill progress is otherwise stored in quests.
        public int millScenarioStage;
        public int[] millOutcomeFlags;
        public SavedFactionRelation[] factionRelations;
        public SavedWorldItem[] worldItems;
        public string[] retiredSceneWorldItemIds;
    }
}
