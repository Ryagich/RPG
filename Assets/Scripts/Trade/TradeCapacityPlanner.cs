using System.Collections.Generic;
using System.Linq;
using Inventory.Inventories;
using Inventory.Item;
using Inventory.Slot;
using UnityEngine;

namespace Trade
{
public sealed class TradeCapacityPlanner
{
    private sealed class SnapshotSlot
    {
        public readonly ItemType ItemType;
        public readonly SlotStackLimitType StackLimitType;
        public ItemStack ItemStack;

        public SnapshotSlot(ItemType itemType, SlotStackLimitType stackLimitType, ItemStack itemStack)
        {
            ItemType = itemType;
            StackLimitType = stackLimitType;
            ItemStack = itemStack?.Clone();
        }

        public int GetMaxStack(ItemConfig itemConfig)
        {
            if (itemConfig == null)
            {
                return 1;
            }

            return StackLimitType == SlotStackLimitType.ItemConfigMaxStack
                ? itemConfig.MaxStack
                : 1;
        }
    }

    private sealed class SnapshotItem
    {
        public readonly ItemStack ItemStack;
        public readonly List<Vector2Int> OccupiedCells;

        public SnapshotItem(ItemStack itemStack, IEnumerable<Vector2Int> occupiedCells)
        {
            ItemStack = itemStack?.Clone();
            OccupiedCells = occupiedCells?.ToList() ?? new List<Vector2Int>();
        }
    }

    private readonly List<SnapshotSlot> slots;
    private readonly List<SnapshotItem> items;
    private Vector2Int gridSize;

    private TradeCapacityPlanner(Vector2Int gridSize, IEnumerable<SnapshotItem> items, IEnumerable<SnapshotSlot> slots = null)
    {
        this.gridSize = gridSize;
        this.items = items?.ToList() ?? new List<SnapshotItem>();
        this.slots = slots?.ToList() ?? new List<SnapshotSlot>();
    }

    public static bool TryCreate(IInventory inventory, out TradeCapacityPlanner snapshot)
    {
        if (inventory is PlayerInventory playerInventory)
        {
            snapshot = new TradeCapacityPlanner(
                new Vector2Int(playerInventory.Tiles.tiles.GetLength(0), playerInventory.Tiles.tiles.GetLength(1)),
                CreateSnapshotItems(playerInventory.Items),
                new[]
                {
                    new SnapshotSlot(playerInventory.HelmSlot.ItemType, playerInventory.HelmSlot.StackLimitType, playerInventory.HelmSlot.ItemStack),
                    new SnapshotSlot(playerInventory.FaceSlot.ItemType, playerInventory.FaceSlot.StackLimitType, playerInventory.FaceSlot.ItemStack),
                    new SnapshotSlot(playerInventory.BodySlot.ItemType, playerInventory.BodySlot.StackLimitType, playerInventory.BodySlot.ItemStack),
                    new SnapshotSlot(playerInventory.HandsSlot.ItemType, playerInventory.HandsSlot.StackLimitType, playerInventory.HandsSlot.ItemStack),
                    new SnapshotSlot(playerInventory.ArmsSlot.ItemType, playerInventory.ArmsSlot.StackLimitType, playerInventory.ArmsSlot.ItemStack),
                    new SnapshotSlot(playerInventory.LegsSlot.ItemType, playerInventory.LegsSlot.StackLimitType, playerInventory.LegsSlot.ItemStack),
                    new SnapshotSlot(playerInventory.HipsSlot.ItemType, playerInventory.HipsSlot.StackLimitType, playerInventory.HipsSlot.ItemStack),
                    new SnapshotSlot(playerInventory.BackpackSlot.ItemType, playerInventory.BackpackSlot.StackLimitType, playerInventory.BackpackSlot.ItemStack),
                    new SnapshotSlot(playerInventory.LeftWeaponSlot.ItemType, playerInventory.LeftWeaponSlot.StackLimitType, playerInventory.LeftWeaponSlot.ItemStack),
                    new SnapshotSlot(playerInventory.RightWeaponSlot.ItemType, playerInventory.RightWeaponSlot.StackLimitType, playerInventory.RightWeaponSlot.ItemStack)
                });
            return true;
        }

        if (inventory is ITiledInventory tiledInventory)
        {
            snapshot = new TradeCapacityPlanner(
                new Vector2Int(tiledInventory.Tiles.tiles.GetLength(0), tiledInventory.Tiles.tiles.GetLength(1)),
                CreateSnapshotItems(tiledInventory.Items));
            return true;
        }

        snapshot = null;
        return false;
    }

    public bool CanAddStacks(IEnumerable<ItemStack> itemStacks)
    {
        if (itemStacks == null)
        {
            return true;
        }

        foreach (var itemStack in itemStacks)
        {
            if (TryAdd(itemStack) != null)
            {
                return false;
            }
        }

        return true;
    }

    private ItemStack TryAdd(ItemStack itemStack)
    {
        var remainingStack = CloneIfValid(itemStack);
        if (remainingStack == null)
        {
            return null;
        }

        FillExistingSlotStacks(remainingStack);
        FillExistingGridStacks(remainingStack);
        if (remainingStack.Count <= 0)
        {
            return null;
        }

        remainingStack = TryAddToFreeSlot(remainingStack);
        if (remainingStack == null || remainingStack.Count <= 0)
        {
            return null;
        }

        return TryAddToGrid(remainingStack);
    }

    private void FillExistingSlotStacks(ItemStack remainingStack)
    {
        foreach (var slot in slots)
        {
            if (remainingStack.Count <= 0
             || slot.ItemStack == null
             || !CanAcceptInSlot(slot, remainingStack)
             || !slot.ItemStack.CanStackWith(remainingStack))
            {
                continue;
            }

            var maxStack = slot.GetMaxStack(remainingStack.ItemConfig);
            if (slot.ItemStack.Count >= maxStack)
            {
                continue;
            }

            var movedCount = Mathf.Min(maxStack - slot.ItemStack.Count, remainingStack.Count);
            slot.ItemStack.Count += movedCount;
            remainingStack.Count -= movedCount;
        }
    }

    private void FillExistingGridStacks(ItemStack remainingStack)
    {
        foreach (var item in items)
        {
            if (remainingStack.Count <= 0 || item.ItemStack == null || !item.ItemStack.CanStackWith(remainingStack) || item.ItemStack.IsFull)
            {
                continue;
            }

            var movedCount = Mathf.Min(item.ItemStack.MaxStack - item.ItemStack.Count, remainingStack.Count);
            item.ItemStack.Count += movedCount;
            remainingStack.Count -= movedCount;
        }
    }

    private ItemStack TryAddToFreeSlot(ItemStack itemStack)
    {
        foreach (var slot in slots)
        {
            if (!CanAcceptInSlot(slot, itemStack) || slot.ItemStack != null)
            {
                continue;
            }

            var countToPlace = Mathf.Min(itemStack.Count, slot.GetMaxStack(itemStack.ItemConfig));
            slot.ItemStack = new ItemStack(itemStack.ItemConfig, countToPlace, itemStack.IsRotated);

            if (slot.ItemType == ItemType.Backpack
             && !TryResizeGrid(itemStack.ItemConfig.BackpackSize))
            {
                slot.ItemStack = null;
                return itemStack;
            }

            if (!HandleSlotSideEffects(slot))
            {
                slot.ItemStack = null;
                return itemStack;
            }

            return itemStack.Count > countToPlace
                ? new ItemStack(itemStack.ItemConfig, itemStack.Count - countToPlace, itemStack.IsRotated)
                : null;
        }

        return itemStack;
    }

    private bool CanAcceptInSlot(SnapshotSlot slot, ItemStack itemStack)
    {
        return slot != null
               && itemStack?.ItemConfig != null
               && slot.ItemType == itemStack.ItemConfig.ItemType
               && !(slot.ItemType == ItemType.Face && IsFaceSlotBlocked());
    }

    private bool HandleSlotSideEffects(SnapshotSlot slot)
    {
        return slot?.ItemType != ItemType.Helm || ResolveBlockedFaceSlotOverflow();
    }

    private bool ResolveBlockedFaceSlotOverflow()
    {
        if (!IsFaceSlotBlocked())
        {
            return true;
        }

        var faceSlot = slots.FirstOrDefault(slot => slot.ItemType == ItemType.Face);
        if (faceSlot?.ItemStack?.ItemConfig == null)
        {
            return true;
        }

        var previousItems = items
            .Select(item => new SnapshotItem(item.ItemStack, item.OccupiedCells))
            .ToList();
        var blockedFaceItem = faceSlot.ItemStack.Clone();
        faceSlot.ItemStack = null;

        if (TryAdd(blockedFaceItem) == null)
        {
            return true;
        }

        faceSlot.ItemStack = blockedFaceItem;
        items.Clear();
        items.AddRange(previousItems);
        return false;
    }

    private bool IsFaceSlotBlocked()
    {
        var helmSlot = slots.FirstOrDefault(slot => slot.ItemType == ItemType.Helm);
        return helmSlot?.ItemStack?.ItemConfig != null && helmSlot.ItemStack.ItemConfig.BlocksFaceSlot;
    }

    private bool TryResizeGrid(Vector2Int newGridSize)
    {
        if (newGridSize == gridSize)
        {
            return true;
        }

        var previousSize = gridSize;
        var previousItems = items
            .Select(item => new SnapshotItem(item.ItemStack, item.OccupiedCells))
            .ToList();

        gridSize = newGridSize;
        items.Clear();

        foreach (var item in previousItems)
        {
            if (TryAddToGrid(item.ItemStack) == null)
            {
                continue;
            }

            gridSize = previousSize;
            items.Clear();
            items.AddRange(previousItems);
            return false;
        }

        return true;
    }

    private ItemStack TryAddToGrid(ItemStack itemStack)
    {
        var remainingStack = CloneIfValid(itemStack);
        while (remainingStack != null && remainingStack.Count > 0 && TryFindFreeCells(remainingStack.Size, out var occupiedCells))
        {
            var countToPlace = Mathf.Min(remainingStack.Count, remainingStack.MaxStack);
            items.Add(new SnapshotItem(new ItemStack(remainingStack.ItemConfig, countToPlace, remainingStack.IsRotated), occupiedCells));
            remainingStack.Count -= countToPlace;
        }

        return remainingStack != null && remainingStack.Count > 0 ? remainingStack : null;
    }

    private bool TryFindFreeCells(Vector2Int size, out List<Vector2Int> occupiedCells)
    {
        for (var y = 0; y <= gridSize.y - size.y; y++)
        for (var x = 0; x <= gridSize.x - size.x; x++)
        {
            var candidateCells = new List<Vector2Int>(size.x * size.y);
            var isFree = true;

            for (var currentY = 0; currentY < size.y && isFree; currentY++)
            for (var currentX = 0; currentX < size.x; currentX++)
            {
                var cell = new Vector2Int(x + currentX, y + currentY);
                if (IsOccupied(cell))
                {
                    isFree = false;
                    break;
                }

                candidateCells.Add(cell);
            }

            if (!isFree)
            {
                continue;
            }

            occupiedCells = candidateCells;
            return true;
        }

        occupiedCells = null;
        return false;
    }

    private bool IsOccupied(Vector2Int cell)
    {
        foreach (var item in items)
        {
            if (item.OccupiedCells.Contains(cell))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<SnapshotItem> CreateSnapshotItems(IEnumerable<ItemInInventory> sourceItems)
    {
        foreach (var item in sourceItems)
        {
            if (item?.ItemStack?.ItemConfig == null)
            {
                continue;
            }

            yield return new SnapshotItem(item.ItemStack, GetOccupiedCells(item));
        }
    }

    private static IEnumerable<Vector2Int> GetOccupiedCells(ItemInInventory item)
    {
        if (item?.Tiles != null && item.Tiles.Count > 0)
        {
            return item.Tiles.Select(tile => tile.Index);
        }

        var center = item?.Position.GetColumn(3) ?? Vector4.zero;
        var itemSize = item?.ItemStack?.Size ?? Vector2Int.one;
        var startX = Mathf.RoundToInt(center.x - (itemSize.x - 1) * 0.5f);
        var startY = Mathf.RoundToInt(center.y - (itemSize.y - 1) * 0.5f);
        var occupiedCells = new List<Vector2Int>(itemSize.x * itemSize.y);

        for (var y = 0; y < itemSize.y; y++)
        for (var x = 0; x < itemSize.x; x++)
        {
            occupiedCells.Add(new Vector2Int(startX + x, startY + y));
        }

        return occupiedCells;
    }

    private static ItemStack CloneIfValid(ItemStack itemStack)
    {
        return itemStack?.ItemConfig == null || itemStack.Count <= 0 ? null : itemStack.Clone();
    }
}
}
