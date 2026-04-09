using System;
using Inventory.Grid;
using Inventory.Item;
using UniRx;
using UnityEngine;

namespace Inventory.Inventories
{
    public interface IInventory
    {
        public ReactiveCollection<ItemInInventory> Items { get; }
        public IObservable<Unit> Changed { get; }
        public float MaxWeight { get; set; }
        public bool CanAdd(ItemConfig config, Tile tile);
        public bool TryAdd(ItemConfig config);
        public ItemStack TryAdd(ItemStack itemStack);
        public bool TryAdd(ItemConfig config, Tile tile);
        public ItemStack TryAdd(ItemStack itemStack, Tile tile);
        public void Add(ItemConfig config, Matrix4x4 position);
        public void Add(ItemStack itemStack, Matrix4x4 position);
        public bool CanGet(ItemInInventory itemInInventory);
        public bool TryGet(Tile tile, out ItemInInventory itemInInventory);
        public void Remove(ItemInInventory itemInInventory);
    }
}
