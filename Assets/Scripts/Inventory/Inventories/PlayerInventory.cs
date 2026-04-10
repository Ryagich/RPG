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

        public ReactiveCollection<ItemInInventory> Items { get; } = new();
        public IObservable<Unit> Changed => changedSubject;
        public ReactiveProperty<SlotModel> HandSlot { get; } = new(new SlotModel(ItemType.None, SlotStackLimitType.SingleItem, null));
        public ReactiveProperty<IInventory> HandSourceInventory { get; } = new(null);
        public Tiles Tiles { get; private set; }
        public float MaxWeight { get; set; }

        public SlotModel HelmSlot = new(ItemType.Helm, SlotStackLimitType.SingleItem);
        public SlotModel BodySlot = new(ItemType.Body, SlotStackLimitType.SingleItem);
        public SlotModel BackpackSlot = new(ItemType.Backpack, SlotStackLimitType.SingleItem);

        public PlayerInventory(InventoryConfig inventoryConfig)
        {
            this.inventoryConfig = inventoryConfig;
            var inventorySize = GetCurrentInventorySize();
            MaxWeight = inventoryConfig.DefaultMaxWeight;
            Tiles = new Tiles(inventorySize.x, inventorySize.y);
        }

        public bool CanAdd(ItemConfig config, Tile tile)
        {
            return config != null && tile != null && TryGetAvailableTiles(config, tile, out _);
        }

        public bool CanAdd(ItemConfig config)
        {
            return config != null && TryFindFreeItemTiles(Tiles, config.Size, out _);
        }

        public bool TryAdd(ItemConfig config)
        {
            return TryAdd(new ItemStack(config)) == null;
        }

        public ItemStack TryAdd(ItemStack itemStack)
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

            var freeSlotRemainder = TryAddToFreeSlot(remainingStack);
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
            while (remainingStack.Count > 0 && TryFindFreeItemTiles(Tiles, remainingStack.ItemConfig.Size, out var itemTiles))
            {
                var countToPlace = Mathf.Min(remainingStack.Count, remainingStack.MaxStack);
                AddItem(new ItemStack(remainingStack.ItemConfig, countToPlace), itemTiles);
                remainingStack.Count -= countToPlace;
                changed = true;
            }

            if (changed)
            {
                NotifyChanged();
            }

            return remainingStack.Count > 0 ? remainingStack : null;
        }

        public bool TryTakeFromSlot(ItemType slotType, out ItemStack itemStack)
        {
            itemStack = null;
            if (!TryGetSlot(slotType, out var slot) || slot.ItemStack == null)
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
            if (count <= 0 || !TryGetSlot(slotType, out var slot) || slot.ItemStack?.ItemConfig == null)
            {
                return false;
            }

            var takenCount = Mathf.Min(count, slot.ItemStack.Count);
            itemStack = new ItemStack(slot.ItemStack.ItemConfig, takenCount);
            if (takenCount >= slot.ItemStack.Count)
            {
                slot.ItemStack = null;
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
            if (newItemStack == null || newItemStack.ItemConfig == null || !TryGetSlot(slotType, out var slot))
            {
                return false;
            }

            if (slot.ItemType != newItemStack.ItemConfig.ItemType)
            {
                return false;
            }

            if (slot.ItemStack == null)
            {
                var emptySlotMaxStack = slot.GetMaxStack(newItemStack.ItemConfig);
                slot.ItemStack = new ItemStack(newItemStack.ItemConfig, Mathf.Min(newItemStack.Count, emptySlotMaxStack));
                if (newItemStack.Count > slot.ItemStack.Count)
                {
                    remainderStack = new ItemStack(newItemStack.ItemConfig, newItemStack.Count - slot.ItemStack.Count);
                }

                NotifyChanged();
                return true;
            }

            if (slot.ItemStack.CanStackWith(newItemStack))
            {
                var existingSlotMaxStack = slot.GetMaxStack(newItemStack.ItemConfig);
                var freeSpace = existingSlotMaxStack - slot.ItemStack.Count;
                if (freeSpace <= 0)
                {
                    remainderStack = newItemStack.Clone();
                    return false;
                }

                var movedCount = Mathf.Min(freeSpace, newItemStack.Count);
                slot.ItemStack.Count += movedCount;
                if (movedCount < newItemStack.Count)
                {
                    remainderStack = new ItemStack(newItemStack.ItemConfig, newItemStack.Count - movedCount);
                }

                NotifyChanged();
                return true;
            }

            replacedStack = slot.ItemStack;
            var slotMaxStack = slot.GetMaxStack(newItemStack.ItemConfig);
            var countToPlace = Mathf.Min(newItemStack.Count, slotMaxStack);
            slot.ItemStack = new ItemStack(newItemStack.ItemConfig, countToPlace);
            if (newItemStack.Count > countToPlace)
            {
                remainderStack = new ItemStack(newItemStack.ItemConfig, newItemStack.Count - countToPlace);
            }

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

            if (!TryGetAvailableTiles(remainingStack.ItemConfig, tile, out var itemTiles))
            {
                return remainingStack;
            }

            var countToPlace = Mathf.Min(remainingStack.Count, remainingStack.MaxStack);
            AddItem(new ItemStack(remainingStack.ItemConfig, countToPlace), itemTiles);
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
                Mathf.RoundToInt(itemCenterPosition.x - (itemStack.ItemConfig.Size.x - 1) * 0.5f),
                Mathf.RoundToInt(itemCenterPosition.y - (itemStack.ItemConfig.Size.y - 1) * 0.5f));
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
                var itemSize = entry.ItemStack.ItemConfig.Size;
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
            if (BackpackSlot.ItemConfig is BackpackItemConfig backpackConfig)
            {
                return backpackConfig.BackpackSize;
            }

            return inventoryConfig.Size;
        }

        private bool TryGetAvailableTiles(ItemConfig config, Tile tile, out List<Tile> itemTiles)
        {
            itemTiles = null;
            if (config == null || tile == null)
            {
                return false;
            }

            var availableTiles = Tiles.GetTilesAround(tile.Index, config.Size);
            if (availableTiles.Count != config.Size.x * config.Size.y || availableTiles.Any(currentTile => !currentTile.IsFree))
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

        private ItemStack TryAddToFreeSlot(ItemStack itemStack)
        {
            foreach (var slot in GetSlots())
            {
                if (slot.ItemType != itemStack.ItemConfig.ItemType || slot.ItemStack != null)
                {
                    continue;
                }

                var countToPlace = Mathf.Min(itemStack.Count, slot.GetMaxStack(itemStack.ItemConfig));
                slot.ItemStack = new ItemStack(itemStack.ItemConfig, countToPlace);
                if (slot.ItemType == ItemType.Backpack)
                {
                    RebuildInventoryFromCurrentBackpack();
                }

                return itemStack.Count > countToPlace
                    ? new ItemStack(itemStack.ItemConfig, itemStack.Count - countToPlace)
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

            var itemTiles = targetTiles.GetTilesAround(tile.Index, itemStack.ItemConfig.Size);
            if (itemTiles.Count != itemStack.ItemConfig.Size.x * itemStack.ItemConfig.Size.y || itemTiles.Any(currentTile => !currentTile.IsFree))
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

                var itemTiles = targetTiles.GetTilesAround(tile.Index, itemStack.ItemConfig.Size);
                if (itemTiles.Count != itemStack.ItemConfig.Size.x * itemStack.ItemConfig.Size.y || itemTiles.Any(currentTile => !currentTile.IsFree))
                {
                    continue;
                }

                AddItemToCollections(targetItems, itemStack.Clone(), itemTiles);
                return true;
            }

            return false;
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

        private bool TryGetSlot(ItemType slotType, out SlotModel slot)
        {
            slot = slotType switch
            {
                ItemType.Helm => HelmSlot,
                ItemType.Body => BodySlot,
                ItemType.Backpack => BackpackSlot,
                _ => null
            };

            return slot != null;
        }

        private IEnumerable<SlotModel> GetSlots()
        {
            yield return HelmSlot;
            yield return BodySlot;
            yield return BackpackSlot;
        }

        private static ItemStack CloneIfValid(ItemStack itemStack)
        {
            return itemStack?.ItemConfig == null || itemStack.Count <= 0 ? null : itemStack.Clone();
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
