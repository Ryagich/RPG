using Inventory.Item;
using UnityEngine;

namespace Messages
{
    public readonly struct WeaponSwingMessage
    {
        public readonly ItemConfig Weapon;
        public readonly Vector3 Position;

        public WeaponSwingMessage(ItemConfig weapon, Vector3 position)
        {
            Weapon = weapon;
            Position = position;
        }
    }

    public readonly struct PhysicalContactMessage
    {
        public readonly ItemConfig Weapon;
        public readonly ItemConfig Armor;
        public readonly Vector3 Point;

        public PhysicalContactMessage(ItemConfig weapon, ItemConfig armor, Vector3 point)
        {
            Weapon = weapon;
            Armor = armor;
            Point = point;
        }
    }
}
