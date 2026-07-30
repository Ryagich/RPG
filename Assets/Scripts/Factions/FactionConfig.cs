using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Inventory.Item;
using NPC;

namespace Factions
{
    [CreateAssetMenu(fileName = "FactionConfig", menuName = "configs/Factions/Faction")]
    public sealed class FactionConfig : ScriptableObject
    {
        [field: SerializeField] public LocalizedString Name { get; private set; } = new("Tables", "Null String");
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: Header("Combat AI")]
        [field: Tooltip("Baseline combat preferences for faction NPCs. A specific NPC can override this in its lifetime scope.")]
        [field: SerializeField] public NpcCombatProfile CombatProfile { get; private set; }
        [field: Header("Initial Inventory")]
        [field: SerializeField] public List<ItemSetConfig> ItemSetConfigs { get; private set; } = new();

        public ItemSetConfig GetRandomItemSetConfig()
        {
            if (ItemSetConfigs == null || ItemSetConfigs.Count == 0)
            {
                return null;
            }

            var validConfigCount = 0;
            foreach (var itemSetConfig in ItemSetConfigs)
            {
                if (itemSetConfig != null)
                {
                    validConfigCount++;
                }
            }

            if (validConfigCount == 0)
            {
                return null;
            }

            var selectedConfigIndex = Random.Range(0, validConfigCount);
            foreach (var itemSetConfig in ItemSetConfigs)
            {
                if (itemSetConfig == null)
                {
                    continue;
                }

                if (selectedConfigIndex-- == 0)
                {
                    return itemSetConfig;
                }
            }

            return null;
        }
    }
}
