using System.Collections.Generic;
using System.Linq;
using Inventory.Grid;
using Inventory.Item;
using UnityEngine;

namespace Inventory.Inventories
{
    /// <summary>
    /// Finds a free position for an item that is being placed automatically.
    /// The item keeps its current orientation when possible; otherwise, it is rotated once
    /// before the placement is accepted.
    /// </summary>
    public static class InventoryGridPlacement
    {
        public static bool TryFindFirstFreeTiles(Tiles tiles, ItemStack itemStack, out List<Tile> itemTiles)
        {
            itemTiles = null;
            if (tiles == null || itemStack?.ItemConfig == null)
            {
                return false;
            }

            if (TryFindFirstFreeTiles(tiles, itemStack.Size, out itemTiles))
            {
                return true;
            }

            if (!itemStack.CanRotate())
            {
                return false;
            }

            itemStack.Rotate90();
            if (TryFindFirstFreeTiles(tiles, itemStack.Size, out itemTiles))
            {
                return true;
            }

            itemStack.Rotate90();
            return false;
        }

        public static bool CanFit(Tiles tiles, ItemStack itemStack)
        {
            return itemStack?.ItemConfig != null
                   && TryFindFirstFreeTiles(tiles, itemStack.Clone(), out _);
        }

        public static bool TryFindFreeTilesAt(
            Tiles tiles,
            Vector2Int position,
            ItemStack itemStack,
            out List<Tile> itemTiles)
        {
            itemTiles = null;
            return tiles != null
                   && itemStack?.ItemConfig != null
                   && TryFindFreeTilesAt(tiles, position, itemStack.Size, out itemTiles);
        }

        private static bool TryFindFirstFreeTiles(Tiles tiles, Vector2Int size, out List<Tile> itemTiles)
        {
            for (var y = 0; y < tiles.tiles.GetLength(1); y++)
            for (var x = 0; x < tiles.tiles.GetLength(0); x++)
            {
                if (TryFindFreeTilesAt(tiles, new Vector2Int(x, y), size, out itemTiles))
                {
                    return true;
                }
            }

            itemTiles = null;
            return false;
        }

        private static bool TryFindFreeTilesAt(
            Tiles tiles,
            Vector2Int position,
            Vector2Int size,
            out List<Tile> itemTiles)
        {
            itemTiles = null;
            if (!tiles.TryGetTile(position.x, position.y, out var tile))
            {
                return false;
            }

            var availableTiles = tiles.GetTilesAround(tile.Index, size);
            if (availableTiles.Count != size.x * size.y || availableTiles.Any(currentTile => !currentTile.IsFree))
            {
                return false;
            }

            itemTiles = availableTiles;
            return true;
        }
    }
}
