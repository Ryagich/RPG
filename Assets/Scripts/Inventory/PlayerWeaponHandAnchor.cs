using UnityEngine;

namespace Inventory
{
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponHandAnchor : MonoBehaviour
    {
        [field: SerializeField] public Transform RightHand { get; private set; }
        [field: SerializeField] public Transform Belt { get; private set; }
    }
}
