using System;
using System.Collections.Generic;
using Inventory;
using Inventory.Item;
using Landings.Plants.PlantConfigs;
using Locations;
using MessagePipe;
using Messages;
using NaughtyAttributes;
using Saves;
using Sounds;
using UnityEngine;
using VContainer;

namespace Landings.Plants
{
    /// <summary>
    /// Scene renderer for a persistent fruit tree. The legacy name is retained for existing
    /// prefabs; its data model is generic and uses PlantConfig.Type == FruitTree.
    /// </summary>
    public sealed class AppleTreeFruitGrower : MonoBehaviour
    {
        [SerializeField] private PlantConfig applePlantConfig;
        [SerializeField] private ItemConfig appleItemConfig;
        [SerializeField] private Transform applePlaces;
        [SerializeField] private SoundConfig breakStickSoundConfig;
        [SerializeField] private Transform breakSoundPlace;
        [SerializeField, Tooltip("Optional explicit location. Leave empty to infer it from VillageLocationSelector.")]
        private string locationId;
        [SerializeField, ReadOnlyInInspector, Tooltip("Stable identity of this tree scene instance. Generated automatically.")]
        private string persistentId;

        private readonly List<AppleSlot> slots = new();
        private PlantWorldPersistenceService plantWorld;
        private IWorldItemDropObserver worldItemDropObserver;
        private IPublisher<PlaySoundMessage> playSoundPublisher;
        private GameObject appleVisualPrefab;
        private bool isSubscribed;
        private bool renderRequested;

        [Inject]
        public void Construct(
            PlantWorldPersistenceService plantWorld,
            IWorldItemDropObserver worldItemDropObserver,
            IPublisher<PlaySoundMessage> publisher)
        {
            this.plantWorld = plantWorld;
            this.worldItemDropObserver = worldItemDropObserver;
            playSoundPublisher = publisher;
            Subscribe();
            renderRequested = true;
        }

        /// <summary>Called before inactive locations are removed from the scene.</summary>
        public void RegisterPersistenceDefinition(PlantWorldPersistenceService service, VillageLocationSelector locationSelector)
        {
            if (service == null || !IsConfigured())
            {
                return;
            }

            EnsurePersistentId();
            string resolvedLocationId = ResolveLocationId(locationSelector);
            if (string.IsNullOrWhiteSpace(persistentId) || string.IsNullOrWhiteSpace(resolvedLocationId))
            {
                Debug.LogWarning($"Fruit tree '{name}' needs a persistent ID and a location to participate in plant saving.", this);
                return;
            }

            service.Register(new FruitTreeGrowthDefinition(
                persistentId,
                resolvedLocationId,
                applePlantConfig,
                slots.Count));
        }

        private void Awake()
        {
            if (applePlaces == null)
            {
                applePlaces = FindChildRecursive(transform, "ApplePlaces");
            }

            if (!IsConfigured())
            {
                enabled = false;
                return;
            }

            appleVisualPrefab = applePlantConfig.Stages[^1];
            foreach (Transform point in applePlaces)
            {
                slots.Add(new AppleSlot(point));
            }

            if (slots.Count == 0)
            {
                Debug.LogWarning($"{name} has no child points in ApplePlaces.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            Subscribe();
            renderRequested = true;
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearVisuals();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ClearVisuals();
        }

        private void OnValidate()
        {
            EnsurePersistentId();
        }

        private void Update()
        {
            if (!renderRequested || plantWorld == null || string.IsNullOrWhiteSpace(persistentId))
            {
                return;
            }

            renderRequested = false;
            SynchronizeVisuals();
        }

        public void TryDropAppleFromHit()
        {
            plantWorld?.TryDropFruitFromTreeHit(persistentId);
        }

        private void SynchronizeVisuals()
        {
            if (!plantWorld.TryGetFruitTree(persistentId, out YG.SavedFruitTree state))
            {
                return;
            }

            IReadOnlyList<int> pendingFalls = plantWorld.ConsumePendingFruitFalls(persistentId);
            if (pendingFalls.Count > 0)
            {
                PlayBreakSound();
                foreach (int slotIndex in pendingFalls)
                {
                    if (slotIndex >= 0 && slotIndex < slots.Count)
                    {
                        SpawnDroppedFruit(slots[slotIndex].Point);
                    }
                }
            }

            for (int index = 0; index < slots.Count; index++)
            {
                bool shouldShowFruit = state.slots != null && index < state.slots.Length && state.slots[index].isOccupied;
                AppleSlot slot = slots[index];
                if (shouldShowFruit && slot.Visual == null)
                {
                    slot.Visual = Instantiate(appleVisualPrefab, slot.Point);
                    slot.Visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    PlaySound(applePlantConfig.PlantSoundsSettings?.GrownUpSoundSettings, slot.Visual.transform.position);
                }
                else if (!shouldShowFruit && slot.Visual != null)
                {
                    Destroy(slot.Visual);
                    slot.Visual = null;
                }
            }
        }

        private void SpawnDroppedFruit(Transform point)
        {
            if (appleItemConfig == null || appleItemConfig.HandPrefab == null)
            {
                Debug.LogWarning($"{name} cannot drop a fruit: its ItemConfig.HandPrefab is missing.", this);
                return;
            }

            ItemHolder holder = Instantiate(appleItemConfig.HandPrefab, point.position, UnityEngine.Random.rotation);
            holder.Initialize(appleItemConfig);
            holder.ConfigurePersistence(true, true);
            holder.CanInteractable = true;
            worldItemDropObserver?.RegisterRuntimeDrop(holder);
        }

        private void OnFruitTreeStateChanged(string treeId)
        {
            if (string.Equals(treeId, persistentId, StringComparison.Ordinal))
            {
                renderRequested = true;
            }
        }

        private void Subscribe()
        {
            if (isSubscribed || plantWorld == null)
            {
                return;
            }

            plantWorld.FruitTreeStateChanged += OnFruitTreeStateChanged;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || plantWorld == null)
            {
                return;
            }

            plantWorld.FruitTreeStateChanged -= OnFruitTreeStateChanged;
            isSubscribed = false;
        }

        private void ClearVisuals()
        {
            foreach (AppleSlot slot in slots)
            {
                if (slot.Visual != null)
                {
                    Destroy(slot.Visual);
                    slot.Visual = null;
                }
            }
        }

        private bool IsConfigured()
        {
            if (applePlantConfig == null || applePlantConfig.Stages == null || applePlantConfig.Stages.Count == 0)
            {
                Debug.LogError($"{name} cannot grow fruit: PlantConfig has no stages.", this);
                return false;
            }

            if (applePlantConfig.Type != PlantType.FruitTree)
            {
                Debug.LogError($"{name} cannot grow fruit: PlantConfig must have type FruitTree.", this);
                return false;
            }

            if (applePlaces == null)
            {
                Debug.LogError($"{name} cannot grow fruit: ApplePlaces is missing.", this);
                return false;
            }

            return applePlantConfig.Stages[^1] != null;
        }

        private string ResolveLocationId(VillageLocationSelector selector)
        {
            if (!string.IsNullOrWhiteSpace(locationId))
            {
                return locationId;
            }

            if (selector == null)
            {
                return null;
            }

            foreach (VillageLocationDefinition location in selector.Locations)
            {
                foreach (GameObject root in location.RequiredObjects)
                {
                    if (root != null && (transform.IsChildOf(root.transform) || root.transform == transform))
                    {
                        return location.Id;
                    }
                }
            }

            return null;
        }

        private void EnsurePersistentId()
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
            {
                persistentId = string.Empty;
                return;
            }
#endif
            if (string.IsNullOrWhiteSpace(persistentId))
            {
                string configId = applePlantConfig != null && !string.IsNullOrWhiteSpace(applePlantConfig.Id)
                    ? applePlantConfig.Id
                    : "fruit_tree";
                persistentId = $"{configId}_tree_{Guid.NewGuid():N}";
            }
        }

        private void PlaySound(SoundSettings settings, Vector3 position)
        {
            if (settings != null && playSoundPublisher != null)
            {
                playSoundPublisher.Publish(new PlaySoundMessage(settings, position, null));
            }
        }

        private void PlayBreakSound()
        {
            if (breakStickSoundConfig != null && breakSoundPlace != null)
            {
                PlaySound(breakStickSoundConfig.SoundSettings, breakSoundPlace.position);
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            foreach (Transform child in root)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private sealed class AppleSlot
        {
            public AppleSlot(Transform point)
            {
                Point = point;
            }

            public Transform Point { get; }
            public GameObject Visual { get; set; }
        }
    }
}
