using Inventory.Grid;
using Inventory.Item;
using UniRx;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Inventory.Inventories
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ChestInventory : ITiledInventory
    {
        public ReactiveCollection<ItemInInventory> Items { get; } = new();

        public Tiles Tiles { get; } = new(7, 2);

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

        public bool TryAdd(ItemConfig config, Tile tile)
        {
            if (!TryGetAvailableTiles(config, tile, out var itemTiles))
            {
                return false;
            }

            AddItem(config, itemTiles);
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
            var itemCenterPosition = itemTiles.Select(tile => (Vector2)tile.Index).Aggregate(Vector2.zero, (current, pos) => current + pos) / itemTiles.Count;
            var item = new ItemInInventory(config, Matrix4x4.Translate(itemCenterPosition), itemTiles);
            foreach (var itemTile in itemTiles)
            {
                itemTile.SetItem(item);
            }

            Items.Add(item);
        }
    }
}