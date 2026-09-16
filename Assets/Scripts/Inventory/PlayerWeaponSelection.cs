using Inventory.Item;

namespace Inventory
{
    /// <summary>
    /// A snapshot of the weapon currently selected by the player. The inventory remains the
    /// source of truth; this value is only used while reconciling the presentation.
    /// </summary>
    internal readonly struct PlayerWeaponSelection
    {
        public PlayerWeaponSelection(int slotIndex, ItemConfig itemConfig)
        {
            SlotIndex = slotIndex;
            ItemConfig = itemConfig;
        }

        public int SlotIndex { get; }
        public ItemConfig ItemConfig { get; }
        public bool HasWeapon => ItemConfig != null;

        public bool Matches(int slotIndex, ItemConfig itemConfig)
        {
            return HasWeapon && SlotIndex == slotIndex && ItemConfig == itemConfig;
        }
    }
}
