using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inventory.Item
{
    [CreateAssetMenu(fileName = "ItemLootSetConfig", menuName = "configs/Inventory/Item Loot Set Config")]
    public sealed class ItemLootSetConfig : ScriptableObject
    {
        [field: SerializeField] public List<ItemLootEntry> Entries { get; private set; } = new();

        private void OnValidate()
        {
            if (Entries == null)
            {
                return;
            }

            foreach (var entry in Entries)
            {
                entry?.NormalizeAmountRange();
            }
        }
    }

    [Serializable]
    public sealed class ItemLootEntry
    {
        [field: SerializeField] public ItemConfig ItemConfig { get; private set; }
        [field: SerializeField, Min(1)] public int MinAmount { get; private set; } = 1;
        [field: SerializeField, Min(1)] public int MaxAmount { get; private set; } = 1;
        [field: SerializeField, Range(0f, 1f)] public float Chance { get; private set; } = 1f;

        public bool IsValid => ItemConfig != null && MinAmount >= 1 && MaxAmount >= MinAmount;

        public void NormalizeAmountRange()
        {
            MinAmount = Mathf.Max(1, MinAmount);
            MaxAmount = Mathf.Max(MinAmount, MaxAmount);
            Chance = Mathf.Clamp01(Chance);
        }
    }
}
