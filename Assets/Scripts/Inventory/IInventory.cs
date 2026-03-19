using Inventory.Item;
using UniRx;
using UnityEngine;

namespace Inventory
{
    public interface IInventory
    {
        public ReactiveCollection<ItemInInventory> Items { get;}
        public bool CanAdd(ItemConfig config);
        public bool TryAdd(ItemConfig config);
        public void Add(ItemConfig config, Matrix4x4 position);
        public bool CanGet(ItemInInventory itemInInventory);
        public void Remove(ItemInInventory itemInInventory);
    }
}