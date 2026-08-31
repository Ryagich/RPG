using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Item;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer.Unity;

namespace Inventory.Storage
{
    /// <summary>
    /// Project-lifetime catalogue of item assets. Asset discovery stays in the Addressables layer;
    /// consumers only query already loaded item configurations.
    /// </summary>
    public sealed class ItemStorage : IStartable, IDisposable
    {
        public const string AddressablesLabel = "ItemConfig";

        private readonly TaskCompletionSource<bool> readyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<ItemConfig> items = new();
        private readonly Dictionary<ItemCategory, IReadOnlyList<ItemConfig>> itemsByCategory = new();

        private AsyncOperationHandle<IList<ItemConfig>> loadHandle;
        private bool hasLoadHandle;
        private bool disposed;

        public Task Ready => readyCompletion.Task;
        public IReadOnlyList<ItemConfig> Items => items;

        public void Start()
        {
            LoadAsync();
        }

        public IReadOnlyList<ItemConfig> GetByCategory(ItemCategory category)
        {
            return itemsByCategory.TryGetValue(category, out var categoryItems)
                ? categoryItems
                : Array.Empty<ItemConfig>();
        }

        public void Dispose()
        {
            disposed = true;
            if (hasLoadHandle && loadHandle.IsValid())
            {
                Addressables.Release(loadHandle);
            }

            hasLoadHandle = false;
            readyCompletion.TrySetResult(false);
        }

        private async void LoadAsync()
        {
            try
            {
                loadHandle = Addressables.LoadAssetsAsync<ItemConfig>(AddressablesLabel, null);
                hasLoadHandle = true;
                var loadedItems = await loadHandle.Task;
                if (disposed || loadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    return;
                }

                BuildCatalogue(loadedItems);
            }
            catch (Exception exception)
            {
                if (!disposed)
                {
                    Debug.LogError($"Failed to load item catalogue with Addressables label '{AddressablesLabel}': {exception}");
                }
            }
            finally
            {
                readyCompletion.TrySetResult(!disposed && items.Count > 0);
            }
        }

        private void BuildCatalogue(IList<ItemConfig> loadedItems)
        {
            items.Clear();
            itemsByCategory.Clear();

            if (loadedItems == null || loadedItems.Count == 0)
            {
                Debug.LogWarning($"Item catalogue label '{AddressablesLabel}' did not load any {nameof(ItemConfig)} assets.");
                return;
            }

            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var itemConfig in loadedItems.Where(config => config != null).Distinct())
            {
                if (string.IsNullOrWhiteSpace(itemConfig.Id))
                {
                    Debug.LogWarning($"Item catalogue ignored '{itemConfig.name}' because its Id is empty.", itemConfig);
                    continue;
                }

                if (!usedIds.Add(itemConfig.Id))
                {
                    Debug.LogWarning($"Item catalogue ignored duplicate item Id '{itemConfig.Id}' on '{itemConfig.name}'.", itemConfig);
                    continue;
                }

                items.Add(itemConfig);
            }

            foreach (var group in items.GroupBy(config => config.ItemCategory))
            {
                itemsByCategory.Add(group.Key, group.ToArray());
            }
        }
    }
}
