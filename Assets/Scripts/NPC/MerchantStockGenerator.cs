using System;
using System.Collections.Generic;
using System.Threading;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Storage;
using UnityEngine;
using VContainer.Unity;

namespace NPC
{
    /// <summary>
    /// Populates one merchant inventory once for the lifetime of its scope.
    /// </summary>
    public sealed class MerchantStockGenerator : IStartable, IDisposable
    {
        private readonly MerchantInventory inventory;
        private readonly ItemStorage itemStorage;
        private readonly MerchantStockSettings settings;
        private readonly CancellationTokenSource disposeCancellation = new();

        public MerchantStockGenerator(MerchantInventory inventory, ItemStorage itemStorage, MerchantStockSettings settings)
        {
            this.inventory = inventory;
            this.itemStorage = itemStorage;
            this.settings = settings;
        }

        public void Start()
        {
            GenerateAsync();
        }

        public void Dispose()
        {
            disposeCancellation.Cancel();
            disposeCancellation.Dispose();
        }

        private async void GenerateAsync()
        {
            if (inventory == null || itemStorage == null || settings == null)
            {
                return;
            }

            await itemStorage.Ready;
            if (disposeCancellation.IsCancellationRequested)
            {
                return;
            }

            foreach (var rule in settings.Rules)
            {
                if (disposeCancellation.IsCancellationRequested || rule == null
                    || !settings.TryGetUniqueItemRange(rule.UniqueItemCount, out var uniqueItemRange)
                    || !settings.TryGetItemAmountRange(rule.ItemAmount, out var amountRange))
                {
                    continue;
                }

                var itemCount = RandomInclusive(uniqueItemRange);
                if (itemCount <= 0)
                {
                    continue;
                }

                var candidates = itemStorage.GetByCategory(rule.Category);
                foreach (var itemConfig in TakeRandomDistinct(candidates, itemCount))
                {
                    var amount = RandomInclusive(amountRange);
                    if (amount > 0)
                    {
                        inventory.TryAdd(new ItemStack(itemConfig, amount));
                    }
                }
            }
        }

        private static int RandomInclusive(Vector2Int range)
        {
            return range.y <= range.x ? range.x : UnityEngine.Random.Range(range.x, range.y + 1);
        }

        private static IEnumerable<ItemConfig> TakeRandomDistinct(IReadOnlyList<ItemConfig> candidates, int count)
        {
            if (candidates == null || candidates.Count == 0 || count <= 0)
            {
                yield break;
            }

            var shuffled = new List<ItemConfig>(candidates);
            for (var index = shuffled.Count - 1; index > 0; index--)
            {
                var swapIndex = UnityEngine.Random.Range(0, index + 1);
                (shuffled[index], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[index]);
            }

            var selectedCount = Mathf.Min(count, shuffled.Count);
            for (var index = 0; index < selectedCount; index++)
            {
                yield return shuffled[index];
            }
        }
    }
}
