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

        public ReactiveCollection<ItemInInventory> Items { get; } = new();
        public Tiles Tiles { get; private set; } = new(GridWidth, DefaultRows);

        public bool CanAdd(ItemConfig config, Tile tile)
        {
            return TryGetAvailableTiles(config, tile, out _);
        }

        public bool TryAdd(ItemConfig config)
        {
            if (config == null)
            {
                return false;
            }

            for (var attempt = 0; attempt < 8; attempt++)
            {
                if (TryAddToCurrentGrid(config))
                {
                    EnsureRowsForFreeSpace();
                    return true;
                }

                Resize(Tiles.tiles.GetLength(1) + RequiredFreeRows);
            }

            return false;
        }

        public bool TryAdd(ItemConfig config, Tile tile)
        {
            if (!TryGetAvailableTiles(config, tile, out var itemTiles))
            {
                return false;
            }

            AddItem(config, itemTiles);
            EnsureRowsForFreeSpace();
            return true;
        }

        public void Add(ItemConfig config, Matrix4x4 position)
        {
            var itemCenterPosition = position.GetColumn(3);
            var startPosition = new Vector2Int(Mathf.RoundToInt(itemCenterPosition.x - (config.Size.x - 1) * 0.5f),
                                               Mathf.RoundToInt(itemCenterPosition.y - (config.Size.y - 1) * 0.5f));
            if (Tiles.TryGetTile(startPosition.x, startPosition.y, out var tile))
            {
                TryAdd(config, tile);
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
        }

        private bool TryAddToCurrentGrid(ItemConfig config)
        {
            for (var y = 0; y < Tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < Tiles.tiles.GetLength(0); x++)
            {
                var itemTiles = Tiles.GetTilesAround(new Vector2Int(x, y), config.Size);
                if (itemTiles.Count != config.Size.x * config.Size.y || itemTiles.Any(tile => !tile.IsFree))
                {
                    continue;
                }

                AddItem(config, itemTiles);
                return true;
            }

            return false;
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
                if (!TryAddEntry(newTiles, newItems, entry.Config, entry.Position) && !TryAddFirstFree(newTiles, newItems, entry.Config))
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

        private List<(ItemConfig Config, Vector2Int Position)> CollectEntries()
        {
            var result = new List<(ItemConfig, Vector2Int)>();
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
                result.Add((item.ItemConfig, new Vector2Int(minX, minY)));
            }

            return result;
        }

        private static bool TryAddEntry(Tiles tiles, List<ItemInInventory> items, ItemConfig config, Vector2Int position)
        {
            if (!tiles.TryGetTile(position.x, position.y, out var tile))
            {
                return false;
            }

            var itemTiles = tiles.GetTilesAround(tile.Index, config.Size);
            if (itemTiles.Count != config.Size.x * config.Size.y || itemTiles.Any(currentTile => !currentTile.IsFree))
            {
                return false;
            }

            AddItem(items, config, itemTiles);
            return true;
        }

        private static bool TryAddFirstFree(Tiles tiles, List<ItemInInventory> items, ItemConfig config)
        {
            for (var y = 0; y < tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < tiles.tiles.GetLength(0); x++)
            {
                var itemTiles = tiles.GetTilesAround(new Vector2Int(x, y), config.Size);
                if (itemTiles.Count != config.Size.x * config.Size.y || itemTiles.Any(tile => !tile.IsFree))
                {
                    continue;
                }

                AddItem(items, config, itemTiles);
                return true;
            }

            return false;
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

        private void AddItem(ItemConfig config, List<Tile> itemTiles)
        {
            AddItem(Items, config, itemTiles);
        }

        private static void AddItem(ICollection<ItemInInventory> items, ItemConfig config, List<Tile> itemTiles)
        {
            var itemCenterPosition = itemTiles.Select(tile => (Vector2)tile.Index).Aggregate(Vector2.zero, (current, pos) => current + pos) / itemTiles.Count;
            var item = new ItemInInventory(config, Matrix4x4.Translate(itemCenterPosition), itemTiles);
            foreach (var itemTile in itemTiles)
            {
                itemTile.SetItem(item);
            }

            items.Add(item);
        }
    }
}