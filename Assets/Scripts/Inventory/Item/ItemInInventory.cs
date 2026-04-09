using System.Collections.Generic;
using Inventory.Grid;
using UnityEngine;

namespace Inventory.Item
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ItemInInventory
    {
        public readonly ItemStack ItemStack;
        public Matrix4x4 Position;
        public List<Tile> Tiles;

        public ItemConfig ItemConfig => ItemStack?.ItemConfig;
        public int Count => ItemStack?.Count ?? 0;
        
        public ItemInInventory(ItemStack itemStack, Matrix4x4 position)
        {
            ItemStack = itemStack;
            Position = position;
        }
        
        public ItemInInventory(ItemStack itemStack, Matrix4x4 position, List<Tile> tiles)
        {
            ItemStack = itemStack;
            Position = position;
            Tiles = tiles;
        }
    }
}
