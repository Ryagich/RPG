using UnityEngine;

namespace Inventory
{
    /// <summary>
    /// Marks a runtime weapon visual created by <see cref="PlayerWeaponVisualController"/>.
    /// It gives the controller an explicit ownership boundary for orphan cleanup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponVisualInstance : MonoBehaviour
    {
    }
}
