using Inventory.Slot;
using UnityEngine;

namespace Inventory
{
    public interface IEquippedWeaponVisual
    {
        bool TryGetCurrentWeaponSlot(out SlotModel slot);
        bool TryGetCurrentWeaponPose(out Vector3 position, out Quaternion rotation);
    }
}
