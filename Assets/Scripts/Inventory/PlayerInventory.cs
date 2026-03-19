using System.Collections.Generic;
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
            Tiles = new Tiles(7, 11);
        }
        
        public bool CanAdd(ItemConfig config)
        {
            return false;
        }
        public bool TryAdd(ItemConfig config)
        {
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