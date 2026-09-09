using Inventory.Item;

namespace Inventory
{
    /// <summary>
    /// A persistence-neutral description of one player inventory entry.
    /// Storage adapters translate this model to their own primitive-only format.
    /// </summary>
    public readonly struct InventoryItemPlacement
    {
        public InventoryItemPlacement(ItemConfig itemConfig, int count, int x, int y, bool isRotated, PlayerInventoryContainer container)
        {
            ItemConfig = itemConfig;
            Count = count;
            X = x;
            Y = y;
            IsRotated = isRotated;
            Container = container;
        }

        public ItemConfig ItemConfig { get; }
        public int Count { get; }
        public int X { get; }
        public int Y { get; }
        public bool IsRotated { get; }
        public PlayerInventoryContainer Container { get; }
    }

    public enum PlayerInventoryContainer
    {
        Grid = 0,
        Helm = 1,
        Face = 2,
        Body = 3,
        Hands = 4,
        Arms = 5,
        Legs = 6,
        Hips = 7,
        Backpack = 8,
        LeftWeapon = 9,
        RightWeapon = 10,
        FastSlot1 = 11,
        FastSlot2 = 12,
        FastSlot3 = 13,
        FastSlot4 = 14
    }
}
