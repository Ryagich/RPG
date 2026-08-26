using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Grid;
using Inventory.Item;
using Inventory.Slot;
using UniRx;
using UnityEngine;

namespace Inventory.Inventories
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerInventory : ITiledInventory
    {
        private readonly InventoryConfig inventoryConfig;
        private readonly Subject<Unit> changedSubject = new();
        private readonly List<ItemStack> pendingOverflowItems = new();

        public ReactiveCollection<ItemInInventory> Items { get; } = new();
        public IObservable<Unit> Changed => changedSubject;
        public ReactiveProperty<SlotModel> HandSlot { get; } = new(new SlotModel(ItemType.None, SlotStackLimitType.SingleItem, null));
        public ReactiveProperty<IInventory> HandSourceInventory { get; } = new(null);
        public IReadOnlyReactiveProperty<float> CurrentWeightReactive { get; }
        public Tiles Tiles { get; private set; }
        public float MaxWeight { get; set; }
        public float CurrentWeight => GetItemsWeight()
                                      + GetSlotWeight(HelmSlot)
                                      + GetSlotWeight(FaceSlot)
                                      + GetSlotWeight(BodySlot)
                                      + GetSlotWeight(HandsSlot)
                                      + GetSlotWeight(ArmsSlot)
                                      + GetSlotWeight(LegsSlot)
                                      + GetSlotWeight(HipsSlot)
                                      + GetSlotWeight(BackpackSlot)
                                      + GetSlotWeight(LeftWeaponSlot)
                                      + GetSlotWeight(RightWeaponSlot)
                                      + GetSlotWeight(HandSlot.Value);
        public float CurrentWeightPercent => MaxWeight > 0f ? CurrentWeight / MaxWeight : 0f;
        public bool IsFaceSlotBlocked => HelmSlot.ItemConfig != null && HelmSlot.ItemConfig.BlocksFaceSlot;

        public SlotModel HelmSlot = new(ItemType.Helm, SlotStackLimitType.SingleItem);
        public SlotModel FaceSlot = new(ItemType.Face, SlotStackLimitType.SingleItem);
        public SlotModel BodySlot = new(ItemType.Body, SlotStackLimitType.SingleItem);
        public SlotModel HandsSlot = new(ItemType.Hands, SlotStackLimitType.SingleItem);
        public SlotModel ArmsSlot = new(ItemType.Arms, SlotStackLimitType.SingleItem);
        public SlotModel LegsSlot = new(ItemType.Legs, SlotStackLimitType.SingleItem);
        public SlotModel HipsSlot = new(ItemType.Hips, SlotStackLimitType.SingleItem);
        public SlotModel BackpackSlot = new(ItemType.Backpack, SlotStackLimitType.SingleItem);
        public SlotModel LeftWeaponSlot = new(ItemType.Weapon, SlotStackLimitType.SingleItem);
        public SlotModel RightWeaponSlot = new(ItemType.Weapon, SlotStackLimitType.SingleItem);
        public FastSlotModel FastSlot1 { get; } = new(1, "FastSlot1", "F1");
        public FastSlotModel FastSlot2 { get; } = new(2, "FastSlot2", "F2");
        public FastSlotModel FastSlot3 { get; } = new(3, "FastSlot3", "F3");
        public FastSlotModel FastSlot4 { get; } = new(4, "FastSlot4", "F4");
        public Vector2Int BaseInventorySize => inventoryConfig.Size;

        public PlayerInventory(InventoryConfig inventoryConfig)
        {
            this.inventoryConfig = inventoryConfig;
            var inventorySize = GetCurrentInventorySize();
            MaxWeight = GetCurrentMaxWeight();
            Tiles = new Tiles(inventorySize.x, inventorySize.y);
            CurrentWeightReactive = Observable.Merge(
                    changedSubject.Select(_ => CurrentWeight),
                    HandSlot.Select(_ => CurrentWeight))
                .StartWith(CurrentWeight)
                .DistinctUntilChanged()
                .ToReadOnlyReactiveProperty(CurrentWeight);
        }

        public bool IsWeightMovementBlocked()
        {
            return CurrentWeightPercent >= inventoryConfig.WeightBlocksMovementPercent;
        }

        public float GetMovementSlowdownNormalizedWeight()
        {
            var startPercent = Mathf.Clamp01(inventoryConfig.WeightAffectsMovementPercent);
            var currentPercent = Mathf.Clamp01(CurrentWeightPercent);
            if (currentPercent <= startPercent)
            {
                return 0f;
            }

            if (startPercent >= 1f)
            {
                return currentPercent >= 1f ? 1f : 0f;
            }

            return Mathf.InverseLerp(startPercent, 1f, currentPercent);
        }

        public bool CanAdd(ItemConfig config, Tile tile)
        {
            return config != null && tile != null && TryGetAvailableTiles(new ItemStack(config), tile, out _);
        }

        public bool CanAdd(ItemConfig config)
        {
            return config != null && TryFindFreeItemTiles(Tiles, config.Size, out _);
        }

        public bool TryGetFastSlot(int index, out FastSlotModel fastSlot)
        {
            fastSlot = index switch
            {
                1 => FastSlot1,
                2 => FastSlot2,
                3 => FastSlot3,
                4 => FastSlot4,
                _ => null
            };

            return fastSlot != null;
        }

        public IEnumerable<FastSlotModel> GetFastSlots()
        {
            yield return FastSlot1;
            yield return FastSlot2;
            yield return FastSlot3;
            yield return FastSlot4;
        }

        public bool AssignFastSlot(FastSlotModel fastSlot, ItemConfig itemConfig)
        {
            if (fastSlot == null || itemConfig == null || itemConfig.ItemType != ItemType.Usable)
            {
                return false;
            }

            fastSlot.Assign(itemConfig);
            NotifyChanged();
            return true;
        }

        public bool HasAnyInventoryItem(ItemConfig itemConfig)
        {
            return GetInventoryItemCount(itemConfig) > 0;
        }

        public int GetInventoryItemCount(ItemConfig itemConfig)
        {
            if (itemConfig == null)
            {
                return 0;
            }

            var totalCount = 0;
            foreach (var item in Items)
            {
                if (item?.ItemStack?.ItemConfig == itemConfig)
                {
                    totalCount += item.ItemStack.Count;
                }
            }

            var handItemStack = HandSlot.Value?.ItemStack;
            if (HandSourceInventory.Value == this && handItemStack?.ItemConfig == itemConfig)
            {
                totalCount += handItemStack.Count;
            }

            return totalCount;
        }

        public bool HasItemCount(ItemConfig itemConfig, int count)
        {
            return count <= 0 || GetInventoryItemCount(itemConfig) >= count;
        }

        public bool IsSlotBlocked(SlotModel slot)
        {
            return slot == FaceSlot && IsFaceSlotBlocked;
        }

        public IReadOnlyList<ItemStack> ConsumePendingOverflowItems()
        {
            if (pendingOverflowItems.Count == 0)
            {
                return System.Array.Empty<ItemStack>();
            }

            var result = pendingOverflowItems.ToArray();
            pendingOverflowItems.Clear();
            return result;
        }

        public bool TryConsumeItemCount(ItemConfig itemConfig, int count)
        {
            return count > 0 && ConsumeUpToItemCount(itemConfig, count) == count;
        }

        public int ConsumeUpToItemCount(ItemConfig itemConfig, int maxCount)
        {
            if (itemConfig == null || maxCount <= 0)
            {
                return 0;
            }

            var consumedCount = 0;
            var remainingCount = maxCount;
            var changed = false;

            foreach (var item in Items.ToList())
            {
                if (remainingCount <= 0 || item?.ItemStack?.ItemConfig != itemConfig)
                {
                    continue;
                }

                var countToTake = Mathf.Min(remainingCount, item.ItemStack.Count);
                item.ItemStack.Count -= countToTake;
                remainingCount -= countToTake;
                consumedCount += countToTake;
                changed = true;

                if (item.ItemStack.Count > 0)
                {
                    continue;
                }

                foreach (var tile in Tiles.tiles)
                {
                    if (tile.ItemInInventory == item)
                    {
                        tile.SetItem(null);
                    }
                }

                Items.Remove(item);
            }

            var handSlot = HandSlot.Value;
            if (remainingCount > 0 &&
                HandSourceInventory.Value == this &&
                handSlot?.ItemStack?.ItemConfig == itemConfig)
            {
                var countToTake = Mathf.Min(remainingCount, handSlot.ItemStack.Count);
                handSlot.ItemStack.Count -= countToTake;
                remainingCount -= countToTake;
                consumedCount += countToTake;
                changed = true;

                if (handSlot.ItemStack.Count <= 0)
                {
                    HandSlot.Value = new SlotModel(handSlot.ItemType, handSlot.StackLimitType, null);
                }
            }

            if (changed)
            {
                NotifyChanged();
            }

            return consumedCount;
        }

        /// <summary>
        /// Removes only stacks issued for a runtime-owned session. Unlike removal by item
        /// config this cannot take an identical item that belonged to the player beforehand.
        /// </summary>
        public int RemoveRuntimeTaggedItems(string runtimeTag)
        {
            if (string.IsNullOrWhiteSpace(runtimeTag))
            {
                return 0;
            }

            var removedCount = 0;
            foreach (ItemInInventory item in Items.ToList())
            {
                if (item?.ItemStack?.RuntimeTag != runtimeTag)
                {
                    continue;
                }

                removedCount += item.ItemStack.Count;
                foreach (Tile tile in Tiles.tiles)
                {
                    if (tile.ItemInInventory == item)
                    {
                        tile.SetItem(null);
                    }
                }

                Items.Remove(item);
            }

            foreach (SlotModel slot in GetSlots())
            {
                if (slot?.ItemStack?.RuntimeTag != runtimeTag)
                {
                    continue;
                }

                removedCount += slot.ItemStack.Count;
                slot.ItemStack = null;
                HandleSlotItemChanged(slot);
            }

            SlotModel handSlot = HandSlot.Value;
            if (HandSourceInventory.Value == this && handSlot?.ItemStack?.RuntimeTag == runtimeTag)
            {
                removedCount += handSlot.ItemStack.Count;
                HandSlot.Value = new SlotModel(handSlot.ItemType, handSlot.StackLimitType, null);
            }

            if (removedCount > 0)
            {
                NotifyChanged();
            }

            return removedCount;
        }

        public bool TryFindFirstItem(ItemConfig itemConfig, out ItemInInventory itemInInventory)
        {
            itemInInventory = itemConfig == null
                ? null
                : Items.FirstOrDefault(item => item?.ItemStack?.ItemConfig == itemConfig);
            return itemInInventory != null;
        }

        public bool TryAdd(ItemConfig config)
        {
            return TryAdd(new ItemStack(config)) == null;
        }

        public ItemStack TryAdd(ItemStack itemStack)
        {
            return TryAdd(itemStack, (Func<SlotModel, Vector2?>)null);
        }

        public ItemStack TryAdd(ItemStack itemStack, Func<SlotModel, Vector2?> slotSizeProvider)
        {
            var remainingStack = CloneIfValid(itemStack);
            if (remainingStack == null)
            {
                return itemStack;
            }

            var changed = FillExistingSlotStacks(remainingStack);
            changed |= FillExistingGridStacks(remainingStack);
            if (remainingStack.Count <= 0)
            {
                if (changed)
                {
                    NotifyChanged();
                }

                return null;
            }

            var freeSlotRemainder = TryAddToFreeSlot(remainingStack, slotSizeProvider);
            if (freeSlotRemainder == null)
            {
                NotifyChanged();
                return null;
            }

            changed |= freeSlotRemainder.Count != remainingStack.Count;
            remainingStack = freeSlotRemainder;
            var gridRemainder = TryAddToGrid(remainingStack);
            if (gridRemainder == null)
            {
                NotifyChanged();
                return null;
            }

            if (changed || gridRemainder.Count != remainingStack.Count)
            {
                NotifyChanged();
            }

            return gridRemainder;
        }

        public bool TryAddToGrid(ItemConfig config)
        {
            return TryAddToGrid(new ItemStack(config)) == null;
        }

        public ItemStack TryAddToGrid(ItemStack itemStack)
        {
            var remainingStack = CloneIfValid(itemStack);
            if (remainingStack == null)
            {
                return itemStack;
            }

            var changed = FillExistingGridStacks(remainingStack);
            while (remainingStack.Count > 0 && TryFindFreeItemTiles(Tiles, remainingStack.Size, out var itemTiles))
            {
                var countToPlace = Mathf.Min(remainingStack.Count, remainingStack.MaxStack);
                AddItem(new ItemStack(remainingStack.ItemConfig, countToPlace, remainingStack.IsRotated, remainingStack.RuntimeTag), itemTiles);
                remainingStack.Count -= countToPlace;
                changed = true;
            }

            if (changed)
            {
                NotifyChanged();
            }

            return remainingStack.Count > 0 ? remainingStack : null;
        }

        public ItemStack TryAddToGridRebuilding(ItemStack itemStack)
        {
            var remainingStack = CloneIfValid(itemStack);
            if (remainingStack == null)
            {
                return itemStack;
            }

            var changed = FillExistingGridStacks(remainingStack);
            while (remainingStack.Count > 0)
            {
                var countToPlace = Mathf.Min(remainingStack.Count, remainingStack.MaxStack);
                var stackToPlace = new ItemStack(remainingStack.ItemConfig, countToPlace, remainingStack.IsRotated, remainingStack.RuntimeTag);
                if (!TryBuildGridWithAdditionalItem(GetCurrentInventorySize(), stackToPlace, out var rebuiltTiles, out var rebuiltItems))
                {
                    break;
                }

                Tiles = rebuiltTiles;
                Items.Clear();
                foreach (var item in rebuiltItems)
                {
                    Items.Add(item);
                }

                remainingStack.Count -= countToPlace;
                changed = true;
            }

            if (changed)
            {
                NotifyChanged();
            }

            return remainingStack.Count > 0 ? remainingStack : null;
        }

        public bool CanMoveSlotItemToGrid(ItemType slotType)
        {
            return TryGetOccupiedSlot(slotType, out var slot) && CanMoveSlotItemToGrid(slot);
        }

        public bool CanMoveSlotItemToGrid(SlotModel slot)
        {
            if (slot?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            var targetSize = slot == BackpackSlot
                ? inventoryConfig.Size
                : new Vector2Int(Tiles.tiles.GetLength(0), Tiles.tiles.GetLength(1));
            return TryBuildGridWithAdditionalItem(targetSize, slot.ItemStack, out _, out _);
        }

        public bool TryMoveSlotItemToGrid(ItemType slotType)
        {
            return TryGetOccupiedSlot(slotType, out var slot) && TryMoveSlotItemToGrid(slot);
        }

        public bool TryMoveSlotItemToGrid(SlotModel slot)
        {
            if (slot?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            var targetSize = slot == BackpackSlot
                ? inventoryConfig.Size
                : new Vector2Int(Tiles.tiles.GetLength(0), Tiles.tiles.GetLength(1));
            if (!TryBuildGridWithAdditionalItem(targetSize, slot.ItemStack, out var rebuiltTiles, out var rebuiltItems))
            {
                return false;
            }

            slot.ItemStack = null;
            Tiles = rebuiltTiles;
            Items.Clear();
            foreach (var item in rebuiltItems)
            {
                Items.Add(item);
            }

            HandleSlotItemChanged(slot);
            NotifyChanged();
            return true;
        }

        public bool TryMoveFirstGridItemToEmptySlot(ItemType slotType)
        {
            var slot = GetSlots().FirstOrDefault(currentSlot => currentSlot.ItemType == slotType && currentSlot.ItemStack == null);
            if (slot == null)
            {
                return false;
            }

            var item = Items.FirstOrDefault(currentItem => currentItem?.ItemStack?.ItemConfig?.ItemType == slotType);
            if (item?.ItemStack == null || !CanAcceptItemInSlot(slot, item.ItemStack))
            {
                return false;
            }

            foreach (var tile in Tiles.tiles)
            {
                if (tile.ItemInInventory == item)
                {
                    tile.SetItem(null);
                }
            }

            slot.ItemStack = item.ItemStack;
            Items.Remove(item);
            HandleSlotItemChanged(slot);
            NotifyChanged();
            return true;
        }

        public bool TryTakeFromSlot(ItemType slotType, out ItemStack itemStack)
        {
            itemStack = null;
            return TryGetOccupiedSlot(slotType, out var slot) && TryTakeFromSlot(slot, out itemStack);
        }

        public bool TryTakeFromSlot(SlotModel slot, out ItemStack itemStack)
        {
            itemStack = null;
            if (slot?.ItemStack == null)
            {
                return false;
            }

            itemStack = slot.ItemStack;
            slot.ItemStack = null;
            NotifyChanged();
            return true;
        }

        public bool TryTakeFromSlot(ItemType slotType, int count, out ItemStack itemStack)
        {
            itemStack = null;
            return TryGetOccupiedSlot(slotType, out var slot) && TryTakeFromSlot(slot, count, out itemStack);
        }

        public bool TryTakeFromSlot(SlotModel slot, int count, out ItemStack itemStack)
        {
            itemStack = null;
            if (count <= 0 || slot?.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            var takenCount = Mathf.Min(count, slot.ItemStack.Count);
            itemStack = new ItemStack(slot.ItemStack.ItemConfig, takenCount, slot.ItemStack.IsRotated);
            if (takenCount >= slot.ItemStack.Count)
            {
                slot.ItemStack = null;
                HandleSlotItemChanged(slot);
            }
            else
            {
                slot.ItemStack.Count -= takenCount;
            }

            NotifyChanged();
            return true;
        }

        public bool TryPlaceInSlot(ItemType slotType, ItemStack newItemStack, out ItemStack remainderStack, out ItemStack replacedStack)
        {
            remainderStack = null;
            replacedStack = null;
            if (newItemStack == null || newItemStack.ItemConfig == null || !TryGetSlotForPlacement(slotType, newItemStack, out var slot))
            {
                return false;
            }

            return TryPlaceInSlot(slot, newItemStack, out remainderStack, out replacedStack);
        }

        public bool TryPlaceInSlot(SlotModel slot, ItemStack newItemStack, out ItemStack remainderStack, out ItemStack replacedStack)
        {
            return TryPlaceInSlot(slot, newItemStack, null, out remainderStack, out replacedStack);
        }

        public bool TryPlaceInSlot(
            SlotModel slot,
            ItemStack newItemStack,
            Vector2? slotSize,
            out ItemStack remainderStack,
            out ItemStack replacedStack)
        {
            remainderStack = null;
            replacedStack = null;
            if (newItemStack == null || newItemStack.ItemConfig == null || slot == null)
            {
                return false;
            }

            if (!CanAcceptItemInSlot(slot, newItemStack))
            {
                return false;
            }

            var originalRotation = newItemStack.IsRotated;
            RotateItemToFitSlot(newItemStack, slotSize);

            if (slot.ItemStack == null)
            {
                var emptySlotMaxStack = slot.GetMaxStack(newItemStack.ItemConfig);
                slot.ItemStack = new ItemStack(newItemStack.ItemConfig, Mathf.Min(newItemStack.Count, emptySlotMaxStack), newItemStack.IsRotated, newItemStack.RuntimeTag);
                if (newItemStack.Count > slot.ItemStack.Count)
                {
                    remainderStack = new ItemStack(newItemStack.ItemConfig, newItemStack.Count - slot.ItemStack.Count, newItemStack.IsRotated, newItemStack.RuntimeTag);
                }

                HandleSlotItemChanged(slot);
                NotifyChanged();
                return true;
            }

            if (slot.ItemStack.CanStackWith(newItemStack))
            {
                var existingSlotMaxStack = slot.GetMaxStack(newItemStack.ItemConfig);
                var freeSpace = existingSlotMaxStack - slot.ItemStack.Count;
                if (freeSpace <= 0)
                {
                    newItemStack.IsRotated = originalRotation;
                    remainderStack = newItemStack.Clone();
                    return false;
                }

                var movedCount = Mathf.Min(freeSpace, newItemStack.Count);
                slot.ItemStack.Count += movedCount;
                if (movedCount < newItemStack.Count)
                {
                    remainderStack = new ItemStack(newItemStack.ItemConfig, newItemStack.Count - movedCount, newItemStack.IsRotated, newItemStack.RuntimeTag);
                }

                NotifyChanged();
                return true;
            }

            replacedStack = slot.ItemStack;
            var slotMaxStack = slot.GetMaxStack(newItemStack.ItemConfig);
            var countToPlace = Mathf.Min(newItemStack.Count, slotMaxStack);
            slot.ItemStack = new ItemStack(newItemStack.ItemConfig, countToPlace, newItemStack.IsRotated, newItemStack.RuntimeTag);
            if (newItemStack.Count > countToPlace)
            {
                remainderStack = new ItemStack(newItemStack.ItemConfig, newItemStack.Count - countToPlace, newItemStack.IsRotated, newItemStack.RuntimeTag);
            }

            HandleSlotItemChanged(slot);
            NotifyChanged();
            return true;
        }

        public bool TryAdd(ItemConfig config, Tile tile)
        {
            return TryAdd(new ItemStack(config), tile) == null;
        }

        public ItemStack TryAdd(ItemStack itemStack, Tile tile)
        {
            var remainingStack = CloneIfValid(itemStack);
            if (remainingStack == null || tile == null)
            {
                return itemStack;
            }

            if (tile.ItemInInventory != null)
            {
                var existingItem = tile.ItemInInventory;
                if (!existingItem.ItemStack.CanStackWith(remainingStack) || existingItem.ItemStack.IsFull)
                {
                    return remainingStack;
                }

                var freeSpace = existingItem.ItemStack.MaxStack - existingItem.ItemStack.Count;
                var movedCount = Mathf.Min(freeSpace, remainingStack.Count);
                existingItem.ItemStack.Count += movedCount;
                remainingStack.Count -= movedCount;
                NotifyChanged();
                return remainingStack.Count > 0 ? remainingStack : null;
            }

            if (!TryGetAvailableTiles(remainingStack, tile, out var itemTiles))
            {
                return remainingStack;
            }

            var countToPlace = Mathf.Min(remainingStack.Count, remainingStack.MaxStack);
            AddItem(new ItemStack(remainingStack.ItemConfig, countToPlace, remainingStack.IsRotated, remainingStack.RuntimeTag), itemTiles);
            remainingStack.Count -= countToPlace;
            NotifyChanged();
            return remainingStack.Count > 0 ? remainingStack : null;
        }

        public void Add(ItemConfig config, Matrix4x4 position)
        {
            Add(new ItemStack(config), position);
        }

        public void Add(ItemStack itemStack, Matrix4x4 position)
        {
            if (itemStack?.ItemConfig == null)
            {
                return;
            }

            var itemCenterPosition = position.GetColumn(3);
            var startPosition = new Vector2Int(
                Mathf.RoundToInt(itemCenterPosition.x - (itemStack.Size.x - 1) * 0.5f),
                Mathf.RoundToInt(itemCenterPosition.y - (itemStack.Size.y - 1) * 0.5f));
            if (Tiles.TryGetTile(startPosition.x, startPosition.y, out var tile))
            {
                var remainder = TryAdd(itemStack, tile);
                if (remainder != null)
                {
                    TryAdd(remainder);
                }
            }
        }

        public bool CanGet(ItemInInventory itemInInventory)
        {
            return itemInInventory != null && Items.Contains(itemInInventory);
        }

        public bool TryGet(Tile tile, out ItemInInventory itemInInventory)
        {
            itemInInventory = tile?.ItemInInventory;
            if (!CanGet(itemInInventory))
            {
                itemInInventory = null;
                return false;
            }

            Remove(itemInInventory);
            return true;
        }

        public void Remove(ItemInInventory itemInInventory)
        {
            if (!CanGet(itemInInventory))
            {
                return;
            }

            foreach (var tile in Tiles.tiles)
            {
                if (tile.ItemInInventory == itemInInventory)
                {
                    tile.SetItem(null);
                }
            }

            Items.Remove(itemInInventory);
            NotifyChanged();
        }

        public IReadOnlyList<ItemStack> RebuildInventoryFromCurrentBackpack()
        {
            var newSize = GetCurrentInventorySize();
            var currentWidth = Tiles.tiles.GetLength(0);
            var currentHeight = Tiles.tiles.GetLength(1);
            if (currentWidth == newSize.x && currentHeight == newSize.y)
            {
                return System.Array.Empty<ItemStack>();
            }

            var transferEntries = CollectItemsInTileOrder(Tiles);
            var rebuiltTiles = new Tiles(newSize.x, newSize.y);
            var rebuiltItems = new List<ItemInInventory>(transferEntries.Count);
            var notPlacedEntries = new List<TransferEntry>();
            var droppedItems = new List<ItemStack>();

            foreach (var entry in transferEntries)
            {
                if (TryAddAtPosition(rebuiltTiles, rebuiltItems, entry.ItemStack, entry.PreferredPosition))
                {
                    continue;
                }

                notPlacedEntries.Add(entry);
            }

            var failedSizes = new List<Vector2Int>();
            foreach (var entry in notPlacedEntries)
            {
                var itemSize = entry.ItemStack.Size;
                if (failedSizes.Any(size => itemSize.x >= size.x && itemSize.y >= size.y))
                {
                    droppedItems.Add(entry.ItemStack.Clone());
                    continue;
                }

                if (TryAddToFirstFreePosition(rebuiltTiles, rebuiltItems, entry.ItemStack))
                {
                    continue;
                }

                failedSizes.Add(itemSize);
                droppedItems.Add(entry.ItemStack.Clone());
            }

            Tiles = rebuiltTiles;
            Items.Clear();
            foreach (var item in rebuiltItems)
            {
                Items.Add(item);
            }

            NotifyChanged();
            return droppedItems;
        }

        private Vector2Int GetCurrentInventorySize()
        {
            if (BackpackSlot.ItemConfig?.ItemType == ItemType.Backpack)
            {
                return BackpackSlot.ItemConfig.BackpackSize;
            }

            return inventoryConfig.Size;
        }

        private float GetCurrentMaxWeight()
        {
            var additionalWeightCapacity = BackpackSlot.ItemConfig?.ItemType == ItemType.Backpack
                ? BackpackSlot.ItemConfig.AdditionalWeightCapacity
                : 0f;
            return inventoryConfig.DefaultMaxWeight + additionalWeightCapacity;
        }

        private bool TryGetAvailableTiles(ItemStack itemStack, Tile tile, out List<Tile> itemTiles)
        {
            itemTiles = null;
            if (itemStack?.ItemConfig == null || tile == null)
            {
                return false;
            }

            var availableTiles = Tiles.GetTilesAround(tile.Index, itemStack.Size);
            if (availableTiles.Count != itemStack.Size.x * itemStack.Size.y || availableTiles.Any(currentTile => !currentTile.IsFree))
            {
                return false;
            }

            itemTiles = availableTiles;
            return true;
        }

        private bool FillExistingSlotStacks(ItemStack remainingStack)
        {
            var changed = false;
            foreach (var slot in GetSlots())
            {
                if (remainingStack.Count <= 0 || slot.ItemStack == null || !slot.ItemStack.CanStackWith(remainingStack))
                {
                    continue;
                }

                var slotMaxStack = slot.GetMaxStack(remainingStack.ItemConfig);
                if (slot.ItemStack.Count >= slotMaxStack)
                {
                    continue;
                }

                var freeSpace = slotMaxStack - slot.ItemStack.Count;
                var movedCount = Mathf.Min(freeSpace, remainingStack.Count);
                slot.ItemStack.Count += movedCount;
                remainingStack.Count -= movedCount;
                changed = true;
            }

            return changed;
        }

        private bool FillExistingGridStacks(ItemStack remainingStack)
        {
            var changed = false;
            foreach (var item in Items)
            {
                if (remainingStack.Count <= 0 || item.ItemStack == null || !item.ItemStack.CanStackWith(remainingStack) || item.ItemStack.IsFull)
                {
                    continue;
                }

                var freeSpace = item.ItemStack.MaxStack - item.ItemStack.Count;
                var movedCount = Mathf.Min(freeSpace, remainingStack.Count);
                item.ItemStack.Count += movedCount;
                remainingStack.Count -= movedCount;
                changed = true;
            }

            return changed;
        }

        private ItemStack TryAddToFreeSlot(ItemStack itemStack, Func<SlotModel, Vector2?> slotSizeProvider)
        {
            foreach (var slot in GetSlots())
            {
                if (!CanAcceptItemInSlot(slot, itemStack) || slot.ItemStack != null)
                {
                    continue;
                }

                RotateItemToFitSlot(itemStack, slotSizeProvider?.Invoke(slot));
                var countToPlace = Mathf.Min(itemStack.Count, slot.GetMaxStack(itemStack.ItemConfig));
                slot.ItemStack = new ItemStack(itemStack.ItemConfig, countToPlace, itemStack.IsRotated);
                HandleSlotItemChanged(slot);

                return itemStack.Count > countToPlace
                    ? new ItemStack(itemStack.ItemConfig, itemStack.Count - countToPlace, itemStack.IsRotated)
                    : null;
            }

            return itemStack;
        }

        private static bool TryFindFreeItemTiles(Tiles tiles, Vector2Int size, out List<Tile> itemTiles)
        {
            for (var y = 0; y < tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < tiles.tiles.GetLength(0); x++)
            {
                var availableTiles = tiles.GetTilesAround(new Vector2Int(x, y), size);
                if (availableTiles.Count != size.x * size.y || availableTiles.Any(tile => !tile.IsFree))
                {
                    continue;
                }

                itemTiles = availableTiles;
                return true;
            }

            itemTiles = null;
            return false;
        }

        private static List<TransferEntry> CollectItemsInTileOrder(Tiles sourceTiles)
        {
            var result = new List<TransferEntry>();
            var uniqueItems = new HashSet<ItemInInventory>();

            for (var y = 0; y < sourceTiles.tiles.GetLength(1); y++)
            for (var x = 0; x < sourceTiles.tiles.GetLength(0); x++)
            {
                var currentItem = sourceTiles.tiles[x, y].ItemInInventory;
                if (currentItem == null || !uniqueItems.Add(currentItem))
                {
                    continue;
                }

                var topLeftPosition = GetTopLeftTilePosition(currentItem);
                result.Add(new TransferEntry(currentItem.ItemStack.Clone(), topLeftPosition));
            }

            return result;
        }

        private static Vector2Int GetTopLeftTilePosition(ItemInInventory itemInInventory)
        {
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            foreach (var tile in itemInInventory.Tiles)
            {
                if (tile.Index.x < minX)
                {
                    minX = tile.Index.x;
                }

                if (tile.Index.y < minY)
                {
                    minY = tile.Index.y;
                }
            }

            return new Vector2Int(minX, minY);
        }

        private static bool TryAddAtPosition(Tiles targetTiles, List<ItemInInventory> targetItems, ItemStack itemStack, Vector2Int position)
        {
            if (!targetTiles.TryGetTile(position.x, position.y, out var tile))
            {
                return false;
            }

            var itemTiles = targetTiles.GetTilesAround(tile.Index, itemStack.Size);
            if (itemTiles.Count != itemStack.Size.x * itemStack.Size.y || itemTiles.Any(currentTile => !currentTile.IsFree))
            {
                return false;
            }

            AddItemToCollections(targetItems, itemStack.Clone(), itemTiles);
            return true;
        }

        private static bool TryAddToFirstFreePosition(Tiles targetTiles, List<ItemInInventory> targetItems, ItemStack itemStack)
        {
            for (var y = 0; y < targetTiles.tiles.GetLength(1); y++)
            for (var x = 0; x < targetTiles.tiles.GetLength(0); x++)
            {
                if (!targetTiles.TryGetTile(x, y, out var tile))
                {
                    continue;
                }

                var itemTiles = targetTiles.GetTilesAround(tile.Index, itemStack.Size);
                if (itemTiles.Count != itemStack.Size.x * itemStack.Size.y || itemTiles.Any(currentTile => !currentTile.IsFree))
                {
                    continue;
                }

                AddItemToCollections(targetItems, itemStack.Clone(), itemTiles);
                return true;
            }

            return false;
        }

        private bool TryBuildGridWithAdditionalItem(
            Vector2Int targetSize,
            ItemStack additionalItemStack,
            out Tiles rebuiltTiles,
            out List<ItemInInventory> rebuiltItems)
        {
            rebuiltTiles = null;
            rebuiltItems = null;

            var extraItemStack = CloneIfValid(additionalItemStack);
            if (extraItemStack == null || targetSize.x <= 0 || targetSize.y <= 0)
            {
                return false;
            }

            var transferEntries = CollectItemsInTileOrder(Tiles);
            transferEntries.Add(new TransferEntry(extraItemStack, new Vector2Int(-1, -1)));

            rebuiltTiles = new Tiles(targetSize.x, targetSize.y);
            rebuiltItems = new List<ItemInInventory>(transferEntries.Count);
            var notPlacedEntries = new List<TransferEntry>();

            foreach (var entry in transferEntries)
            {
                if (TryAddAtPosition(rebuiltTiles, rebuiltItems, entry.ItemStack, entry.PreferredPosition))
                {
                    continue;
                }

                notPlacedEntries.Add(entry);
            }

            foreach (var entry in notPlacedEntries)
            {
                if (TryAddToFirstFreePosition(rebuiltTiles, rebuiltItems, entry.ItemStack))
                {
                    continue;
                }

                rebuiltTiles = null;
                rebuiltItems = null;
                return false;
            }

            return true;
        }

        private static void AddItemToCollections(ICollection<ItemInInventory> targetItems, ItemStack itemStack, List<Tile> itemTiles)
        {
            var averagePosition = itemTiles
                .Select(tile => new Vector3(tile.Index.x, tile.Index.y, 0))
                .Aggregate(Vector3.zero, (current, position) => current + position) / itemTiles.Count;

            var itemInInventory = new ItemInInventory(itemStack, Matrix4x4.Translate(averagePosition))
            {
                Tiles = itemTiles
            };

            foreach (var tile in itemTiles)
            {
                tile.SetItem(itemInInventory);
            }

            targetItems.Add(itemInInventory);
        }

        private void AddItem(ItemStack itemStack, List<Tile> itemTiles)
        {
            AddItemToCollections(Items, itemStack, itemTiles);
        }

        private bool TryGetOccupiedSlot(ItemType slotType, out SlotModel slot)
        {
            slot = null;
            foreach (var currentSlot in GetSlots())
            {
                if (currentSlot.ItemType == slotType && currentSlot.ItemStack?.ItemConfig != null)
                {
                    slot = currentSlot;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetSlotForPlacement(ItemType slotType, ItemStack itemStack, out SlotModel slot)
        {
            slot = null;
            if (itemStack?.ItemConfig == null || itemStack.ItemConfig.ItemType != slotType)
            {
                return false;
            }

            foreach (var currentSlot in GetSlots())
            {
                if (currentSlot.ItemType != slotType || !CanAcceptItemInSlot(currentSlot, itemStack))
                {
                    continue;
                }

                if (currentSlot.ItemStack?.CanStackWith(itemStack) == true
                 && currentSlot.ItemStack.Count < currentSlot.GetMaxStack(itemStack.ItemConfig))
                {
                    slot = currentSlot;
                    return true;
                }
            }

            foreach (var currentSlot in GetSlots())
            {
                if (currentSlot.ItemType == slotType && currentSlot.ItemStack == null && CanAcceptItemInSlot(currentSlot, itemStack))
                {
                    slot = currentSlot;
                    return true;
                }
            }

            foreach (var currentSlot in GetSlots())
            {
                if (currentSlot.ItemType == slotType && CanAcceptItemInSlot(currentSlot, itemStack))
                {
                    slot = currentSlot;
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<SlotModel> GetSlots()
        {
            yield return HelmSlot;
            yield return FaceSlot;
            yield return BodySlot;
            yield return HandsSlot;
            yield return ArmsSlot;
            yield return LegsSlot;
            yield return HipsSlot;
            yield return BackpackSlot;
            yield return LeftWeaponSlot;
            yield return RightWeaponSlot;
        }

        private bool CanAcceptItemInSlot(SlotModel slot, ItemStack itemStack)
        {
            return slot != null
                   && itemStack?.ItemConfig != null
                   && slot.ItemType == itemStack.ItemConfig.ItemType
                   && !IsSlotBlocked(slot);
        }

        private static void RotateItemToFitSlot(ItemStack itemStack, Vector2? slotSize)
        {
            if (itemStack?.ItemConfig == null
             || !itemStack.CanRotate()
             || !slotSize.HasValue
             || slotSize.Value.x <= 0f
             || slotSize.Value.y <= 0f)
            {
                return;
            }

            var itemSize = itemStack.Size;
            if (itemSize.x == itemSize.y || Mathf.Approximately(slotSize.Value.x, slotSize.Value.y))
            {
                return;
            }

            if ((itemSize.x > itemSize.y) != (slotSize.Value.x > slotSize.Value.y))
            {
                itemStack.Rotate90();
            }
        }

        private void HandleSlotItemChanged(SlotModel slot)
        {
            if (slot == BackpackSlot)
            {
                MaxWeight = GetCurrentMaxWeight();
                pendingOverflowItems.AddRange(RebuildInventoryFromCurrentBackpack());
            }

            if (slot == HelmSlot)
            {
                ResolveBlockedFaceSlotOverflow();
            }
        }

        private void ResolveBlockedFaceSlotOverflow()
        {
            if (!IsFaceSlotBlocked || FaceSlot.ItemStack?.ItemConfig == null)
            {
                return;
            }

            var blockedFaceItem = FaceSlot.ItemStack;
            FaceSlot.ItemStack = null;

            var remainder = TryAdd(blockedFaceItem);
            if (remainder != null)
            {
                pendingOverflowItems.Add(remainder);
            }
        }

        private static ItemStack CloneIfValid(ItemStack itemStack)
        {
            return itemStack?.ItemConfig == null || itemStack.Count <= 0 ? null : itemStack.Clone();
        }

        private float GetItemsWeight()
        {
            var totalWeight = 0f;
            foreach (var item in Items)
            {
                if (item?.ItemStack != null)
                {
                    totalWeight += item.ItemStack.TotalWeight;
                }
            }

            return totalWeight;
        }

        private static float GetSlotWeight(SlotModel slot)
        {
            return slot?.ItemStack?.TotalWeight ?? 0f;
        }

        private void NotifyChanged()
        {
            changedSubject.OnNext(Unit.Default);
        }

        private readonly struct TransferEntry
        {
            public readonly ItemStack ItemStack;
            public readonly Vector2Int PreferredPosition;

            public TransferEntry(ItemStack itemStack, Vector2Int preferredPosition)
            {
                ItemStack = itemStack;
                PreferredPosition = preferredPosition;
            }
        }
    }
}
