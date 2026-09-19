using System;
using System.Collections.Generic;
using System.Linq;
using Container.Game;
using Inventory;
using Inventory.Item;
using Inventory.Storage;
using Loading;
using Locations;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;
using YG;

namespace Saves
{
    /// <summary>
    /// Reconciles persistent world-item records with the active gameplay location. It changes
    /// only the in-memory save snapshot; complete checkpoint owners decide when it is persisted.
    /// </summary>
    public sealed class DroppedItemPersistenceService : IStartable, ITickable, IDisposable, IWorldItemDropObserver
    {
        private const float DefaultLifetimeSeconds = 600f;
        private const float TimerStepSeconds = 1f;

        private readonly GameWorldBootstrapper worldBootstrapper;
        private readonly GameSaveController saveController;
        private readonly ItemStorage itemStorage;
        private readonly LocationTransitionService locationTransitions;
        private readonly GameSceneSessionConfiguration sceneSessionConfiguration;
        private readonly SceneLoadingService sceneLoadingService;
        private readonly Dictionary<string, TrackedWorldItem> activeItems = new(StringComparer.Ordinal);
        private readonly List<SavedWorldItem> worldItems = new();
        private readonly HashSet<string> retiredSceneItemIds = new(StringComparer.Ordinal);

        private float timerAccumulator;
        private bool isReady;
        private bool isDisposed;
        private bool isSceneUnloading;

        public DroppedItemPersistenceService(
            GameWorldBootstrapper worldBootstrapper,
            GameSaveController saveController,
            ItemStorage itemStorage,
            LocationTransitionService locationTransitions,
            GameSceneSessionConfiguration sceneSessionConfiguration,
            SceneLoadingService sceneLoadingService)
        {
            this.worldBootstrapper = worldBootstrapper;
            this.saveController = saveController;
            this.itemStorage = itemStorage;
            this.locationTransitions = locationTransitions;
            this.sceneSessionConfiguration = sceneSessionConfiguration;
            this.sceneLoadingService = sceneLoadingService;
        }

        public async void Start()
        {
            await worldBootstrapper.WorldInitialized;
            await saveController.Ready;
            if (isDisposed || !sceneSessionConfiguration.IsGameplayScene)
            {
                return;
            }

            if (!await itemStorage.Ready)
            {
                Debug.LogError("Persistent world items were not initialized because the item catalogue failed to load.");
                return;
            }

            worldItems.AddRange(saveController.GetWorldItems());
            retiredSceneItemIds.UnionWith(saveController.GetRetiredSceneWorldItemIds());
            RestoreCurrentLocation();
            isReady = true;
            saveController.CheckpointPreparing += SynchronizeCurrentLocation;
            sceneLoadingService.LoadRequested += MarkSceneUnloading;
        }

        public void Dispose()
        {
            isDisposed = true;
            saveController.CheckpointPreparing -= SynchronizeCurrentLocation;
            sceneLoadingService.LoadRequested -= MarkSceneUnloading;
            foreach (TrackedWorldItem tracked in activeItems.Values.ToArray())
            {
                Untrack(tracked.Holder);
            }
        }

        public void RegisterRuntimeDrop(ItemHolder itemHolder)
        {
            if (!isReady || isDisposed || !sceneSessionConfiguration.IsGameplayScene ||
                itemHolder == null || !itemHolder.UsesSave ||
                string.IsNullOrWhiteSpace(itemHolder.PersistentId) || itemHolder.Config == null)
            {
                return;
            }

            string locationId = locationTransitions.CurrentLocation?.Id;
            if (string.IsNullOrWhiteSpace(locationId))
            {
                Debug.LogWarning($"Persistent world item '{itemHolder.name}' was created without an active location.", itemHolder);
                return;
            }

            if (activeItems.ContainsKey(itemHolder.PersistentId) || FindWorldItemIndex(itemHolder.PersistentId) >= 0)
            {
                Debug.LogError($"Duplicate persistent world item id '{itemHolder.PersistentId}'.", itemHolder);
                return;
            }

            worldItems.Add(CreateState(itemHolder, locationId, isSceneAuthored: false));
            Track(itemHolder, isSceneAuthored: false);
            PublishWorkingState();
        }

        public void Tick()
        {
            if (!isReady || isDisposed || Time.timeScale <= 0f)
            {
                return;
            }

            timerAccumulator += Time.deltaTime;
            while (timerAccumulator >= TimerStepSeconds)
            {
                timerAccumulator -= TimerStepSeconds;
                AdvanceLifetime();
            }
        }

        private void RestoreCurrentLocation()
        {
            string locationId = locationTransitions.CurrentLocation?.Id;
            if (string.IsNullOrWhiteSpace(locationId))
            {
                Debug.LogError("Persistent world items cannot be restored because the active location is unknown.");
                return;
            }

            var authoredById = new Dictionary<string, ItemHolder>(StringComparer.Ordinal);
            foreach (ItemHolder holder in FindActivePersistentHolders())
            {
                if (!authoredById.TryAdd(holder.PersistentId, holder))
                {
                    Debug.LogError($"Several authored ItemHolders use persistent id '{holder.PersistentId}'.", holder);
                }
            }

            foreach (SavedWorldItem savedItem in worldItems.Where(item => item.locationId == locationId).ToArray())
            {
                if (!IsStateValid(savedItem) || (savedItem.hasLifetime && savedItem.remainingLifetimeSeconds <= 0f))
                {
                    RemoveRecord(savedItem.persistentId, destroyTrackedObject: false);
                    continue;
                }

                if (!itemStorage.TryGetById(savedItem.itemId, out ItemConfig config) || config.HandPrefab == null)
                {
                    Debug.LogWarning($"Saved world item '{savedItem.itemId}' cannot be restored because its prefab is unavailable.");
                    RemoveRecord(savedItem.persistentId, destroyTrackedObject: false);
                    continue;
                }

                if (authoredById.Remove(savedItem.persistentId, out ItemHolder authoredHolder))
                {
                    RestoreHolder(authoredHolder, config, savedItem);
                    Track(authoredHolder, savedItem.isSceneAuthored);
                    continue;
                }

                var holder = Object.Instantiate(config.HandPrefab, ToPosition(savedItem), ToRotation(savedItem));
                holder.Initialize(config, savedItem.count);
                holder.ConfigurePersistence(true, savedItem.hasLifetime, savedItem.persistentId);
                holder.CanInteractable = true;
                Track(holder, savedItem.isSceneAuthored);
            }

            foreach (ItemHolder authoredHolder in authoredById.Values)
            {
                if (retiredSceneItemIds.Contains(authoredHolder.PersistentId))
                {
                    Object.Destroy(authoredHolder.gameObject);
                    continue;
                }

                worldItems.Add(CreateState(authoredHolder, locationId, isSceneAuthored: true));
                Track(authoredHolder, isSceneAuthored: true);
            }

            PublishWorkingState();
        }

        private void SynchronizeCurrentLocation()
        {
            if (!isReady || isDisposed)
            {
                return;
            }

            string locationId = locationTransitions.CurrentLocation?.Id;
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return;
            }

            foreach (TrackedWorldItem tracked in activeItems.Values.ToArray())
            {
                if (tracked.Holder == null)
                {
                    continue;
                }

                UpdateRecord(tracked.Holder, locationId, tracked.IsSceneAuthored);
            }

            PublishWorkingState();
        }

        private void AdvanceLifetime()
        {
            var expiredIds = new List<string>();
            for (var index = 0; index < worldItems.Count; index++)
            {
                SavedWorldItem item = worldItems[index];
                if (!item.hasLifetime)
                {
                    continue;
                }

                item.remainingLifetimeSeconds = Mathf.Max(0f, item.remainingLifetimeSeconds - TimerStepSeconds);
                worldItems[index] = item;
                if (item.remainingLifetimeSeconds <= 0f)
                {
                    expiredIds.Add(item.persistentId);
                }
            }

            foreach (string persistentId in expiredIds)
            {
                RemoveRecord(persistentId, destroyTrackedObject: true);
            }

            PublishWorkingState();
        }

        private void Track(ItemHolder holder, bool isSceneAuthored)
        {
            if (holder == null || string.IsNullOrWhiteSpace(holder.PersistentId))
            {
                return;
            }

            if (activeItems.TryGetValue(holder.PersistentId, out TrackedWorldItem current) && current.Holder != holder)
            {
                Debug.LogError($"Several runtime ItemHolders use persistent id '{holder.PersistentId}'.", holder);
                return;
            }

            Untrack(holder);
            activeItems[holder.PersistentId] = new TrackedWorldItem(holder, isSceneAuthored);
            holder.Destroyed += OnTrackedHolderDestroyed;
            holder.StateChanged += OnTrackedHolderStateChanged;
        }

        private void Untrack(ItemHolder holder)
        {
            if (holder == null || string.IsNullOrWhiteSpace(holder.PersistentId))
            {
                return;
            }

            holder.Destroyed -= OnTrackedHolderDestroyed;
            holder.StateChanged -= OnTrackedHolderStateChanged;
            activeItems.Remove(holder.PersistentId);
        }

        private void OnTrackedHolderDestroyed(ItemHolder holder)
        {
            if (isDisposed || isSceneUnloading || ReferenceEquals(holder, null))
            {
                return;
            }

            string persistentId = holder.PersistentId;
            if (string.IsNullOrWhiteSpace(persistentId) ||
                !activeItems.TryGetValue(persistentId, out TrackedWorldItem tracked))
            {
                return;
            }

            activeItems.Remove(persistentId);
            if (tracked.IsSceneAuthored)
            {
                retiredSceneItemIds.Add(persistentId);
            }

            RemoveRecord(persistentId, destroyTrackedObject: false);
            PublishWorkingState();
        }

        private void MarkSceneUnloading()
        {
            isSceneUnloading = true;
        }

        private void OnTrackedHolderStateChanged(ItemHolder holder)
        {
            if (!isReady || isDisposed || holder == null ||
                !activeItems.TryGetValue(holder.PersistentId, out TrackedWorldItem tracked))
            {
                return;
            }

            string locationId = locationTransitions.CurrentLocation?.Id;
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return;
            }

            UpdateRecord(holder, locationId, tracked.IsSceneAuthored);
            PublishWorkingState();
        }

        private void UpdateRecord(ItemHolder holder, string locationId, bool isSceneAuthored)
        {
            int index = FindWorldItemIndex(holder.PersistentId);
            if (index < 0)
            {
                worldItems.Add(CreateState(holder, locationId, isSceneAuthored));
                return;
            }

            SavedWorldItem state = worldItems[index];
            state.itemId = holder.Config.Id;
            state.count = holder.Count;
            state.locationId = locationId;
            state.positionX = holder.transform.position.x;
            state.positionY = holder.transform.position.y;
            state.positionZ = holder.transform.position.z;
            state.rotationX = holder.transform.rotation.x;
            state.rotationY = holder.transform.rotation.y;
            state.rotationZ = holder.transform.rotation.z;
            state.rotationW = holder.transform.rotation.w;
            state.hasLifetime = holder.UsesLifetime;
            state.isSceneAuthored = isSceneAuthored;
            worldItems[index] = state;
        }

        private void RestoreHolder(ItemHolder holder, ItemConfig config, SavedWorldItem state)
        {
            holder.Initialize(config, state.count);
            holder.ConfigurePersistence(true, state.hasLifetime, state.persistentId);
            holder.transform.SetPositionAndRotation(ToPosition(state), ToRotation(state));
            holder.CanInteractable = true;
        }

        private SavedWorldItem CreateState(ItemHolder holder, string locationId, bool isSceneAuthored)
        {
            return new SavedWorldItem
            {
                persistentId = holder.PersistentId,
                itemId = holder.Config.Id,
                count = holder.Count,
                locationId = locationId,
                positionX = holder.transform.position.x,
                positionY = holder.transform.position.y,
                positionZ = holder.transform.position.z,
                rotationX = holder.transform.rotation.x,
                rotationY = holder.transform.rotation.y,
                rotationZ = holder.transform.rotation.z,
                rotationW = holder.transform.rotation.w,
                hasLifetime = holder.UsesLifetime,
                remainingLifetimeSeconds = holder.UsesLifetime ? DefaultLifetimeSeconds : 0f,
                isSceneAuthored = isSceneAuthored
            };
        }

        private void RemoveRecord(string persistentId, bool destroyTrackedObject)
        {
            int index = FindWorldItemIndex(persistentId);
            if (index >= 0)
            {
                SavedWorldItem state = worldItems[index];
                if (state.isSceneAuthored)
                {
                    retiredSceneItemIds.Add(persistentId);
                }

                worldItems.RemoveAt(index);
            }

            if (!activeItems.TryGetValue(persistentId, out TrackedWorldItem tracked))
            {
                return;
            }

            Untrack(tracked.Holder);
            if (destroyTrackedObject && tracked.Holder != null)
            {
                Object.Destroy(tracked.Holder.gameObject);
            }
        }

        private IEnumerable<ItemHolder> FindActivePersistentHolders()
        {
            return Object.FindObjectsByType<ItemHolder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(holder => holder != null && holder.UsesSave &&
                                 !string.IsNullOrWhiteSpace(holder.PersistentId) && holder.Config != null);
        }

        private bool IsStateValid(SavedWorldItem item)
        {
            return !string.IsNullOrWhiteSpace(item.persistentId) &&
                   !string.IsNullOrWhiteSpace(item.itemId) && item.count > 0;
        }

        private int FindWorldItemIndex(string persistentId)
        {
            return worldItems.FindIndex(item => string.Equals(item.persistentId, persistentId, StringComparison.Ordinal));
        }

        private void PublishWorkingState()
        {
            saveController.SetWorldItemState(worldItems, retiredSceneItemIds);
        }

        private static Vector3 ToPosition(SavedWorldItem state) =>
            new(state.positionX, state.positionY, state.positionZ);

        private static Quaternion ToRotation(SavedWorldItem state)
        {
            var rotation = new Quaternion(state.rotationX, state.rotationY, state.rotationZ, state.rotationW);
            float lengthSquared = rotation.x * rotation.x + rotation.y * rotation.y +
                                  rotation.z * rotation.z + rotation.w * rotation.w;
            return lengthSquared < 0.0001f ? Quaternion.identity : rotation.normalized;
        }

        private readonly struct TrackedWorldItem
        {
            public TrackedWorldItem(ItemHolder holder, bool isSceneAuthored)
            {
                Holder = holder;
                IsSceneAuthored = isSceneAuthored;
            }

            public ItemHolder Holder { get; }
            public bool IsSceneAuthored { get; }
        }
    }
}
