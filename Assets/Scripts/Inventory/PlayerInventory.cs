using System.Collections.Generic;
using System.Linq;
using Inventory.Grid;
using Inventory.Item;
using UniRx;
using UnityEngine;

namespace Inventory
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerInventory : IInventory
    {
        public ReactiveCollection<ItemInInventory> Items { get; private set; } = new();
        public List<Slot> Slots = new();

        public Tiles Tiles;
        
        public PlayerInventory()
        {
            Debug.Log($"PlayerInventory");

            Tiles = new Tiles(7, 11);
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

                var averagePosition = itemTiles
                                     .Select(tile => new Vector3(tile.Index.x, tile.Index.y, 0))
                                     .Aggregate(Vector3.zero, (current, position) => current + position) / itemTiles.Count;

                var itemInInventory = new ItemInInventory(config, Matrix4x4.Translate(averagePosition));

                foreach (var tile in itemTiles)
                {
                    tile.SetItem(itemInInventory);
                }

                Items.Add(itemInInventory);
                return true;
            }

            return false;
        }
        
        public void Add(ItemConfig config, Matrix4x4 position)
        {
        }

        public bool CanGet(ItemInInventory itemInInventory)
        {
            return false;
        }

        public void Remove(ItemInInventory itemInInventory)
        {
        }
    }
}