using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inventory.Grid
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Tiles
    {
        public Tile[,] tiles { get; private set; }

        public Tiles(int width, int height)
        {
            tiles = new Tile[width, height];

            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                tiles[x, y] = new Tile(new Vector2Int(x, y));
            }
        }
        
        public Tile GetTile(int x, int y)
        {
            if (x < 0 || x >= tiles.GetLength(0)
             || y < 0 || y >= tiles.GetLength(1))
            {
                throw new IndexOutOfRangeException("Tile position out of bounds!");
            }
            return tiles[x, y];
        }
        
        public bool TryGetTile(int x, int y, out Tile tile)
        {
            if (x < 0 || x >= tiles.GetLength(0)
             || y < 0 || y >= tiles.GetLength(1))
            {
                tile = null;
                return false;
            }
            tile = tiles[x, y];
            return true;
        }
        
        public void SetTile(int x, int y, Tile tile)
        {
            if (x < 0 || x >= tiles.GetLength(0)
             || y < 0 || y >= tiles.GetLength(1))
            {
                throw new IndexOutOfRangeException("Tile position out of bounds!");
            }
            tiles[x, y] = tile;
        }
        
        public List<Tile> GetTilesAround(Vector2Int position, Vector2Int size)
        {
            var result = new List<Tile>();
            for (var x = position.x; x < position.x + size.x; x++)
            {
                if (x < 0 || x >= tiles.GetLength(0))
                {
                    continue;
                }
                for (var y = position.y; y < position.y + size.y; y++)
                {
                    if (y < 0 || y >= tiles.GetLength(1))
                    {
                        continue;
                    }
                    var tile = GetTile(x, y);
                    if (tile != null)
                    {
                        result.Add(tile);
                    }
                }
            }
            return result;
        }
    }
}