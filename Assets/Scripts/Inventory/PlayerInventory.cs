using System.Collections.Generic;
using System.Linq;
using Inventory.Grid;
using Inventory.Item;
using Inventory.Slot;
using UniRx;
using UnityEngine;

namespace Inventory
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlayerInventory : IInventory
    {
        private readonly InventoryConfig inventoryConfig;
        
        public ReactiveCollection<ItemInInventory> Items { get; private set; } = new();
        public ReactiveProperty<SlotModel> HandSlot { get; } = new(new SlotModel(ItemType.None, null));

        public Tiles Tiles;
        
        public SlotModel HelmSlot = new(ItemType.Helm);
        public SlotModel BodySlot = new(ItemType.Body);
        public SlotModel BackpackSlot = new(ItemType.Backpack);

        public PlayerInventory(InventoryConfig inventoryConfig)
        {
            this.inventoryConfig = inventoryConfig;
            var inventorySize = GetCurrentInventorySize();
            Tiles = new Tiles(inventorySize.x, inventorySize.y);
        }
        
        public bool CanAdd(ItemConfig config, Tile tile)
        {
            return TryGetAvailableTiles(config, tile, out _);
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
            if (config == null)
            {
                return false;
            }

            if (TryAddToFreeSlot(config))
            {
                return true;
            }

            return TryAddToGrid(config);
        }

        public bool TryAddToGrid(ItemConfig config)
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
                AddItem(config, itemTiles);
                return true;
            }

            return false;
        }
        public bool TryTakeFromSlot(ItemType slotType, out ItemConfig itemConfig)
        {
            itemConfig = null;
            if (!TryGetSlot(slotType, out var slot) || slot.ItemConfig == null)
            {
                return false;
            }

            itemConfig = slot.ItemConfig;
            slot.ItemConfig = null;
            return true;
        }

        public bool TryPlaceInSlot(ItemType slotType, ItemConfig newItemConfig, out ItemConfig replacedItemConfig)
        {
            replacedItemConfig = null;
            if (newItemConfig == null || !TryGetSlot(slotType, out var slot))
            {
                return false;
            }

            if (slot.ItemType != newItemConfig.ItemType)
            {
                return false;
            }

            replacedItemConfig = slot.ItemConfig;
            slot.ItemConfig = newItemConfig;
            return true;
        }
        
        public bool TryAdd(ItemConfig config, Tile tile)
        {
            if (!TryGetAvailableTiles(config, tile, out var itemTiles))
            {
                return false;
            }

            AddItem(config, itemTiles);
            return true;
        }
        
        public void Add(ItemConfig config, Matrix4x4 position)
        {
            var itemCenterPosition = position.GetColumn(3);
            var startPosition = new Vector2Int(Mathf.RoundToInt(itemCenterPosition.x - (config.Size.x - 1) * 0.5f),
                                               Mathf.RoundToInt(itemCenterPosition.y - (config.Size.y - 1) * 0.5f));
            if (Tiles.TryGetTile(startPosition.x, startPosition.y, out var tile))
            {
                TryAdd(config, tile);
            }
        }

        public bool CanGet(ItemInInventory itemInInventory)
        {
            return itemInInventory != null && Items.Contains(itemInInventory);
        }
        
        public bool TryGet(Tile tile, out ItemInInventory itemInInventory)
        {
            itemInInventory = tile?.ItemInInventory;
            if (!CanGet(itemInInventory))
            {
                itemInInventory = null;
                return false;
            }

            Remove(itemInInventory);
            return true;
        }
        
        public void Remove(ItemInInventory itemInInventory)
        {
            if (!CanGet(itemInInventory))
            {
                return;
            }

            foreach (var tile in Tiles.tiles)
            {
                if (tile.ItemInInventory == itemInInventory)
                {
                    tile.SetItem(null);
                }
            }

            Items.Remove(itemInInventory);
        }
        
        private bool TryGetAvailableTiles(ItemConfig config, Tile tile, out List<Tile> itemTiles)
        {
            itemTiles = null;
            if (config is null || tile is null)
            {
                return false;
            }

            var availableTiles = Tiles.GetTilesAround(tile.Index, config.Size);
            if (availableTiles.Count != config.Size.x * config.Size.y || availableTiles.Any(currentTile => !currentTile.IsFree))
            {
                return false;
            }

            itemTiles = availableTiles;
            return true;
        }
        
        public IReadOnlyList<ItemConfig> RebuildInventoryFromCurrentBackpack()
        {
            var newSize = GetCurrentInventorySize();
            var currentWidth = Tiles.tiles.GetLength(0);
            var currentHeight = Tiles.tiles.GetLength(1);
            if (currentWidth == newSize.x && currentHeight == newSize.y)
            {
                return System.Array.Empty<ItemConfig>();
            }

            var transferEntries = CollectItemsInTileOrder(Tiles);
            var rebuiltTiles = new Tiles(newSize.x, newSize.y);
            var rebuiltItems = new List<ItemInInventory>(transferEntries.Count);
            var notPlacedEntries = new List<TransferEntry>();
            var droppedItems = new List<ItemConfig>();

            foreach (var entry in transferEntries)
            {
                if (TryAddAtPosition(rebuiltTiles, rebuiltItems, entry.Config, entry.PreferredPosition))
                {
                    continue;
                }

                notPlacedEntries.Add(entry);
            }

            var failedSizes = new List<Vector2Int>();
            foreach (var entry in notPlacedEntries)
            {
                var itemSize = entry.Config.Size;
                if (failedSizes.Any(size => itemSize.x >= size.x && itemSize.y >= size.y))
                {
                    droppedItems.Add(entry.Config);
                    continue;
                }

                if (TryAddToFirstFreePosition(rebuiltTiles, rebuiltItems, entry.Config))
                {
                    continue;
                }

                failedSizes.Add(itemSize);
                droppedItems.Add(entry.Config);
            }

            Tiles = rebuiltTiles;
            Items.Clear();
            foreach (var item in rebuiltItems)
            {
                Items.Add(item);
            }

            return droppedItems;
        }
         private Vector2Int GetCurrentInventorySize()
        {
            if (BackpackSlot.ItemConfig is BackpackItemConfig backpackConfig)
            {
                return backpackConfig.Size;
            }

            return inventoryConfig.Size;
        }

        private static List<TransferEntry> CollectItemsInTileOrder(Tiles sourceTiles)
        {
            var result = new List<TransferEntry>();
            var uniqueItems = new HashSet<ItemInInventory>();

            for (var y = 0; y < sourceTiles.tiles.GetLength(1); y++)
            for (var x = 0; x < sourceTiles.tiles.GetLength(0); x++)
            {
                var currentItem = sourceTiles.tiles[x, y].ItemInInventory;
                if (currentItem == null || !uniqueItems.Add(currentItem))
                {
                    continue;
                }

                var topLeftPosition = GetTopLeftTilePosition(currentItem);
                result.Add(new TransferEntry(currentItem.ItemConfig, topLeftPosition));
            }

            return result;
        }

        private static Vector2Int GetTopLeftTilePosition(ItemInInventory itemInInventory)
        {
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            foreach (var tile in itemInInventory.Tiles)
            {
                if (tile.Index.x < minX)
                {
                    minX = tile.Index.x;
                }

                if (tile.Index.y < minY)
                {
                    minY = tile.Index.y;
                }
            }

            return new Vector2Int(minX, minY);
        }

        private static bool TryAddAtPosition(Tiles targetTiles, List<ItemInInventory> targetItems, ItemConfig config, Vector2Int position)
        {
            if (!targetTiles.TryGetTile(position.x, position.y, out var tile))
            {
                return false;
            }

            var itemTiles = targetTiles.GetTilesAround(tile.Index, config.Size);
            if (itemTiles.Count != config.Size.x * config.Size.y || itemTiles.Any(currentTile => !currentTile.IsFree))
            {
                return false;
            }

            AddItemToCollections(targetItems, config, itemTiles);
            return true;
        }

        private static bool TryAddToFirstFreePosition(Tiles targetTiles, List<ItemInInventory> targetItems, ItemConfig config)
        {
            for (var y = 0; y < targetTiles.tiles.GetLength(1); y++)
            for (var x = 0; x < targetTiles.tiles.GetLength(0); x++)
            {
                if (!targetTiles.TryGetTile(x, y, out var tile))
                {
                    continue;
                }

                var itemTiles = targetTiles.GetTilesAround(tile.Index, config.Size);
                if (itemTiles.Count != config.Size.x * config.Size.y || itemTiles.Any(currentTile => !currentTile.IsFree))
                {
                    continue;
                }

                AddItemToCollections(targetItems, config, itemTiles);
                return true;
            }

            return false;
        }

        private static void AddItemToCollections(List<ItemInInventory> targetItems, ItemConfig config, List<Tile> itemTiles)
        {
            var averagePosition = itemTiles
                                 .Select(tile => new Vector3(tile.Index.x, tile.Index.y, 0))
                                 .Aggregate(Vector3.zero, (current, position) => current + position) / itemTiles.Count;

            var itemInInventory = new ItemInInventory(config, Matrix4x4.Translate(averagePosition))
                                  {
                                      Tiles = itemTiles
                                  };

            foreach (var tile in itemTiles)
            {
                tile.SetItem(itemInInventory);
            }

            targetItems.Add(itemInInventory);
        }

        private readonly struct TransferEntry
        {
            public readonly ItemConfig Config;
            public readonly Vector2Int PreferredPosition;

            public TransferEntry(ItemConfig config, Vector2Int preferredPosition)
            {
                Config = config;
                PreferredPosition = preferredPosition;
            }
        }

        private void AddItem(ItemConfig config, List<Tile> itemTiles)
        {
            var averagePosition = itemTiles
                                 .Select(tile => new Vector3(tile.Index.x, tile.Index.y, 0))
                                 .Aggregate(Vector3.zero, (current, position) => current + position) / itemTiles.Count;

            var itemInInventory = new ItemInInventory(config, Matrix4x4.Translate(averagePosition))
                                  {
                                      Tiles = itemTiles
                                  };

            foreach (var tile in itemTiles)
            {
                tile.SetItem(itemInInventory);
            }

            Items.Add(itemInInventory);
        }
        private bool TryAddToFreeSlot(ItemConfig config)
        {
            if (config == null)
            {
                return false;
            }

            foreach (var slot in GetSlots())
            {
                if (slot.ItemType == config.ItemType && slot.ItemConfig == null)
                {
                    slot.ItemConfig = config;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetSlot(ItemType slotType, out SlotModel slot)
        {
            slot = slotType switch
                   {
                       ItemType.Helm => HelmSlot,
                       ItemType.Body => BodySlot,
                       ItemType.Backpack => BackpackSlot,
                       _ => null
                   };

            return slot != null;
        }

        private IEnumerable<SlotModel> GetSlots()
        {
            yield return HelmSlot;
            yield return BodySlot;
            yield return BackpackSlot;
        }
    }
}