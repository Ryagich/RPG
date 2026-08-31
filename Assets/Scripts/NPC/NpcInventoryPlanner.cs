using System.Collections.Generic;
using System.Linq;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using UnityEngine;

namespace NPC
{
    public sealed class NpcInventoryPlanner
    {
        private readonly IEquipmentInventory inventory;
        private readonly ICharacterInventoryCapacity inventoryCapacity;
        private readonly NpcItemPickupConfig config;

        public NpcInventoryPlanner(
            IEquipmentInventory inventory,
            ICharacterInventoryCapacity inventoryCapacity,
            NpcItemPickupConfig config)
        {
            this.inventory = inventory;
            this.inventoryCapacity = inventoryCapacity;
            this.config = config;
        }

        public bool TryBuildPickupPlan(ItemHolder itemHolder, out NpcItemPickupPlan plan)
        {
            plan = null;
            var sourceStack = itemHolder != null && itemHolder.CanInteractable ? itemHolder.GetItemStack() : null;
            if (sourceStack?.ItemConfig == null || inventory == null)
            {
                return false;
            }

            foreach (var candidate in GetStackRotations(sourceStack))
            {
                var candidateScore = NpcItemScoreUtility.Calculate(candidate, config);
                var slotPlan = TryBuildSlotPlan(itemHolder, candidate, candidateScore);
                plan = ChooseBetterPlan(plan, slotPlan);

                var gridPlan = TryBuildGridPlan(itemHolder, candidate, candidateScore);
                plan = ChooseBetterPlan(plan, gridPlan);
            }

            return plan != null;
        }

        private NpcItemPickupPlan TryBuildSlotPlan(ItemHolder itemHolder, ItemStack candidate, float candidateScore)
        {
            var compatibleSlots = GetSlots()
                .Where(slot => slot != null && slot.ItemType == candidate.ItemConfig.ItemType && !inventory.IsSlotBlocked(slot))
                .ToList();
            if (compatibleSlots.Count == 0)
            {
                return null;
            }

            var emptySlot = compatibleSlots.FirstOrDefault(slot => slot.ItemStack == null);
            if (emptySlot != null)
            {
                var drops = BuildWeightDropPlan(candidate.TotalWeight, candidateScore, out var droppedScore);
                if (drops == null)
                {
                    return null;
                }

                return new NpcItemPickupPlan(itemHolder, candidate.Clone(), emptySlot, drops, candidateScore - droppedScore, candidateScore);
            }

            var replaceSlot = compatibleSlots
                .OrderBy(slot => NpcItemScoreUtility.Calculate(slot.ItemStack, config))
                .FirstOrDefault();
            if (replaceSlot?.ItemStack == null)
            {
                return null;
            }

            var replaceScore = NpcItemScoreUtility.Calculate(replaceSlot.ItemStack, config);
            if (candidateScore <= replaceScore)
            {
                return null;
            }

            var deltaWeight = candidate.TotalWeight - replaceSlot.ItemStack.TotalWeight;
            var dropSources = BuildWeightDropPlan(deltaWeight, candidateScore, out var droppedGridScore);
            if (dropSources == null)
            {
                return null;
            }

            var gain = candidateScore - replaceScore - droppedGridScore;
            return gain > 0f
                ? new NpcItemPickupPlan(itemHolder, candidate.Clone(), replaceSlot, dropSources, gain, candidateScore)
                : null;
        }

        private NpcItemPickupPlan TryBuildGridPlan(ItemHolder itemHolder, ItemStack candidate, float candidateScore)
        {
            var gridSources = inventory.Items
                .Where(item => item?.ItemStack?.ItemConfig != null)
                .Select(item => new NpcInventoryDropSource(item, item.ItemStack.Clone(), NpcItemScoreUtility.Calculate(item.ItemStack, config)))
                .OrderBy(source => source.Score)
                .ToList();
            var keptSources = gridSources.ToList();
            var dropSources = new List<NpcInventoryDropSource>();
            var droppedScore = 0f;

            while (!CanCarryAdditional(candidate.TotalWeight, dropSources) || !CanPackWithCandidate(keptSources, candidate))
            {
                var sourceToDrop = gridSources.FirstOrDefault(source => !dropSources.Contains(source) && source.Score < candidateScore);
                if (sourceToDrop == null)
                {
                    return null;
                }

                dropSources.Add(sourceToDrop);
                keptSources.Remove(sourceToDrop);
                droppedScore += sourceToDrop.Score;
            }

            var gain = candidateScore - droppedScore;
            return gain > 0f
                ? new NpcItemPickupPlan(itemHolder, candidate.Clone(), null, dropSources, gain, candidateScore)
                : null;
        }

        private List<NpcInventoryDropSource> BuildWeightDropPlan(float additionalWeight, float candidateScore, out float droppedScore)
        {
            droppedScore = 0f;
            if (additionalWeight <= 0f
                || inventory.MaxWeight <= 0f
                || inventoryCapacity.CurrentWeight + additionalWeight <= inventory.MaxWeight)
            {
                return new List<NpcInventoryDropSource>();
            }

            var dropSources = inventory.Items
                .Where(item => item?.ItemStack?.ItemConfig != null)
                .Select(item => new NpcInventoryDropSource(item, item.ItemStack.Clone(), NpcItemScoreUtility.Calculate(item.ItemStack, config)))
                .Where(source => source.Score < candidateScore)
                .OrderBy(source => source.Score)
                .ToList();

            var removedWeight = 0f;
            var selected = new List<NpcInventoryDropSource>();
            foreach (var source in dropSources)
            {
                selected.Add(source);
                removedWeight += source.Snapshot.TotalWeight;
                droppedScore += source.Score;

                if (inventoryCapacity.CurrentWeight + additionalWeight - removedWeight <= inventory.MaxWeight)
                {
                    return selected;
                }
            }

            return null;
        }

        private bool CanCarryAdditional(float additionalWeight, IReadOnlyCollection<NpcInventoryDropSource> plannedDrops)
        {
            var droppedWeight = plannedDrops.Sum(source => source.Snapshot?.TotalWeight ?? 0f);
            return inventory.MaxWeight <= 0f || inventoryCapacity.CurrentWeight + additionalWeight - droppedWeight <= inventory.MaxWeight;
        }

        private bool CanPackWithCandidate(IReadOnlyList<NpcInventoryDropSource> keptSources, ItemStack candidate)
        {
            var width = inventory.Tiles.tiles.GetLength(0);
            var height = inventory.Tiles.tiles.GetLength(1);
            var items = keptSources
                .Where(source => source?.Snapshot?.ItemConfig != null)
                .Select(source => source.Snapshot.Clone())
                .Append(candidate.Clone())
                .OrderByDescending(item => item.Size.x * item.Size.y)
                .ToList();
            var occupied = new bool[width, height];
            return TryPack(items, 0, occupied, width, height);
        }

        private static bool TryPack(IReadOnlyList<ItemStack> items, int index, bool[,] occupied, int width, int height)
        {
            if (index >= items.Count)
            {
                return true;
            }

            var item = items[index];
            var size = item.Size;
            for (var y = 0; y <= height - size.y; y++)
            for (var x = 0; x <= width - size.x; x++)
            {
                if (!CanPlace(occupied, x, y, size))
                {
                    continue;
                }

                SetOccupied(occupied, x, y, size, true);
                if (TryPack(items, index + 1, occupied, width, height))
                {
                    return true;
                }

                SetOccupied(occupied, x, y, size, false);
            }

            return false;
        }

        private static bool CanPlace(bool[,] occupied, int startX, int startY, Vector2Int size)
        {
            for (var y = startY; y < startY + size.y; y++)
            for (var x = startX; x < startX + size.x; x++)
            {
                if (occupied[x, y])
                {
                    return false;
                }
            }

            return true;
        }

        private static void SetOccupied(bool[,] occupied, int startX, int startY, Vector2Int size, bool value)
        {
            for (var y = startY; y < startY + size.y; y++)
            for (var x = startX; x < startX + size.x; x++)
            {
                occupied[x, y] = value;
            }
        }

        private static IEnumerable<ItemStack> GetStackRotations(ItemStack source)
        {
            if (source?.ItemConfig == null)
            {
                yield break;
            }

            yield return source.Clone();
            if (!source.CanRotate())
            {
                yield break;
            }

            var rotated = source.Clone();
            rotated.Rotate90();
            yield return rotated;
        }

        private static NpcItemPickupPlan ChooseBetterPlan(NpcItemPickupPlan current, NpcItemPickupPlan candidate)
        {
            if (candidate == null)
            {
                return current;
            }

            return current == null || candidate.Gain > current.Gain ? candidate : current;
        }

        private IEnumerable<SlotModel> GetSlots()
        {
            yield return inventory.HelmSlot;
            yield return inventory.FaceSlot;
            yield return inventory.BodySlot;
            yield return inventory.HandsSlot;
            yield return inventory.ArmsSlot;
            yield return inventory.LegsSlot;
            yield return inventory.HipsSlot;
            yield return inventory.BackpackSlot;
            yield return inventory.LeftWeaponSlot;
            yield return inventory.RightWeaponSlot;
        }
    }
}
