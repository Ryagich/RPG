using System.Collections.Generic;
using Quests.Graph;
using UnityEngine;

namespace Quests
{
    /// <summary>
    /// Explicit catalogue of authored quest definitions eligible for save restoration.
    /// It avoids runtime asset-path discovery and makes the save contract inspectable in data.
    /// </summary>
    [CreateAssetMenu(fileName = "Quest Catalog", menuName = "configs/Quests/Catalog")]
    public sealed class QuestCatalog : ScriptableObject
    {
        [SerializeField] private List<QuestGraph> quests = new();

        public IReadOnlyList<QuestGraph> Quests => quests;

        public bool TryGet(string persistentId, out QuestGraph questGraph)
        {
            questGraph = null;
            if (string.IsNullOrWhiteSpace(persistentId))
            {
                return false;
            }

            foreach (QuestGraph candidate in quests)
            {
                if (candidate != null && candidate.PersistentId == persistentId)
                {
                    questGraph = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
