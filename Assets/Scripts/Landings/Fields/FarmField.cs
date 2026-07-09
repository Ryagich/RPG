using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Inventory.Item;
using Landings.Plants;
using Landings.Plants.PlantConfigs;
using UnityEngine;

namespace Landings.Fields
{
    public sealed class FarmField : MonoBehaviour
    {
        [SerializeField] private PlantConfig plantConfig;
        [SerializeField, Min(0)] private int targetPlantCount = 8;
        [SerializeField, Min(0.1f)] private float minDistanceBetweenPlants = 0.75f;
        [SerializeField, Min(0f)] private float sideScatter = 0.25f;
        [SerializeField, Min(1)] private int randomPlacementAttemptsPerPlant = 30;
        [SerializeField] private List<FieldFurrow> furrows = new();
        [SerializeField] private Transform plantsRoot;
        [SerializeField] private bool growOnStart = true;
        [SerializeField] private Vector2 initialGrowDelay = new(0f, 0.5f);

        private readonly List<PlantSlot> slots = new();

        private void Awake()
        {
            if (plantsRoot == null)
            {
                plantsRoot = transform;
            }

            RebuildSlots();
        }

        private void Start()
        {
            if (!growOnStart)
            {
                return;
            }

            foreach (var slot in slots)
            {
                StartGrowth(slot, Random.Range(initialGrowDelay.x, initialGrowDelay.y));
            }
        }

        private void OnDisable()
        {
            foreach (var slot in slots)
            {
                UnsubscribeCollectable(slot);
                UnsubscribeFruitHarvest(slot);
            }
        }

        private void OnValidate()
        {
            if (initialGrowDelay.y < initialGrowDelay.x)
            {
                initialGrowDelay.y = initialGrowDelay.x;
            }
        }

        private void RebuildSlots()
        {
            slots.Clear();
            if (targetPlantCount <= 0)
            {
                return;
            }

            var candidates = BuildCandidates();

            foreach (var candidate in candidates)
            {
                slots.Add(new PlantSlot(candidate.Position));
            }

            if (slots.Count < targetPlantCount)
            {
                Debug.LogWarning($"{name} can fit only {slots.Count} plants without overlap. Requested: {targetPlantCount}.", this);
            }
        }

        private List<PlantCandidate> BuildCandidates()
        {
            var candidates = new List<PlantCandidate>();
            var availableFurrows = new List<FieldFurrow>();
            var totalLength = 0f;
            foreach (var furrow in furrows)
            {
                if (furrow == null || furrow.Length <= 0.01f)
                {
                    continue;
                }

                availableFurrows.Add(furrow);
                totalLength += furrow.Length;
            }

            if (availableFurrows.Count == 0)
            {
                return candidates;
            }

            var maxAttempts = Mathf.Max(targetPlantCount, targetPlantCount * randomPlacementAttemptsPerPlant);
            for (var attempt = 0; attempt < maxAttempts && candidates.Count < targetPlantCount; attempt++)
            {
                var furrow = GetRandomFurrow(availableFurrows, totalLength);
                var sideOffset = Random.Range(-sideScatter, sideScatter);
                var position = furrow.GetPoint(Random.value, sideOffset);

                if (IsFarEnough(position, candidates))
                {
                    candidates.Add(new PlantCandidate(position));
                }
            }

            return candidates;
        }

        private bool IsFarEnough(Vector3 position, IReadOnlyList<PlantCandidate> candidates)
        {
            var minSqrDistance = minDistanceBetweenPlants * minDistanceBetweenPlants;
            foreach (var candidate in candidates)
            {
                if ((candidate.Position - position).sqrMagnitude < minSqrDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private static FieldFurrow GetRandomFurrow(IReadOnlyList<FieldFurrow> availableFurrows, float totalLength)
        {
            var targetLength = Random.Range(0f, totalLength);
            var currentLength = 0f;
            foreach (var furrow in availableFurrows)
            {
                currentLength += furrow.Length;
                if (targetLength <= currentLength)
                {
                    return furrow;
                }
            }

            return availableFurrows[^1];
        }

        private void StartGrowth(PlantSlot slot, float delay = 0f)
        {
            if (plantConfig == null || plantConfig.Stages == null || plantConfig.Stages.Count == 0)
            {
                Debug.LogWarning($"{name} cannot grow plants: plant config or stages are not assigned.", this);
                return;
            }

            if (slot.Routine != null)
            {
                StopCoroutine(slot.Routine);
            }

            UnsubscribeCollectable(slot);
            UnsubscribeFruitHarvest(slot);
            slot.Routine = StartCoroutine(Grow(slot, delay));
        }

        private IEnumerator Grow(PlantSlot slot, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            var fruitPlantConfig = plantConfig as FruitPlantConfig;
            if (fruitPlantConfig != null && fruitPlantConfig.FruitStages.Count > 0)
            {
                if (slot.PlantVisual == null)
                {
                    yield return GrowPlantBody(slot, false);
                    PrepareFruitHarvest(slot);
                }

                yield return GrowFruit(slot, fruitPlantConfig);
            }
            else
            {
                yield return GrowPlantBody(slot, true);
            }

            slot.Routine = null;
        }

        private IEnumerator GrowPlantBody(PlantSlot slot, bool finalStageIsCollectable)
        {
            yield return GrowFirstStageByUpper(slot);
            if (plantConfig.Stages.Count <= 1)
            {
                if (finalStageIsCollectable && slot.PlantVisual != null)
                {
                    RegisterCollectable(slot, slot.PlantVisual);
                }

                yield break;
            }

            yield return GrowStages(slot, plantConfig.Stages, plantConfig.TimeBetweenStages, finalStageIsCollectable, PlantVisualTarget.Plant, 1);
        }

        private IEnumerator GrowFirstStageByUpper(PlantSlot slot)
        {
            var firstStage = ReplaceVisual(
                slot,
                plantConfig.Stages[0],
                PlantVisualTarget.Plant,
                slot.Position + plantConfig.StartPosition);

            var growTime = Random.Range(plantConfig.GrowTime.x, plantConfig.GrowTime.y);
            if (growTime <= 0f)
            {
                firstStage.transform.position = slot.Position + plantConfig.TargetPosition;
                yield break;
            }

            var tween = firstStage.transform
                .DOMove(slot.Position + plantConfig.TargetPosition, growTime)
                .SetEase(Ease.Linear);

            yield return tween.WaitForCompletion();
        }

        private IEnumerator GrowFruit(PlantSlot slot, FruitPlantConfig fruitPlantConfig)
        {
            while (fruitPlantConfig.FruitGrowChance < Random.Range(0f, 1f))
            {
                yield return new WaitForSeconds(Random.Range(fruitPlantConfig.FruitGrowTime.x, fruitPlantConfig.FruitGrowTime.y));
            }

            PrepareFruitSlots(slot);
            if (slot.FruitSlots.Count == 0)
            {
                slot.FruitSlots.Add(new FruitSlot(slot.PlantVisual != null ? slot.PlantVisual.transform : plantsRoot));
            }

            var growingFruits = new List<FruitSlot>();
            foreach (var fruitSlot in slot.FruitSlots)
            {
                if (fruitSlot.Visual != null)
                {
                    continue;
                }

                if (fruitPlantConfig.FruitGrowChance >= Random.Range(0f, 1f))
                {
                    growingFruits.Add(fruitSlot);
                }
            }

            if (growingFruits.Count == 0)
            {
                yield return new WaitForSeconds(Random.Range(fruitPlantConfig.FruitGrowTime.x, fruitPlantConfig.FruitGrowTime.y));
                yield return GrowFruit(slot, fruitPlantConfig);
                yield break;
            }

            for (var i = 0; i < fruitPlantConfig.FruitStages.Count; i++)
            {
                foreach (var fruitSlot in growingFruits)
                {
                    var visual = ReplaceFruitVisual(fruitSlot, fruitPlantConfig.FruitStages[i]);
                    if (i == fruitPlantConfig.FruitStages.Count - 1)
                    {
                        RegisterFruit(slot, visual, fruitPlantConfig);
                    }
                }

                if (i < fruitPlantConfig.FruitStages.Count - 1)
                {
                    yield return new WaitForSeconds(Random.Range(fruitPlantConfig.FruitGrowTime.x, fruitPlantConfig.FruitGrowTime.y));
                }
            }
        }

        private IEnumerator GrowStages(
            PlantSlot slot,
            IReadOnlyList<GameObject> stages,
            Vector2 timeBetweenStages,
            bool finalStageIsCollectable,
            PlantVisualTarget target,
            int startIndex = 0)
        {
            for (var i = startIndex; i < stages.Count; i++)
            {
                var visual = ReplaceVisual(slot, stages[i], target, slot.Position + plantConfig.TargetPosition);

                if (finalStageIsCollectable && i == stages.Count - 1)
                {
                    RegisterCollectable(slot, visual);
                    yield break;
                }

                if (i < stages.Count - 1)
                {
                    var wait = Random.Range(timeBetweenStages.x, timeBetweenStages.y);
                    yield return new WaitForSeconds(wait);
                }
            }
        }

        private GameObject ReplaceVisual(PlantSlot slot, GameObject prefab, PlantVisualTarget target, Vector3 position)
        {
            if (target == PlantVisualTarget.Plant && slot.PlantVisual != null)
            {
                Destroy(slot.PlantVisual);
            }
            else if (target == PlantVisualTarget.Fruit && slot.FruitVisual != null)
            {
                Destroy(slot.FruitVisual);
            }

            var visual = Instantiate(prefab, position, Quaternion.identity, plantsRoot);
            var targetScale = visual.transform.localScale;
            visual.transform.localScale = targetScale * 0.5f;
            visual.transform.DOScale(targetScale, 0.5f).SetEase(Ease.OutElastic, 0.2f);

            if (target == PlantVisualTarget.Plant)
            {
                slot.PlantVisual = visual;
            }
            else
            {
                slot.FruitVisual = visual;
            }

            return visual;
        }

        private GameObject ReplaceFruitVisual(FruitSlot fruitSlot, GameObject prefab)
        {
            if (fruitSlot.Visual != null)
            {
                Destroy(fruitSlot.Visual);
            }

            var visual = Instantiate(prefab, fruitSlot.Point.position, fruitSlot.Point.rotation, plantsRoot);
            visual.transform.SetParent(fruitSlot.Point, true);
            var targetScale = visual.transform.localScale;
            visual.transform.localScale = targetScale * 0.5f;
            visual.transform.DOScale(targetScale, 0.5f).SetEase(Ease.OutElastic, 0.2f);
            fruitSlot.Visual = visual;
            return visual;
        }

        private void RegisterCollectable(PlantSlot slot, GameObject visual)
        {
            var collectable = visual.GetComponentInChildren<ItemHolder>();
            if (collectable == null)
            {
                Debug.LogWarning($"{name} expected final growth stage '{visual.name}' to have an ItemHolder.", this);
                return;
            }

            slot.Collectable = collectable;
            collectable.CanInteractable = true;
            collectable.Destroyed += OnCollectableDestroyed;
        }

        private void RegisterFruit(PlantSlot slot, GameObject visual, FruitPlantConfig fruitPlantConfig)
        {
            var fruit = visual.GetComponentInChildren<ItemHolder>();
            if (fruit == null && fruitPlantConfig.HandFruit != null)
            {
                fruit = visual.AddComponent<ItemHolder>();
                fruit.Initialize(fruitPlantConfig.HandFruit);
            }

            if (fruit == null)
            {
                Debug.LogWarning($"{name} expected final fruit stage '{visual.name}' to have an ItemHolder.", this);
                return;
            }

            if (slot.FruitHarvest == null)
            {
                Debug.LogWarning($"{name} expected fruit plant '{slot.PlantVisual?.name}' to have FruitPlantHarvestInteractable.", this);
                fruit.CanInteractable = true;
                return;
            }

            slot.FruitHarvest.RegisterFruit(fruit);
        }

        private void PrepareFruitHarvest(PlantSlot slot)
        {
            UnsubscribeFruitHarvest(slot);
            if (slot.PlantVisual == null)
            {
                return;
            }

            slot.FruitHarvest = slot.PlantVisual.GetComponentInChildren<FruitPlantHarvestInteractable>(true);
            if (slot.FruitHarvest != null)
            {
                slot.FruitHarvest.Emptied += OnFruitPlantEmptied;
            }
        }

        private void PrepareFruitSlots(PlantSlot slot)
        {
            if (slot.PlantVisual == null || slot.FruitSlots.Count > 0)
            {
                return;
            }

            var fruitPlaces = FindChildRecursive(slot.PlantVisual.transform, "FruitPlaces");
            if (fruitPlaces == null)
            {
                return;
            }

            foreach (Transform fruitPlace in fruitPlaces)
            {
                slot.FruitSlots.Add(new FruitSlot(fruitPlace));
            }
        }

        private void OnFruitPlantEmptied(FruitPlantHarvestInteractable fruitHarvest)
        {
            foreach (var slot in slots)
            {
                if (slot.FruitHarvest != fruitHarvest)
                {
                    continue;
                }

                UnsubscribeFruitHarvest(slot);
                slot.PlantVisual = null;
                slot.FruitSlots.Clear();
                StartGrowth(slot);
                return;
            }
        }

        private void UnsubscribeFruitHarvest(PlantSlot slot)
        {
            if (slot.FruitHarvest == null)
            {
                return;
            }

            slot.FruitHarvest.Emptied -= OnFruitPlantEmptied;
            slot.FruitHarvest = null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            foreach (Transform child in root)
            {
                if (child.name == childName)
                {
                    return child;
                }

                var nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void OnCollectableDestroyed(ItemHolder itemHolder)
        {
            foreach (var slot in slots)
            {
                if (slot.Collectable != itemHolder)
                {
                    continue;
                }

                UnsubscribeCollectable(slot);
                if (slot.FruitVisual != null && itemHolder.transform.IsChildOf(slot.FruitVisual.transform))
                {
                    slot.FruitVisual = null;
                }
                else if (slot.PlantVisual != null && itemHolder.transform.IsChildOf(slot.PlantVisual.transform))
                {
                    slot.PlantVisual = null;
                }

                StartGrowth(slot);
                return;
            }
        }

        private void UnsubscribeCollectable(PlantSlot slot)
        {
            if (slot.Collectable == null)
            {
                return;
            }

            slot.Collectable.Destroyed -= OnCollectableDestroyed;
            slot.Collectable = null;
        }

        private readonly struct PlantCandidate
        {
            public readonly Vector3 Position;

            public PlantCandidate(Vector3 position)
            {
                Position = position;
            }
        }

        private enum PlantVisualTarget
        {
            Plant,
            Fruit
        }

        private sealed class PlantSlot
        {
            public readonly Vector3 Position;
            public Coroutine Routine;
            public GameObject PlantVisual;
            public GameObject FruitVisual;
            public ItemHolder Collectable;
            public FruitPlantHarvestInteractable FruitHarvest;
            public readonly List<FruitSlot> FruitSlots = new();

            public PlantSlot(Vector3 position)
            {
                Position = position;
            }
        }

        private sealed class FruitSlot
        {
            public readonly Transform Point;
            public GameObject Visual;

            public FruitSlot(Transform point)
            {
                Point = point;
            }
        }
    }
}
