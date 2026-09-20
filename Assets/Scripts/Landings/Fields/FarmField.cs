using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Inventory.Item;
using Landings.Plants;
using Landings.Plants.PlantConfigs;
using Locations;
using MessagePipe;
using Messages;
using NaughtyAttributes;
using Saves;
using Sounds;
using UnityEngine;
using VContainer;

namespace Landings.Fields
{
    /// <summary>
    /// Scene-side renderer for one persistent field. Plant progression is owned by
    /// PlantWorldPersistenceService and survives this location being unloaded.
    /// </summary>
    public sealed class FarmField : MonoBehaviour
    {
        [SerializeField] private PlantConfig plantConfig;
        [SerializeField, Min(0)] private int targetPlantCount = 8;
        [SerializeField, Min(0.1f)] private float minDistanceBetweenPlants = 0.75f;
        [SerializeField, Min(1)] private int randomPlacementAttemptsPerPlant = 30;
        [SerializeField] private List<GameObject> plantingAreas = new();
        [SerializeField] private Transform plantsRoot;
        [SerializeField, Tooltip("Optional explicit location. Leave empty to infer it from VillageLocationSelector.")]
        private string locationId;
        [SerializeField, ReadOnlyInInspector, Tooltip("Stable identity of this scene field. Generated automatically.")]
        private string persistentId;

        private readonly Dictionary<string, PlantVisual> visuals = new(StringComparer.Ordinal);
        private readonly Dictionary<ItemHolder, FruitBinding> fruitBindings = new();
        private readonly Dictionary<FruitPlantHarvestInteractable, string> harvestBindings = new();
        private PlantWorldPersistenceService plantWorld;
        private IPublisher<PlaySoundMessage> playSoundPublisher;
        private bool isSubscribed;
        private bool renderRequested;

        public string PersistentId => persistentId;

        [Inject]
        public void Construct(PlantWorldPersistenceService plantWorld, IPublisher<PlaySoundMessage> publisher)
        {
            this.plantWorld = plantWorld;
            playSoundPublisher = publisher;
            Subscribe();
            renderRequested = true;
        }

        /// <summary>Called before inactive locations are removed from the scene.</summary>
        public void RegisterPersistenceDefinition(PlantWorldPersistenceService service, VillageLocationSelector locationSelector)
        {
            if (service == null || plantConfig == null || string.IsNullOrWhiteSpace(plantConfig.Id))
            {
                return;
            }

            EnsurePersistentId();
            string resolvedLocationId = ResolveLocationId(locationSelector);
            if (string.IsNullOrWhiteSpace(persistentId) || string.IsNullOrWhiteSpace(resolvedLocationId))
            {
                Debug.LogWarning($"Farm field '{name}' needs a persistent ID and a location to participate in plant saving.", this);
                return;
            }

            service.Register(new FarmFieldGrowthDefinition(
                persistentId,
                resolvedLocationId,
                plantConfig,
                targetPlantCount,
                GetFruitSlotCount()));
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

        private void Update()
        {
            if (!renderRequested || plantWorld == null || string.IsNullOrWhiteSpace(persistentId))
            {
                return;
            }

            renderRequested = false;
            SynchronizeVisuals();
        }

        private void OnValidate()
        {
            EnsurePersistentId();
        }

        private void SynchronizeVisuals()
        {
            IReadOnlyList<YG.SavedFarmPlant> plants = plantWorld.GetFieldPlants(persistentId);
            if (plants.Count == 0)
            {
                return;
            }

            if (plants.Any(plant => !plant.hasPosition))
            {
                plantWorld.SetInitialPlantPositions(persistentId, BuildPlantPositions(plants));
                return;
            }

            var presentPlantIds = new HashSet<string>(plants.Select(plant => plant.plantId), StringComparer.Ordinal);
            foreach (string visualId in visuals.Keys.Where(id => !presentPlantIds.Contains(id)).ToArray())
            {
                RemoveVisual(visualId);
            }

            foreach (YG.SavedFarmPlant plant in plants)
            {
                SynchronizePlant(plant);
            }
        }

        private void SynchronizePlant(YG.SavedFarmPlant plant)
        {
            if (plantConfig == null || plantConfig.Stages == null || plantConfig.Stages.Count == 0)
            {
                return;
            }

            if (!visuals.TryGetValue(plant.plantId, out PlantVisual visual))
            {
                visual = new PlantVisual(plant.plantId);
                visuals.Add(plant.plantId, visual);
            }

            int stageIndex = Mathf.Clamp(plant.bodyStageIndex, 0, plantConfig.Stages.Count - 1);
            if (visual.Root == null || visual.BodyStageIndex != stageIndex)
            {
                DestroyVisualRoot(visual);
                visual.BodyStageIndex = stageIndex;
                visual.Root = CreateBodyVisual(plant, stageIndex, stageIndex == 0);
                if (plant.isHarvestable)
                {
                    RegisterCollectable(visual);
                }
            }
            else if (plant.isHarvestable && visual.Collectable == null)
            {
                RegisterCollectable(visual);
            }

            if (plantConfig is FruitPlantConfig fruitConfig &&
                stageIndex >= plantConfig.Stages.Count - 1 && visual.Root != null)
            {
                SynchronizeFruitVisuals(visual, plant, fruitConfig);
            }
            else
            {
                ClearFruitVisuals(visual);
            }
        }

        private GameObject CreateBodyVisual(YG.SavedFarmPlant plant, int stageIndex, bool riseFromGround)
        {
            Vector3 plantPosition = ToPosition(plant);
            GameObject prefab = plantConfig.Stages[stageIndex];
            if (prefab == null)
            {
                return null;
            }

            Vector3 targetPosition = plantPosition + plantConfig.TargetPosition;
            GameObject visual = Instantiate(
                prefab,
                riseFromGround ? plantPosition + plantConfig.StartPosition : targetPosition,
                Quaternion.identity,
                plantsRoot);
            if (riseFromGround)
            {
                float duration = Mathf.Max(0.01f, RandomInterval(plantConfig.GrowTime));
                visual.transform.DOMove(targetPosition, duration).SetEase(Ease.Linear);
            }
            PlayPlantGrowthSound(stageIndex >= plantConfig.Stages.Count - 1, visual.transform.position);
            return visual;
        }

        private void RegisterCollectable(PlantVisual visual)
        {
            if (visual.Root == null || visual.Collectable != null)
            {
                return;
            }

            ItemHolder itemHolder = visual.Root.GetComponentInChildren<ItemHolder>();
            if (itemHolder == null)
            {
                Debug.LogWarning($"Field '{name}' expected final stage '{visual.Root.name}' to contain an ItemHolder.", this);
                return;
            }

            // The field owns this item. It must never enter the independent dropped-item save.
            itemHolder.ConfigurePersistence(false, false);
            itemHolder.CanInteractable = true;
            itemHolder.Destroyed += OnCollectableDestroyed;
            visual.Collectable = itemHolder;
        }

        private void SynchronizeFruitVisuals(PlantVisual visual, YG.SavedFarmPlant plant, FruitPlantConfig config)
        {
            Transform fruitPlaces = FindChildRecursive(visual.Root.transform, "FruitPlaces") ?? visual.Root.transform;
            int slotCount = Mathf.Min(plant.fruits?.Length ?? 0, Mathf.Max(1, fruitPlaces.childCount));
            for (int index = 0; index < slotCount; index++)
            {
                Transform point = fruitPlaces.childCount == 0 ? fruitPlaces : fruitPlaces.GetChild(index);
                YG.SavedFarmFruit fruit = plant.fruits[index];
                visual.Fruits.TryGetValue(index, out FruitVisual fruitVisual);

                if (fruit.stageIndex < 0 || config.FruitStages == null || config.FruitStages.Count == 0)
                {
                    if (fruitVisual != null)
                    {
                        RemoveFruitVisual(visual, index);
                    }

                    continue;
                }

                int stageIndex = Mathf.Clamp(fruit.stageIndex, 0, config.FruitStages.Count - 1);
                if (fruitVisual != null && fruitVisual.StageIndex == stageIndex && fruitVisual.Root != null)
                {
                    continue;
                }

                if (fruitVisual != null)
                {
                    RemoveFruitVisual(visual, index);
                }

                GameObject prefab = config.FruitStages[stageIndex];
                if (prefab == null)
                {
                    continue;
                }

                GameObject fruitObject = Instantiate(prefab, point.position, point.rotation, point);
                fruitObject.transform.SetParent(point, true);
                visual.Fruits[index] = new FruitVisual(stageIndex, fruitObject);
                PlaySound(config.FruitSoundConfig?.SoundSettings, fruitObject.transform.position);

                if (stageIndex >= config.FruitStages.Count - 1)
                {
                    RegisterFruit(visual, plant.plantId, index, fruitObject, config);
                }
            }

            foreach (int slotIndex in visual.Fruits.Keys.Where(index => index >= slotCount).ToArray())
            {
                RemoveFruitVisual(visual, slotIndex);
            }
        }

        private void RegisterFruit(
            PlantVisual visual,
            string plantId,
            int slotIndex,
            GameObject fruitObject,
            FruitPlantConfig config)
        {
            ItemHolder fruit = fruitObject.GetComponentInChildren<ItemHolder>();
            if (fruit == null && config.HandFruit != null)
            {
                fruit = fruitObject.AddComponent<ItemHolder>();
                fruit.Initialize(config.HandFruit);
            }

            if (fruit == null)
            {
                Debug.LogWarning($"Field '{name}' expected fruit stage '{fruitObject.name}' to contain an ItemHolder.", this);
                return;
            }

            fruit.ConfigurePersistence(false, false);
            fruit.CanInteractable = false;
            fruitBindings[fruit] = new FruitBinding(plantId, slotIndex);

            if (visual.FruitHarvest == null)
            {
                visual.FruitHarvest = visual.Root.GetComponentInChildren<FruitPlantHarvestInteractable>(true);
                if (visual.FruitHarvest != null)
                {
                    visual.FruitHarvest.FruitCollected += OnFruitCollected;
                    visual.FruitHarvest.Emptied += OnFruitPlantEmptied;
                    harvestBindings[visual.FruitHarvest] = visual.PlantId;
                }
            }

            if (visual.FruitHarvest != null)
            {
                visual.FruitHarvest.RegisterFruit(fruit);
            }
            else
            {
                fruit.CanInteractable = true;
            }
        }

        private void OnCollectableDestroyed(ItemHolder itemHolder)
        {
            foreach (PlantVisual visual in visuals.Values)
            {
                if (visual.Collectable != itemHolder)
                {
                    continue;
                }

                PlayItemGivenSound(itemHolder.transform.position);
                plantWorld?.HarvestPlant(persistentId, visual.PlantId);
                return;
            }
        }

        private void OnFruitCollected(ItemHolder fruit)
        {
            if (!fruitBindings.TryGetValue(fruit, out FruitBinding binding))
            {
                return;
            }

            PlayItemGivenSound(fruit.transform.position);
            plantWorld?.HarvestFruit(persistentId, binding.PlantId, binding.SlotIndex);
        }

        private void OnFruitPlantEmptied(FruitPlantHarvestInteractable harvest)
        {
            if (harvestBindings.TryGetValue(harvest, out string plantId))
            {
                plantWorld?.ResetFruitPlant(persistentId, plantId);
            }
        }

        private void OnFieldStateChanged(string fieldId)
        {
            if (string.Equals(fieldId, persistentId, StringComparison.Ordinal))
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

            plantWorld.FieldStateChanged += OnFieldStateChanged;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || plantWorld == null)
            {
                return;
            }

            plantWorld.FieldStateChanged -= OnFieldStateChanged;
            isSubscribed = false;
        }

        private void ClearVisuals()
        {
            foreach (PlantVisual visual in visuals.Values.ToArray())
            {
                DestroyVisualRoot(visual);
            }

            visuals.Clear();
            fruitBindings.Clear();
            harvestBindings.Clear();
        }

        private void RemoveVisual(string plantId)
        {
            if (visuals.Remove(plantId, out PlantVisual visual))
            {
                DestroyVisualRoot(visual);
            }
        }

        private void DestroyVisualRoot(PlantVisual visual)
        {
            if (visual.Collectable != null)
            {
                visual.Collectable.Destroyed -= OnCollectableDestroyed;
                visual.Collectable = null;
            }

            if (visual.FruitHarvest != null)
            {
                visual.FruitHarvest.FruitCollected -= OnFruitCollected;
                visual.FruitHarvest.Emptied -= OnFruitPlantEmptied;
                harvestBindings.Remove(visual.FruitHarvest);
                visual.FruitHarvest = null;
            }

            ClearFruitVisuals(visual);
            if (visual.Root != null)
            {
                visual.Root.transform.DOKill();
                Destroy(visual.Root);
                visual.Root = null;
            }
        }

        private void ClearFruitVisuals(PlantVisual visual)
        {
            foreach (int index in visual.Fruits.Keys.ToArray())
            {
                RemoveFruitVisual(visual, index);
            }
        }

        private void RemoveFruitVisual(PlantVisual visual, int slotIndex)
        {
            if (!visual.Fruits.Remove(slotIndex, out FruitVisual fruitVisual))
            {
                return;
            }

            ItemHolder itemHolder = fruitVisual.Root != null ? fruitVisual.Root.GetComponentInChildren<ItemHolder>() : null;
            if (itemHolder != null)
            {
                fruitBindings.Remove(itemHolder);
            }

            if (fruitVisual.Root != null)
            {
                Destroy(fruitVisual.Root);
            }
        }

        private List<Vector3> BuildPlantPositions(IReadOnlyList<YG.SavedFarmPlant> plants)
        {
            int count = plants.Count;
            var candidates = new List<Vector3>();
            var positions = new Vector3[count];
            var availableAreas = new List<PlantingArea>();
            float totalArea = 0f;
            foreach (GameObject plantingArea in plantingAreas)
            {
                if (plantingArea == null || !TryGetBounds(plantingArea, out Bounds bounds))
                {
                    continue;
                }

                float area = Mathf.Max(0.01f, bounds.size.x) * Mathf.Max(0.01f, bounds.size.z);
                availableAreas.Add(new PlantingArea(bounds, area));
                totalArea += area;
            }

            if (availableAreas.Count == 0)
            {
                Debug.LogWarning($"Field '{name}' has no usable planting areas.", this);
                return Enumerable.Repeat(transform.position, count).ToList();
            }

            // Already placed plants remain fixed. New cycles choose a point around them rather
            // than rebuilding the layout of the whole field.
            for (int index = 0; index < count; index++)
            {
                if (!plants[index].hasPosition)
                {
                    continue;
                }

                positions[index] = ToPosition(plants[index]);
                candidates.Add(positions[index]);
            }

            for (int index = 0; index < count; index++)
            {
                if (plants[index].hasPosition)
                {
                    continue;
                }


                Vector3 position = default;
                bool foundPosition = false;
                int attempts = Mathf.Max(1, randomPlacementAttemptsPerPlant);
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    position = GetRandomPosition(GetRandomArea(availableAreas, totalArea).Bounds);
                    if (IsFarEnough(position, candidates))
                    {
                        foundPosition = true;
                        break;
                    }
                }

                // Do not discard a saved growth cycle when an authored field is too small.
                // Only this overflow placement relaxes the configured spacing.
                if (!foundPosition)
                {
                    position = GetRandomPosition(GetRandomArea(availableAreas, totalArea).Bounds);
                }

                positions[index] = position;
                candidates.Add(position);
            }

            return positions.ToList();
        }

        private bool IsFarEnough(Vector3 position, IReadOnlyList<Vector3> candidates)
        {
            float minSqrDistance = minDistanceBetweenPlants * minDistanceBetweenPlants;
            foreach (Vector3 candidate in candidates)
            {
                Vector3 offset = candidate - position;
                if (offset.x * offset.x + offset.z * offset.z < minSqrDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private int GetFruitSlotCount()
        {
            if (plantConfig is not FruitPlantConfig || plantConfig.Stages == null || plantConfig.Stages.Count == 0 ||
                plantConfig.Stages[^1] == null)
            {
                return 1;
            }

            Transform fruitPlaces = FindChildRecursive(plantConfig.Stages[^1].transform, "FruitPlaces");
            return Mathf.Max(1, fruitPlaces != null ? fruitPlaces.childCount : 1);
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
                string configId = plantConfig != null && !string.IsNullOrWhiteSpace(plantConfig.Id)
                    ? plantConfig.Id
                    : "field";
                persistentId = $"{configId}_field_{Guid.NewGuid():N}";
            }
        }

        private static bool TryGetBounds(GameObject plantingArea, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in plantingArea.GetComponentsInChildren<Renderer>())
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
            {
                return true;
            }

            foreach (Collider collider in plantingArea.GetComponentsInChildren<Collider>())
            {
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        private static PlantingArea GetRandomArea(IReadOnlyList<PlantingArea> areas, float totalArea)
        {
            float targetArea = UnityEngine.Random.Range(0f, totalArea);
            float currentArea = 0f;
            foreach (PlantingArea area in areas)
            {
                currentArea += area.Area;
                if (targetArea <= currentArea)
                {
                    return area;
                }
            }

            return areas[^1];
        }

        private static Vector3 GetRandomPosition(Bounds bounds) => new(
            UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
            bounds.center.y,
            UnityEngine.Random.Range(bounds.min.z, bounds.max.z));

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

        private static Vector3 ToPosition(YG.SavedFarmPlant plant) =>
            new(plant.positionX, plant.positionY, plant.positionZ);

        private static float RandomInterval(Vector2 interval)
        {
            float min = Mathf.Max(0.1f, Mathf.Min(interval.x, interval.y));
            float max = Mathf.Max(min, interval.x, interval.y);
            return UnityEngine.Random.Range(min, max);
        }

        private void PlayPlantGrowthSound(bool isFinalStage, Vector3 position)
        {
            SoundSettings settings = isFinalStage
                ? plantConfig?.PlantSoundsSettings?.GrownUpSoundSettings
                : plantConfig?.PlantSoundsSettings?.GrownStageSoundSettings;
            PlaySound(settings, position);
        }

        private void PlayItemGivenSound(Vector3 position) => PlaySound(plantConfig?.ItemGivenSound?.SoundSettings, position);

        private void PlaySound(SoundSettings settings, Vector3 position)
        {
            if (settings != null && playSoundPublisher != null)
            {
                playSoundPublisher.Publish(new PlaySoundMessage(settings, position, null));
            }
        }

        private sealed class PlantVisual
        {
            public PlantVisual(string plantId)
            {
                PlantId = plantId;
            }

            public string PlantId { get; }
            public int BodyStageIndex { get; set; } = -1;
            public GameObject Root { get; set; }
            public ItemHolder Collectable { get; set; }
            public FruitPlantHarvestInteractable FruitHarvest { get; set; }
            public Dictionary<int, FruitVisual> Fruits { get; } = new();
        }

        private sealed class FruitVisual
        {
            public FruitVisual(int stageIndex, GameObject root)
            {
                StageIndex = stageIndex;
                Root = root;
            }

            public int StageIndex { get; }
            public GameObject Root { get; }
        }

        private readonly struct FruitBinding
        {
            public FruitBinding(string plantId, int slotIndex)
            {
                PlantId = plantId;
                SlotIndex = slotIndex;
            }

            public string PlantId { get; }
            public int SlotIndex { get; }
        }

        private readonly struct PlantingArea
        {
            public PlantingArea(Bounds bounds, float area)
            {
                Bounds = bounds;
                Area = area;
            }

            public Bounds Bounds { get; }
            public float Area { get; }
        }
    }
}
