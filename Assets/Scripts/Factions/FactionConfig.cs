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
        [field: Tooltip("Fallback combat preferences. Used when the faction has no profiles in the randomized profile list.")]
        [field: SerializeField] public NpcCombatProfile CombatProfile { get; private set; }
        [field: Tooltip("Combat profiles randomly assigned to newly created faction NPCs. A specific NPC can override its assigned profile in its lifetime scope.")]
        [field: SerializeField] public List<NpcCombatProfile> CombatProfiles { get; private set; } = new();
        [field: Header("Initial Inventory")]
        [field: SerializeField] public List<ItemSetConfig> ItemSetConfigs { get; private set; } = new();
        [field: SerializeField] public List<ItemLootSetConfig> ItemLootSetConfigs { get; private set; } = new();

        public NpcCombatProfile GetRandomCombatProfile()
        {
            if (CombatProfiles == null || CombatProfiles.Count == 0)
            {
                return CombatProfile;
            }

            var validProfileCount = 0;
            foreach (var combatProfile in CombatProfiles)
            {
                if (combatProfile != null)
                {
                    validProfileCount++;
                }
            }

            if (validProfileCount == 0)
            {
                return CombatProfile;
            }

            var selectedProfileIndex = Random.Range(0, validProfileCount);
            foreach (var combatProfile in CombatProfiles)
            {
                if (combatProfile == null)
                {
                    continue;
                }

                if (selectedProfileIndex-- == 0)
                {
                    return combatProfile;
                }
            }

            return CombatProfile;
        }

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

        public ItemLootSetConfig GetRandomItemLootSetConfig()
        {
            if (ItemLootSetConfigs == null || ItemLootSetConfigs.Count == 0)
            {
                return null;
            }

            var validConfigCount = 0;
            foreach (var itemLootSetConfig in ItemLootSetConfigs)
            {
                if (itemLootSetConfig != null)
                {
                    validConfigCount++;
                }
            }

            if (validConfigCount == 0)
            {
                return null;
            }

            var selectedConfigIndex = Random.Range(0, validConfigCount);
            foreach (var itemLootSetConfig in ItemLootSetConfigs)
            {
                if (itemLootSetConfig == null)
                {
                    continue;
                }

                if (selectedConfigIndex-- == 0)
                {
                    return itemLootSetConfig;
                }
            }

            return null;
        }
    }
}
