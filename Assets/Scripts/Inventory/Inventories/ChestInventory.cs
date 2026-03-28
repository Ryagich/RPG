using Inventory.Grid;
using Inventory.Item;
using UniRx;
using UnityEngine;

namespace Inventory.Inventories
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ChestInventory : IInventory
    {
        public ReactiveCollection<ItemInInventory> Items { private set; get; }
        
        public Tiles Tiles = new(7, 2);
        
        public bool CanAdd(ItemConfig config, Tile tile)
        {
            return false;
        }

        public bool TryAdd(ItemConfig config)
        {
            return false;
        }

        public bool TryAdd(ItemConfig config, Tile tile)
        {
            return false;
        }

        public void Add(ItemConfig config, Matrix4x4 position) { }

        public bool CanGet(ItemInInventory itemInInventory)
        {
            return false;
        }

        public bool TryGet(Tile tile, out ItemInInventory itemInInventory)
        {
            itemInInventory = null;
            return false;
        }

        public void Remove(ItemInInventory itemInInventory) { }
    }
}