using System.Collections.Generic;
using Inventory.Item;
using Landings.Plants.PlantConfigs;
using MessagePipe;
using Messages;
using Sounds;
using UnityEngine;

namespace Landings.Plants
{
    /// <summary>
    /// Grows one visible fruit at a time in the child points of an apple tree.
    /// The final stage of <see cref="PlantConfig"/> is used as the fruit visual;
    /// the item's HandPrefab is only spawned when a fruit falls.
    /// </summary>
    public sealed class AppleTreeFruitGrower : MonoBehaviour
    {
        [SerializeField] private PlantConfig applePlantConfig;
        [SerializeField] private ItemConfig appleItemConfig;
        [SerializeField] private Transform applePlaces;
        [SerializeField] private SoundConfig breakStickSoundConfig;
        [SerializeField] private Transform breakSoundPlace;

        private readonly List<AppleSlot> slots = new();
        private float growTimer;
        private float fallTimer;
        private float nextFallCheck;
        private GameObject appleVisualPrefab;

        private void Awake()
        {
            if (applePlaces == null)
            {
                applePlaces = FindChildRecursive(transform, "ApplePlaces");
            }

            if (applePlantConfig == null || applePlantConfig.Stages == null || applePlantConfig.Stages.Count == 0)
            {
                Debug.LogError($"{name} cannot grow apples: Apple PlantConfig has no stages.", this);
                enabled = false;
                return;
            }

            if (applePlantConfig.Type != PlantType.FruitTree)
            {
                Debug.LogError($"{name} cannot grow apples: Apple PlantConfig must have type FruitTree.", this);
                enabled = false;
                return;
            }

            appleVisualPrefab = applePlantConfig.Stages[^1];
            if (applePlaces == null || appleVisualPrefab == null)
            {
                Debug.LogError($"{name} cannot grow apples: ApplePlaces or the final apple stage is missing.", this);
                enabled = false;
                return;
            }

            foreach (Transform point in applePlaces)
            {
                slots.Add(new AppleSlot(point));
            }

            if (slots.Count == 0)
            {
                Debug.LogWarning($"{name} has no child points in ApplePlaces.", this);
                enabled = false;
                return;
            }

            nextFallCheck = RandomInterval(applePlantConfig.FruitFallCheckInterval);
        }

        private void Update()
        {
            if (HasFreeSlot())
            {
                growTimer += Time.deltaTime;
                if (growTimer >= GetGrowInterval())
                {
                    growTimer = 0f;
                    GrowOneApple();
                }
            }

            fallTimer += Time.deltaTime;
            if (fallTimer >= nextFallCheck)
            {
                fallTimer = 0f;
                nextFallCheck = RandomInterval(applePlantConfig.FruitFallCheckInterval);
                TryDropApple(applePlantConfig.FruitFallChancePerCheck);
            }
        }

        private float GetGrowInterval()
        {
            var occupiedFraction = OccupiedCount / (float)slots.Count;
            return Mathf.Lerp(
                applePlantConfig.TreeFruitGrowInterval.x,
                applePlantConfig.TreeFruitGrowInterval.y,
                occupiedFraction);
        }

        private void GrowOneApple()
        {
            var freeSlots = new List<AppleSlot>();
            foreach (var slot in slots)
            {
                if (slot.Visual == null)
                {
                    freeSlots.Add(slot);
                }
            }

            if (freeSlots.Count == 0)
            {
                return;
            }

            var slotToGrow = freeSlots[Random.Range(0, freeSlots.Count)];
            slotToGrow.Visual = Instantiate(appleVisualPrefab, slotToGrow.Point);
            slotToGrow.Visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            PlaySound(
                applePlantConfig.PlantSoundsSettings?.GrownUpSoundSettings,
                slotToGrow.Visual.transform.position);
        }

        public void TryDropAppleFromHit()
        {
            var naturalFallChance = applePlantConfig.FruitFallChancePerCheck
                                    * OccupiedCount / (float)slots.Count;
            var hitFallChance = applePlantConfig.FruitFallChanceOnHit * (1f + naturalFallChance);
            TryDropAppleWithChance(hitFallChance);
        }

        private void TryDropApple(float fallChanceAtFullTree)
        {
            var occupiedFraction = OccupiedCount / (float)slots.Count;
            TryDropAppleWithChance(fallChanceAtFullTree * occupiedFraction);
        }

        private void TryDropAppleWithChance(float fallChance)
        {
            var occupiedCount = OccupiedCount;
            if (occupiedCount == 0)
            {
                return;
            }

            if (Random.value > Mathf.Clamp01(fallChance))
            {
                return;
            }

            if (appleItemConfig == null || appleItemConfig.HandPrefab == null)
            {
                Debug.LogWarning($"{name} cannot drop an apple: Apple ItemConfig.HandPrefab is missing.", this);
                return;
            }

            var occupiedSlots = new List<AppleSlot>();
            foreach (var slot in slots)
            {
                if (slot.Visual != null)
                {
                    occupiedSlots.Add(slot);
                }
            }

            var applesToDrop = Random.Range(1, occupiedSlots.Count + 1);
            PlayBreakSound();
            for (var i = 0; i < applesToDrop; i++)
            {
                var index = Random.Range(0, occupiedSlots.Count);
                var slotToDrop = occupiedSlots[index];
                occupiedSlots.RemoveAt(index);

                Destroy(slotToDrop.Visual);
                slotToDrop.Visual = null;
                Instantiate(appleItemConfig.HandPrefab, slotToDrop.Point.position, Random.rotation);
            }
        }

        private bool HasFreeSlot()
        {
            return OccupiedCount < slots.Count;
        }

        private int OccupiedCount
        {
            get
            {
                var count = 0;
                foreach (var slot in slots)
                {
                    if (slot.Visual != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private static float RandomInterval(Vector2 interval)
        {
            return Random.Range(Mathf.Max(0.1f, interval.x), Mathf.Max(0.1f, interval.y));
        }

        private static void PlaySound(SoundSettings settings, Vector3 position)
        {
            if (settings == null)
            {
                return;
            }

            GlobalMessagePipe.GetPublisher<PlaySoundMessage>()
                             .Publish(new PlaySoundMessage(settings, position, null));
        }

        private void PlayBreakSound()
        {
            if (breakStickSoundConfig == null || breakSoundPlace == null)
            {
                return;
            }

            PlaySound(breakStickSoundConfig.SoundSettings, breakSoundPlace.position);
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

        private sealed class AppleSlot
        {
            public readonly Transform Point;
            public GameObject Visual;

            public AppleSlot(Transform point)
            {
                Point = point;
            }
        }
    }
}
