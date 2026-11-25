using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Grid;
using Inventory.Item;
using UniRx;
using UnityEngine;

namespace Inventory.Inventories
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ChestInventory : ITiledInventory
    {
        private readonly Subject<Unit> changedSubject = new();

        public ReactiveCollection<ItemInInventory> Items { get; } = new();
        public IObservable<Unit> Changed => changedSubject;
        public float MaxWeight { get; set; }
        public Tiles Tiles { get; } = new(7, 2);

        public ChestInventory(InventoryConfig inventoryConfig = null)
        {
            MaxWeight = inventoryConfig?.DefaultMaxWeight ?? -1f;
        }

        public bool CanAdd(ItemConfig config, Tile tile)
        {
            return config != null && tile != null && TryGetAvailableTiles(new ItemStack(config), tile, out _);
        }

        public bool TryAdd(ItemConfig config)
        {
            return TryAdd(new ItemStack(config)) == null;
        }

        public ItemStack TryAdd(ItemStack itemStack)
        {
            var remainingStack = CloneIfValid(itemStack);
            if (remainingStack == null)
            {
                return itemStack;
            }

            var changed = FillExistingStacks(remainingStack);
            while (remainingStack.Count > 0 && InventoryGridPlacement.TryFindFirstFreeTiles(Tiles, remainingStack, out var itemTiles))
            {
                var countToPlace = Mathf.Min(remainingStack.Count, remainingStack.MaxStack);
                AddItem(new ItemStack(remainingStack.ItemConfig, countToPlace, remainingStack.IsRotated), itemTiles);
                remainingStack.Count -= countToPlace;
                changed = true;
            }

            if (changed)
            {
                NotifyChanged();
            }

            return remainingStack.Count > 0 ? remainingStack : null;
        }

        public bool TryAdd(ItemConfig config, Tile tile)
        {
            return TryAdd(new ItemStack(config), tile) == null;
        }

        public ItemStack TryAdd(ItemStack itemStack, Tile tile)
        {
            var remainingStack = CloneIfValid(itemStack);
            if (remainingStack == null || tile == null)
            {
                return itemStack;
            }

            if (tile.ItemInInventory != null)
            {
                var existingItem = tile.ItemInInventory;
                if (!existingItem.ItemStack.CanStackWith(remainingStack) || existingItem.ItemStack.IsFull)
                {
                    return remainingStack;
                }

                var freeSpace = existingItem.ItemStack.MaxStack - existingItem.ItemStack.Count;
                var movedCount = Mathf.Min(freeSpace, remainingStack.Count);
                existingItem.ItemStack.Count += movedCount;
                remainingStack.Count -= movedCount;
                NotifyChanged();
                return remainingStack.Count > 0 ? remainingStack : null;
            }

            if (!TryGetAvailableTiles(remainingStack, tile, out var itemTiles))
            {
                return remainingStack;
            }

            var countToPlace = Mathf.Min(remainingStack.Count, remainingStack.MaxStack);
            AddItem(new ItemStack(remainingStack.ItemConfig, countToPlace, remainingStack.IsRotated), itemTiles);
            remainingStack.Count -= countToPlace;
            NotifyChanged();
            return remainingStack.Count > 0 ? remainingStack : null;
        }

        public void Add(ItemConfig config, Matrix4x4 position)
        {
            Add(new ItemStack(config), position);
        }

        public void Add(ItemStack itemStack, Matrix4x4 position)
        {
            if (itemStack?.ItemConfig == null)
            {
                return;
            }

            var itemCenterPosition = position.GetColumn(3);
            var startPosition = new Vector2Int(
                Mathf.RoundToInt(itemCenterPosition.x - (itemStack.Size.x - 1) * 0.5f),
                Mathf.RoundToInt(itemCenterPosition.y - (itemStack.Size.y - 1) * 0.5f));
            if (Tiles.TryGetTile(startPosition.x, startPosition.y, out var tile))
            {
                var remainder = TryAdd(itemStack, tile);
                if (remainder != null)
                {
                    TryAdd(remainder);
                }
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
            NotifyChanged();
        }

        private bool TryGetAvailableTiles(ItemStack itemStack, Tile tile, out List<Tile> itemTiles)
        {
            itemTiles = null;
            return tile != null && InventoryGridPlacement.TryFindFreeTilesAt(Tiles, tile.Index, itemStack, out itemTiles);
        }

        private bool FillExistingStacks(ItemStack remainingStack)
        {
            var changed = false;
            foreach (var item in Items)
            {
                if (remainingStack.Count <= 0 || item.ItemStack == null || !item.ItemStack.CanStackWith(remainingStack) || item.ItemStack.IsFull)
                {
                    continue;
                }

                var freeSpace = item.ItemStack.MaxStack - item.ItemStack.Count;
                var movedCount = Mathf.Min(freeSpace, remainingStack.Count);
                item.ItemStack.Count += movedCount;
                remainingStack.Count -= movedCount;
                changed = true;
            }

            return changed;
        }

        private void AddItem(ItemStack itemStack, List<Tile> itemTiles)
        {
            var itemCenterPosition = itemTiles.Select(tile => (Vector2)tile.Index).Aggregate(Vector2.zero, (current, pos) => current + pos) / itemTiles.Count;
            var item = new ItemInInventory(itemStack, Matrix4x4.Translate(itemCenterPosition), itemTiles);
            foreach (var itemTile in itemTiles)
            {
                itemTile.SetItem(item);
            }

            Items.Add(item);
        }

        private static ItemStack CloneIfValid(ItemStack itemStack)
        {
            return itemStack?.ItemConfig == null || itemStack.Count <= 0 ? null : itemStack.Clone();
        }

        private void NotifyChanged()
        {
            changedSubject.OnNext(Unit.Default);
        }
    }
}
