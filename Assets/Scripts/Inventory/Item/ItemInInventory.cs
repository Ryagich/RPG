using System.Collections.Generic;
using Inventory.Grid;
using UnityEngine;

namespace Inventory.Item
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ItemInInventory
    {
        public readonly ItemConfig ItemConfig;
        public Matrix4x4 Position;
        public List<Tile> Tiles;
        
        public ItemInInventory(ItemConfig itemConfig, Matrix4x4 position)
        {
            ItemConfig = itemConfig;
            Position = position;
        }
    }
}