using System;
using Inventory.Item;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Grid
{
    [Serializable]
    public class Tile
    {
        [field: SerializeField] public Vector2Int Index { get; private set; }
        [field: SerializeField] public ItemInInventory ItemInInventory { get; private set; }

        public bool IsFree => ItemInInventory is null;
    }
}