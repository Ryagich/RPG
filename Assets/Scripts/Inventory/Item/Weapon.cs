using Combat;
using UnityEngine;

namespace Inventory.Item
{
    public sealed class Weapon : MonoBehaviour
    {
        [field: SerializeField] public WeaponDamageZone DamageZone { get; private set; }
    }
}
