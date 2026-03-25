using System.Collections.Generic;
using System.Linq;
using Inventory.Grid;
using Inventory.Item;
using Inventory.Slot;
using UniRx;
using UnityEngine;

namespace Inventory
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerInventory : IInventory
    {
        public ReactiveCollection<ItemInInventory> Items { get; private set; } = new();
        public ReactiveProperty<SlotModel> HandSlot { get; } = new(new SlotModel(ItemType.None, null));

        public Tiles Tiles;
        
        public SlotModel HelmSlot = new(ItemType.Helm);
        public SlotModel BodySlot = new(ItemType.Body);
        public SlotModel BackpackSlot = new(ItemType.Backpack);

        public PlayerInventory()
        {
            Tiles = new Tiles(7, 11);
        }
        
        public bool CanAdd(ItemConfig config, Tile tile)
        {
            return TryGetAvailableTiles(config, tile, out _);
        }
        
        public bool CanAdd(ItemConfig config)
        {
            if (config is null || config.Size.x <= 0 || config.Size.y <= 0)
            {
                return false;
            }

            for (var y = 0; y < Tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < Tiles.tiles.GetLength(0); x++)
            {
                var itemTiles = Tiles.GetTilesAround(new Vector2Int(x, y), config.Size);
                if (itemTiles.Count == config.Size.x * config.Size.y && itemTiles.All(tile => tile.IsFree))
                {
                    return true;
                }
            }

            return false;
        }
        
        public bool TryAdd(ItemConfig config)
        {
            if (!CanAdd(config))
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
            if (config is null || tile is null)
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
            var averagePosition = itemTiles
                                 .Select(tile => new Vector3(tile.Index.x, tile.Index.y, 0))
                                 .Aggregate(Vector3.zero, (current, position) => current + position) / itemTiles.Count;

            var itemInInventory = new ItemInInventory(config, Matrix4x4.Translate(averagePosition))
                                  {
                                      Tiles = itemTiles
                                  };

            foreach (var tile in itemTiles)
            {
                tile.SetItem(itemInInventory);
            }

            Items.Add(itemInInventory);
        }
    }
}