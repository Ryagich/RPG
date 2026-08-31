using System;
using System.Collections.Generic;
using Inventory.Item;
using UnityEngine;

namespace NPC
{
    public enum MerchantStockLevel
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    [Serializable]
    public sealed class MerchantStockRule
    {
        [field: SerializeField] public ItemCategory Category { get; private set; }
        [field: SerializeField] public MerchantStockLevel UniqueItemCount { get; private set; }
        [field: SerializeField] public MerchantStockLevel ItemAmount { get; private set; }
    }

    [Serializable]
    public sealed class MerchantUniqueItemCountRange
    {
        [field: SerializeField] public MerchantStockLevel Value { get; private set; }
        [field: SerializeField, Min(0)] public int Min { get; private set; }
        [field: SerializeField, Min(0)] public int Max { get; private set; }
    }

    [Serializable]
    public sealed class MerchantItemAmountRange
    {
        [field: SerializeField] public MerchantStockLevel Value { get; private set; }
        [field: SerializeField, Min(0)] public int Min { get; private set; }
        [field: SerializeField, Min(0)] public int Max { get; private set; }
    }

    public sealed class MerchantStockSettings
    {
        private readonly IReadOnlyList<MerchantStockRule> rules;
        private readonly Dictionary<MerchantStockLevel, Vector2Int> uniqueItemRanges;
        private readonly Dictionary<MerchantStockLevel, Vector2Int> itemAmountRanges;

        public IReadOnlyList<MerchantStockRule> Rules => rules;

        public MerchantStockSettings(
            IReadOnlyList<MerchantStockRule> rules,
            IReadOnlyList<MerchantUniqueItemCountRange> uniqueItemRanges,
            IReadOnlyList<MerchantItemAmountRange> itemAmountRanges)
        {
            this.rules = rules == null ? Array.Empty<MerchantStockRule>() : new List<MerchantStockRule>(rules);
            this.uniqueItemRanges = BuildRanges(uniqueItemRanges, range => range.Value, range => range.Min, range => range.Max);
            this.itemAmountRanges = BuildRanges(itemAmountRanges, range => range.Value, range => range.Min, range => range.Max);
        }

        public bool TryGetUniqueItemRange(MerchantStockLevel value, out Vector2Int range)
        {
            return uniqueItemRanges.TryGetValue(value, out range);
        }

        public bool TryGetItemAmountRange(MerchantStockLevel value, out Vector2Int range)
        {
            return itemAmountRanges.TryGetValue(value, out range);
        }

        private static Dictionary<TValue, Vector2Int> BuildRanges<TRange, TValue>(
            IReadOnlyList<TRange> ranges,
            Func<TRange, TValue> getValue,
            Func<TRange, int> getMin,
            Func<TRange, int> getMax)
            where TValue : struct
            where TRange : class
        {
            var result = new Dictionary<TValue, Vector2Int>();
            if (ranges == null)
            {
                return result;
            }

            foreach (var range in ranges)
            {
                if (range == null || result.ContainsKey(getValue(range)))
                {
                    continue;
                }

                var min = Mathf.Max(0, getMin(range));
                var max = Mathf.Max(min, getMax(range));
                result.Add(getValue(range), new Vector2Int(min, max));
            }

            return result;
        }
    }
}
