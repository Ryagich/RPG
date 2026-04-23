using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Grid;
using Inventory.Item;
using UniRx;
using UnityEngine;

namespace Inventory.Inventories
{
    public class TradeSellInventory : ITiledInventory
    {
        private const int GridWidth = 7;
        private const int DefaultRows = 4;
        private const int RequiredFreeRows = 4;

        private readonly Subject<Unit> changedSubject = new();

        public ReactiveCollection<ItemInInventory> Items { get; } = new();
        public IObservable<Unit> Changed => changedSubject;
        public float MaxWeight { get; set; }
        public Tiles Tiles { get; private set; } = new(GridWidth, DefaultRows);

        public bool CanAdd(ItemConfig config, Tile tile)
        {
            return config != null && tile != null && TryGetAvailableTiles(new ItemStack(config), tile, out _);
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

            var changed = FillExistingStacks(remainingStack);
            for (var attempt = 0; remainingStack.Count > 0 && attempt < 8; attempt++)
            {
                if (TryAddToCurrentGrid(remainingStack, out changed))
                {
                    continue;
                }

                Resize(Tiles.tiles.GetLength(1) + RequiredFreeRows);
                changed = true;
            }

            if (changed)
            {
                EnsureRowsForFreeSpace();
                NotifyChanged();
            }

            return remainingStack.Count > 0 ? remainingStack : null;
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
                EnsureRowsForFreeSpace();
                NotifyChanged();
                return remainingStack.Count > 0 ? remainingStack : null;
            }

            if (!TryGetAvailableTiles(remainingStack, tile, out var itemTiles))
            {
                return remainingStack;
            }

            var countToPlace = Mathf.Min(remainingStack.Count, remainingStack.MaxStack);
            AddItem(new ItemStack(remainingStack.ItemConfig, countToPlace, remainingStack.IsRotated), itemTiles);
            remainingStack.Count -= countToPlace;
            EnsureRowsForFreeSpace();
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
            EnsureRowsForFreeSpace();
            NotifyChanged();
        }

        private bool TryAddToCurrentGrid(ItemStack remainingStack, out bool changed)
        {
            changed = false;
            if (!TryFindFreeItemTiles(remainingStack.Size, out var itemTiles))
            {
                return false;
            }

            var countToPlace = Mathf.Min(remainingStack.Count, remainingStack.MaxStack);
            AddItem(new ItemStack(remainingStack.ItemConfig, countToPlace, remainingStack.IsRotated), itemTiles);
            remainingStack.Count -= countToPlace;
            changed = true;
            return true;
        }

        private void EnsureRowsForFreeSpace()
        {
            var occupiedMaxRow = -1;
            foreach (var item in Items)
            {
                foreach (var tile in item.Tiles)
                {
                    if (tile.Index.y > occupiedMaxRow)
                    {
                        occupiedMaxRow = tile.Index.y;
                    }
                }
            }

            var desiredRows = occupiedMaxRow < 0
                ? DefaultRows
                : Mathf.Max(DefaultRows, occupiedMaxRow + 1 + RequiredFreeRows);

            if (Tiles.tiles.GetLength(1) == desiredRows)
            {
                return;
            }

            Resize(desiredRows);
        }

        private void Resize(int rows)
        {
            rows = Mathf.Max(rows, DefaultRows);
            var entries = CollectEntries();
            var newTiles = new Tiles(GridWidth, rows);
            var newItems = new List<ItemInInventory>(entries.Count);

            foreach (var entry in entries)
            {
                if (!TryAddEntry(newTiles, newItems, entry.ItemStack, entry.Position) && !TryAddFirstFree(newTiles, newItems, entry.ItemStack))
                {
                    continue;
                }
            }

            Tiles = newTiles;
            Items.Clear();
            foreach (var item in newItems)
            {
                Items.Add(item);
            }
        }

        private List<(ItemStack ItemStack, Vector2Int Position)> CollectEntries()
        {
            var result = new List<(ItemStack, Vector2Int)>();
            var unique = new HashSet<ItemInInventory>();
            for (var y = 0; y < Tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < Tiles.tiles.GetLength(0); x++)
            {
                var item = Tiles.tiles[x, y].ItemInInventory;
                if (item == null || !unique.Add(item))
                {
                    continue;
                }

                var minX = item.Tiles.Min(t => t.Index.x);
                var minY = item.Tiles.Min(t => t.Index.y);
                result.Add((item.ItemStack.Clone(), new Vector2Int(minX, minY)));
            }

            return result;
        }

        private static bool TryAddEntry(Tiles tiles, List<ItemInInventory> items, ItemStack itemStack, Vector2Int position)
        {
            if (!tiles.TryGetTile(position.x, position.y, out var tile))
            {
                return false;
            }

            var itemTiles = tiles.GetTilesAround(tile.Index, itemStack.Size);
            if (itemTiles.Count != itemStack.Size.x * itemStack.Size.y || itemTiles.Any(currentTile => !currentTile.IsFree))
            {
                return false;
            }

            AddItem(items, itemStack.Clone(), itemTiles);
            return true;
        }

        private static bool TryAddFirstFree(Tiles tiles, List<ItemInInventory> items, ItemStack itemStack)
        {
            for (var y = 0; y < tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < tiles.tiles.GetLength(0); x++)
            {
                var itemTiles = tiles.GetTilesAround(new Vector2Int(x, y), itemStack.Size);
                if (itemTiles.Count != itemStack.Size.x * itemStack.Size.y || itemTiles.Any(tile => !tile.IsFree))
                {
                    continue;
                }

                AddItem(items, itemStack.Clone(), itemTiles);
                return true;
            }

            return false;
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

        private bool FillExistingStacks(ItemStack remainingStack)
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

        private bool TryFindFreeItemTiles(Vector2Int size, out List<Tile> itemTiles)
        {
            for (var y = 0; y < Tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < Tiles.tiles.GetLength(0); x++)
            {
                var availableTiles = Tiles.GetTilesAround(new Vector2Int(x, y), size);
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

        private void AddItem(ItemStack itemStack, List<Tile> itemTiles)
        {
            AddItem(Items, itemStack, itemTiles);
        }

        private static void AddItem(ICollection<ItemInInventory> items, ItemStack itemStack, List<Tile> itemTiles)
        {
            var itemCenterPosition = itemTiles.Select(tile => (Vector2)tile.Index).Aggregate(Vector2.zero, (current, pos) => current + pos) / itemTiles.Count;
            var item = new ItemInInventory(itemStack, Matrix4x4.Translate(itemCenterPosition), itemTiles);
            foreach (var itemTile in itemTiles)
            {
                itemTile.SetItem(item);
            }

            items.Add(item);
        }

        private static ItemStack CloneIfValid(ItemStack itemStack)
        {
            return itemStack?.ItemConfig == null || itemStack.Count <= 0 ? null : itemStack.Clone();
        }

        private void NotifyChanged()
        {
            changedSubject.OnNext(Unit.Default);
        }
    }
}
