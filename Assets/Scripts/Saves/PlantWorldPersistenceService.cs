using System;
using System.Collections.Generic;
using System.Linq;
using Landings.Plants.PlantConfigs;
using UnityEngine;
using VContainer.Unity;
using YG;

namespace Saves
{
    /// <summary>
    /// Authoring information copied from a FarmField before its location can be unloaded.
    /// The service retains this value data, never a scene component or Transform.
    /// </summary>
    public readonly struct FarmFieldGrowthDefinition
    {
        public FarmFieldGrowthDefinition(
            string fieldId,
            string locationId,
            PlantConfig plantConfig,
            int plantCount,
            int fruitSlotCount)
        {
            FieldId = fieldId;
            LocationId = locationId;
            PlantConfig = plantConfig;
            PlantCount = Mathf.Max(0, plantCount);
            FruitSlotCount = Mathf.Max(1, fruitSlotCount);
        }

        public string FieldId { get; }
        public string LocationId { get; }
        public PlantConfig PlantConfig { get; }
        public int PlantCount { get; }
        public int FruitSlotCount { get; }
    }

    /// <summary>Authoring information required to grow a fruit tree while its scene is absent.</summary>
    public readonly struct FruitTreeGrowthDefinition
    {
        public FruitTreeGrowthDefinition(string treeId, string locationId, PlantConfig plantConfig, int fruitSlotCount)
        {
            TreeId = treeId;
            LocationId = locationId;
            PlantConfig = plantConfig;
            FruitSlotCount = Mathf.Max(0, fruitSlotCount);
        }

        public string TreeId { get; }
        public string LocationId { get; }
        public PlantConfig PlantConfig { get; }
        public int FruitSlotCount { get; }
    }

    /// <summary>
    /// Project-lifetime owner of plant and fruit-tree progression. It advances semantic state
    /// for every authored location; scene components only render the state when present.
    /// </summary>
    public sealed class PlantWorldPersistenceService : IStartable, ITickable, IDisposable
    {
        private const float TickIntervalSeconds = 1f;
        private const int MaxTransitionsPerTick = 64;

        private readonly GameSaveController saveController;
        private readonly Dictionary<string, FarmFieldGrowthDefinition> fieldDefinitions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FruitTreeGrowthDefinition> treeDefinitions = new(StringComparer.Ordinal);
        private readonly List<SavedFarmPlant> plants = new();
        private readonly List<SavedFruitTree> trees = new();
        private float tickAccumulator;
        private bool isReady;
        private bool gameplaySessionActive;
        private bool isDisposed;

        public event Action<string> FieldStateChanged;
        public event Action<string> FruitTreeStateChanged;

        public PlantWorldPersistenceService(GameSaveController saveController)
        {
            this.saveController = saveController;
        }

        public async void Start()
        {
            await saveController.Ready;
            if (isDisposed)
            {
                return;
            }

            plants.Clear();
            plants.AddRange(saveController.GetFarmPlants());
            trees.Clear();
            trees.AddRange(saveController.GetFruitTrees());
            isReady = true;
            EnsureRegisteredState();
            saveController.SaveReset += ResetState;
        }

        public void Dispose()
        {
            isDisposed = true;
            saveController.SaveReset -= ResetState;
        }

        public void BeginGameplaySession()
        {
            gameplaySessionActive = true;
        }

        public void EndGameplaySession()
        {
            gameplaySessionActive = false;
            tickAccumulator = 0f;
        }

        public void Register(FarmFieldGrowthDefinition definition)
        {
            if (!IsValid(definition))
            {
                Debug.LogWarning("A farm field was not registered for persistence because its ID, location, or PlantConfig is missing.");
                return;
            }

            fieldDefinitions[definition.FieldId] = definition;
            if (isReady)
            {
                EnsureFieldState(definition);
            }
        }

        public void Register(FruitTreeGrowthDefinition definition)
        {
            if (!IsValid(definition))
            {
                Debug.LogWarning("A fruit tree was not registered for persistence because its ID, location, or PlantConfig is missing.");
                return;
            }

            treeDefinitions[definition.TreeId] = definition;
            if (isReady)
            {
                EnsureTreeState(definition);
            }
        }

        public IReadOnlyList<SavedFarmPlant> GetFieldPlants(string fieldId)
        {
            return plants.Where(plant => string.Equals(plant.fieldId, fieldId, StringComparison.Ordinal)).ToArray();
        }

        public bool SetInitialPlantPositions(string fieldId, IReadOnlyList<Vector3> positions)
        {
            if (!isReady || !fieldDefinitions.TryGetValue(fieldId, out FarmFieldGrowthDefinition definition) ||
                positions == null)
            {
                return false;
            }

            List<int> plantIndices = FindPlantIndices(fieldId);
            if (plantIndices.Count != positions.Count)
            {
                Debug.LogWarning($"Field '{fieldId}' supplied {positions.Count} positions for {plantIndices.Count} saved plants.");
                return false;
            }

            bool changed = false;
            for (int i = 0; i < plantIndices.Count; i++)
            {
                int index = plantIndices[i];
                SavedFarmPlant plant = plants[index];
                if (plant.hasPosition)
                {
                    continue;
                }

                Vector3 position = positions[i];
                plant.hasPosition = true;
                plant.positionX = position.x;
                plant.positionY = position.y;
                plant.positionZ = position.z;
                plants[index] = plant;
                changed = true;
            }

            if (changed)
            {
                PublishState();
                FieldStateChanged?.Invoke(definition.FieldId);
            }

            return true;
        }

        public void HarvestPlant(string fieldId, string plantId)
        {
            int index = FindPlantIndex(fieldId, plantId);
            if (index < 0 || !fieldDefinitions.TryGetValue(fieldId, out FarmFieldGrowthDefinition definition))
            {
                return;
            }

            SavedFarmPlant plant = plants[index];
            ResetPlant(ref plant, definition.PlantConfig);
            plants[index] = plant;
            PublishState();
            FieldStateChanged?.Invoke(fieldId);
        }

        public void HarvestFruit(string fieldId, string plantId, int fruitSlotIndex)
        {
            int index = FindPlantIndex(fieldId, plantId);
            if (index < 0 || !fieldDefinitions.TryGetValue(fieldId, out FarmFieldGrowthDefinition definition) ||
                definition.PlantConfig is not FruitPlantConfig fruitConfig)
            {
                return;
            }

            SavedFarmPlant plant = plants[index];
            if (plant.fruits == null || fruitSlotIndex < 0 || fruitSlotIndex >= plant.fruits.Length)
            {
                return;
            }

            SavedFarmFruit fruit = plant.fruits[fruitSlotIndex];
            fruit.stageIndex = -1;
            fruit.remainingStageSeconds = RandomInterval(fruitConfig.FruitGrowTime);
            plant.fruits[fruitSlotIndex] = fruit;
            plants[index] = plant;
            PublishState();
            FieldStateChanged?.Invoke(fieldId);
        }

        public void ResetFruitPlant(string fieldId, string plantId)
        {
            HarvestPlant(fieldId, plantId);
        }

        public bool TryGetFruitTree(string treeId, out SavedFruitTree state)
        {
            int index = FindTreeIndex(treeId);
            if (index < 0)
            {
                state = default;
                return false;
            }

            state = trees[index];
            return true;
        }

        public IReadOnlyList<int> ConsumePendingFruitFalls(string treeId)
        {
            int index = FindTreeIndex(treeId);
            if (index < 0)
            {
                return Array.Empty<int>();
            }

            SavedFruitTree tree = trees[index];
            int[] pending = tree.pendingFallenSlotIndices ?? Array.Empty<int>();
            if (pending.Length == 0)
            {
                return pending;
            }

            tree.pendingFallenSlotIndices = Array.Empty<int>();
            trees[index] = tree;
            PublishState();
            return pending;
        }

        public void TryDropFruitFromTreeHit(string treeId)
        {
            int index = FindTreeIndex(treeId);
            if (index < 0 || !treeDefinitions.TryGetValue(treeId, out FruitTreeGrowthDefinition definition))
            {
                return;
            }

            SavedFruitTree tree = trees[index];
            int occupiedCount = OccupiedTreeSlotCount(tree);
            if (occupiedCount == 0)
            {
                return;
            }

            float naturalChance = definition.PlantConfig.FruitFallChancePerCheck * occupiedCount / (float)tree.slots.Length;
            float hitChance = definition.PlantConfig.FruitFallChanceOnHit * (1f + naturalChance);
            if (UnityEngine.Random.value > Mathf.Clamp01(hitChance))
            {
                return;
            }

            QueueRandomTreeFalls(ref tree, UnityEngine.Random.Range(1, occupiedCount + 1));
            trees[index] = tree;
            PublishState();
            FruitTreeStateChanged?.Invoke(treeId);
        }

        public void Tick()
        {
            if (!isReady || !gameplaySessionActive || Time.timeScale <= 0f)
            {
                return;
            }

            tickAccumulator += Time.deltaTime;
            if (tickAccumulator < TickIntervalSeconds)
            {
                return;
            }

            float elapsed = tickAccumulator;
            tickAccumulator = 0f;
            var changedFields = new HashSet<string>(StringComparer.Ordinal);
            var changedTrees = new HashSet<string>(StringComparer.Ordinal);
            bool stateChanged = AdvancePlants(elapsed, changedFields) | AdvanceTrees(elapsed, changedTrees);
            if (!stateChanged)
            {
                return;
            }

            PublishState();
            foreach (string fieldId in changedFields)
            {
                FieldStateChanged?.Invoke(fieldId);
            }

            foreach (string treeId in changedTrees)
            {
                FruitTreeStateChanged?.Invoke(treeId);
            }
        }

        private bool AdvancePlants(float elapsed, ISet<string> changedFields)
        {
            bool changed = false;
            for (int i = 0; i < plants.Count; i++)
            {
                SavedFarmPlant plant = plants[i];
                if (!fieldDefinitions.TryGetValue(plant.fieldId, out FarmFieldGrowthDefinition definition))
                {
                    continue;
                }

                bool visualStateChanged = false;
                bool plantChanged = AdvancePlant(ref plant, definition, elapsed, ref visualStateChanged);
                if (!plantChanged)
                {
                    continue;
                }

                plants[i] = plant;
                changed = true;
                if (visualStateChanged)
                {
                    changedFields.Add(plant.fieldId);
                }
            }

            return changed;
        }

        private bool AdvanceTrees(float elapsed, ISet<string> changedTrees)
        {
            bool changed = false;
            for (int i = 0; i < trees.Count; i++)
            {
                SavedFruitTree tree = trees[i];
                if (!treeDefinitions.TryGetValue(tree.treeId, out FruitTreeGrowthDefinition definition))
                {
                    continue;
                }

                bool treeChanged = AdvanceTree(ref tree, definition, elapsed);
                if (!treeChanged)
                {
                    continue;
                }

                trees[i] = tree;
                changed = true;
                changedTrees.Add(tree.treeId);
            }

            return changed;
        }

        private bool AdvancePlant(
            ref SavedFarmPlant plant,
            FarmFieldGrowthDefinition definition,
            float elapsed,
            ref bool visualStateChanged)
        {
            PlantConfig config = definition.PlantConfig;
            if (config == null || config.Stages == null || config.Stages.Count == 0 || plant.isHarvestable)
            {
                return false;
            }

            bool changed = false;
            float remainingElapsed = elapsed;
            int guard = 0;
            while (remainingElapsed > 0f && guard++ < MaxTransitionsPerTick)
            {
                if (plant.remainingStageSeconds > remainingElapsed)
                {
                    plant.remainingStageSeconds -= remainingElapsed;
                    return true;
                }

                remainingElapsed -= Mathf.Max(0f, plant.remainingStageSeconds);
                if (config is FruitPlantConfig fruitConfig && plant.bodyStageIndex >= config.Stages.Count - 1)
                {
                    changed |= AdvanceFruits(ref plant, fruitConfig, remainingElapsed, ref visualStateChanged);
                    return changed;
                }

                if (plant.bodyStageIndex < config.Stages.Count - 1)
                {
                    plant.bodyStageIndex++;
                    plant.remainingStageSeconds = plant.bodyStageIndex >= config.Stages.Count - 1
                        ? 0f
                        : RandomInterval(config.TimeBetweenStages);
                    changed = true;
                    visualStateChanged = true;
                    if (plant.bodyStageIndex >= config.Stages.Count - 1)
                    {
                        if (config is FruitPlantConfig fruitPlantConfig)
                        {
                            EnsureFruitSlots(ref plant, definition.FruitSlotCount, fruitPlantConfig);
                        }
                        else
                        {
                            plant.isHarvestable = true;
                        }
                    }

                    continue;
                }

                break;
            }

            return changed;
        }

        private static bool AdvanceFruits(
            ref SavedFarmPlant plant,
            FruitPlantConfig config,
            float elapsed,
            ref bool visualStateChanged)
        {
            if (plant.fruits == null || plant.fruits.Length == 0 || config.FruitStages == null || config.FruitStages.Count == 0)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < plant.fruits.Length; i++)
            {
                SavedFarmFruit fruit = plant.fruits[i];
                if (fruit.stageIndex >= config.FruitStages.Count - 1)
                {
                    continue;
                }

                float remainingElapsed = elapsed;
                int guard = 0;
                while (remainingElapsed > 0f && guard++ < MaxTransitionsPerTick)
                {
                    if (fruit.remainingStageSeconds > remainingElapsed)
                    {
                        fruit.remainingStageSeconds -= remainingElapsed;
                        changed = true;
                        break;
                    }

                    remainingElapsed -= Mathf.Max(0f, fruit.remainingStageSeconds);
                    if (fruit.stageIndex < 0)
                    {
                        if (UnityEngine.Random.value <= config.FruitGrowChance)
                        {
                            fruit.stageIndex = 0;
                            visualStateChanged = true;
                        }

                        fruit.remainingStageSeconds = RandomInterval(config.FruitGrowTime);
                        changed = true;
                        continue;
                    }

                    fruit.stageIndex++;
                    fruit.remainingStageSeconds = fruit.stageIndex >= config.FruitStages.Count - 1
                        ? 0f
                        : RandomInterval(config.FruitGrowTime);
                    changed = true;
                    visualStateChanged = true;
                    if (fruit.stageIndex >= config.FruitStages.Count - 1)
                    {
                        break;
                    }
                }

                plant.fruits[i] = fruit;
            }

            return changed;
        }

        private static bool AdvanceTree(ref SavedFruitTree tree, FruitTreeGrowthDefinition definition, float elapsed)
        {
            if (tree.slots == null || tree.slots.Length == 0)
            {
                return false;
            }

            bool changed = false;
            int occupiedCount = OccupiedTreeSlotCount(tree);
            if (occupiedCount < tree.slots.Length)
            {
                tree.remainingGrowSeconds -= elapsed;
                if (tree.remainingGrowSeconds <= 0f)
                {
                    List<int> freeSlots = FindFreeTreeSlots(tree);
                    if (freeSlots.Count > 0)
                    {
                        tree.slots[freeSlots[UnityEngine.Random.Range(0, freeSlots.Count)]].isOccupied = true;
                        occupiedCount++;
                        changed = true;
                    }

                    tree.remainingGrowSeconds = RandomTreeGrowInterval(definition.PlantConfig, occupiedCount, tree.slots.Length);
                }
            }

            tree.remainingFallSeconds -= elapsed;
            if (tree.remainingFallSeconds <= 0f)
            {
                tree.remainingFallSeconds = RandomInterval(definition.PlantConfig.FruitFallCheckInterval);
                occupiedCount = OccupiedTreeSlotCount(tree);
                if (occupiedCount > 0)
                {
                    float chance = definition.PlantConfig.FruitFallChancePerCheck * occupiedCount / tree.slots.Length;
                    if (UnityEngine.Random.value <= Mathf.Clamp01(chance))
                    {
                        QueueRandomTreeFalls(ref tree, UnityEngine.Random.Range(1, occupiedCount + 1));
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private void EnsureRegisteredState()
        {
            foreach (FarmFieldGrowthDefinition definition in fieldDefinitions.Values)
            {
                EnsureFieldState(definition);
            }

            foreach (FruitTreeGrowthDefinition definition in treeDefinitions.Values)
            {
                EnsureTreeState(definition);
            }
        }

        private void EnsureFieldState(FarmFieldGrowthDefinition definition)
        {
            int count = plants.Count(plant => string.Equals(plant.fieldId, definition.FieldId, StringComparison.Ordinal));
            bool changed = false;
            for (int index = count; index < definition.PlantCount; index++)
            {
                plants.Add(CreatePlant(definition, index));
                changed = true;
            }

            if (changed)
            {
                PublishState();
                FieldStateChanged?.Invoke(definition.FieldId);
            }
        }

        private void EnsureTreeState(FruitTreeGrowthDefinition definition)
        {
            int index = FindTreeIndex(definition.TreeId);
            if (index < 0)
            {
                trees.Add(CreateTree(definition));
                PublishState();
                FruitTreeStateChanged?.Invoke(definition.TreeId);
                return;
            }

            SavedFruitTree tree = trees[index];
            if (tree.slots != null && tree.slots.Length == definition.FruitSlotCount)
            {
                return;
            }

            SavedFruitTreeSlot[] previousSlots = tree.slots ?? Array.Empty<SavedFruitTreeSlot>();
            tree.slots = new SavedFruitTreeSlot[definition.FruitSlotCount];
            Array.Copy(previousSlots, tree.slots, Mathf.Min(previousSlots.Length, tree.slots.Length));
            tree.pendingFallenSlotIndices = (tree.pendingFallenSlotIndices ?? Array.Empty<int>())
                .Where(slot => slot >= 0 && slot < tree.slots.Length)
                .ToArray();
            trees[index] = tree;
            PublishState();
            FruitTreeStateChanged?.Invoke(definition.TreeId);
        }

        private static SavedFarmPlant CreatePlant(FarmFieldGrowthDefinition definition, int index)
        {
            return new SavedFarmPlant
            {
                plantId = $"{definition.FieldId}_plant_{index}",
                fieldId = definition.FieldId,
                locationId = definition.LocationId,
                plantConfigId = definition.PlantConfig.Id,
                hasPosition = false,
                bodyStageIndex = 0,
                remainingStageSeconds = RandomInterval(definition.PlantConfig.GrowTime),
                isHarvestable = false,
                fruits = Array.Empty<SavedFarmFruit>()
            };
        }

        private static SavedFruitTree CreateTree(FruitTreeGrowthDefinition definition)
        {
            return new SavedFruitTree
            {
                treeId = definition.TreeId,
                locationId = definition.LocationId,
                plantConfigId = definition.PlantConfig.Id,
                remainingGrowSeconds = RandomTreeGrowInterval(definition.PlantConfig, 0, definition.FruitSlotCount),
                remainingFallSeconds = RandomInterval(definition.PlantConfig.FruitFallCheckInterval),
                slots = new SavedFruitTreeSlot[definition.FruitSlotCount],
                pendingFallenSlotIndices = Array.Empty<int>()
            };
        }

        private static void ResetPlant(ref SavedFarmPlant plant, PlantConfig config)
        {
            // A harvested plant is a new growth cycle, so its first visual placement is chosen
            // again by the field when that location is next rendered.
            plant.hasPosition = false;
            plant.positionX = 0f;
            plant.positionY = 0f;
            plant.positionZ = 0f;
            plant.bodyStageIndex = 0;
            plant.remainingStageSeconds = RandomInterval(config.GrowTime);
            plant.isHarvestable = false;
            plant.fruits = Array.Empty<SavedFarmFruit>();
        }

        private static void EnsureFruitSlots(ref SavedFarmPlant plant, int slotCount, FruitPlantConfig config)
        {
            if (plant.fruits != null && plant.fruits.Length > 0)
            {
                return;
            }

            plant.fruits = new SavedFarmFruit[Mathf.Max(1, slotCount)];
            for (int i = 0; i < plant.fruits.Length; i++)
            {
                plant.fruits[i] = new SavedFarmFruit
                {
                    stageIndex = -1,
                    remainingStageSeconds = RandomInterval(config.FruitGrowTime)
                };
            }
        }

        private static void QueueRandomTreeFalls(ref SavedFruitTree tree, int requestedCount)
        {
            List<int> occupiedSlots = new();
            for (int i = 0; i < tree.slots.Length; i++)
            {
                if (tree.slots[i].isOccupied)
                {
                    occupiedSlots.Add(i);
                }
            }

            int count = Mathf.Min(requestedCount, occupiedSlots.Count);
            var pending = new List<int>(tree.pendingFallenSlotIndices ?? Array.Empty<int>());
            for (int i = 0; i < count; i++)
            {
                int pick = UnityEngine.Random.Range(0, occupiedSlots.Count);
                int slotIndex = occupiedSlots[pick];
                occupiedSlots.RemoveAt(pick);
                tree.slots[slotIndex].isOccupied = false;
                pending.Add(slotIndex);
            }

            tree.pendingFallenSlotIndices = pending.Distinct().ToArray();
        }

        private int FindPlantIndex(string fieldId, string plantId)
        {
            return plants.FindIndex(plant => string.Equals(plant.fieldId, fieldId, StringComparison.Ordinal) &&
                                             string.Equals(plant.plantId, plantId, StringComparison.Ordinal));
        }

        private List<int> FindPlantIndices(string fieldId)
        {
            var result = new List<int>();
            for (int i = 0; i < plants.Count; i++)
            {
                if (string.Equals(plants[i].fieldId, fieldId, StringComparison.Ordinal))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private int FindTreeIndex(string treeId) =>
            trees.FindIndex(tree => string.Equals(tree.treeId, treeId, StringComparison.Ordinal));

        private void ResetState()
        {
            plants.Clear();
            trees.Clear();
            if (isReady)
            {
                EnsureRegisteredState();
            }
        }

        private void PublishState()
        {
            if (isReady)
            {
                saveController.SetPlantWorldState(plants, trees);
            }
        }

        private static List<int> FindFreeTreeSlots(SavedFruitTree tree)
        {
            var freeSlots = new List<int>();
            for (int i = 0; i < tree.slots.Length; i++)
            {
                if (!tree.slots[i].isOccupied)
                {
                    freeSlots.Add(i);
                }
            }

            return freeSlots;
        }

        private static int OccupiedTreeSlotCount(SavedFruitTree tree)
        {
            int count = 0;
            foreach (SavedFruitTreeSlot slot in tree.slots ?? Array.Empty<SavedFruitTreeSlot>())
            {
                if (slot.isOccupied)
                {
                    count++;
                }
            }

            return count;
        }

        private static float RandomTreeGrowInterval(PlantConfig config, int occupiedCount, int slotCount)
        {
            if (config == null || slotCount <= 0)
            {
                return TickIntervalSeconds;
            }

            float occupiedFraction = Mathf.Clamp01(occupiedCount / (float)slotCount);
            float interval = Mathf.Lerp(config.TreeFruitGrowInterval.x, config.TreeFruitGrowInterval.y, occupiedFraction);
            return Mathf.Max(0.1f, interval);
        }

        private static float RandomInterval(Vector2 interval)
        {
            float min = Mathf.Max(0.1f, Mathf.Min(interval.x, interval.y));
            float max = Mathf.Max(min, interval.x, interval.y);
            return UnityEngine.Random.Range(min, max);
        }

        private static bool IsValid(FarmFieldGrowthDefinition definition) =>
            !string.IsNullOrWhiteSpace(definition.FieldId) && !string.IsNullOrWhiteSpace(definition.LocationId) &&
            definition.PlantConfig != null && !string.IsNullOrWhiteSpace(definition.PlantConfig.Id);

        private static bool IsValid(FruitTreeGrowthDefinition definition) =>
            !string.IsNullOrWhiteSpace(definition.TreeId) && !string.IsNullOrWhiteSpace(definition.LocationId) &&
            definition.PlantConfig != null && !string.IsNullOrWhiteSpace(definition.PlantConfig.Id);
    }
}
