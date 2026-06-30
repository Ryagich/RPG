using Inventory.Item;
using UnityEngine;

namespace Combat
{
    public readonly struct WeaponHit
    {
        public readonly CharacterDamageReceiver Attacker;
        public readonly ItemConfig WeaponConfig;
        public readonly float Damage;
        public readonly Vector3 Point;
        public readonly Collider HitCollider;

        public WeaponHit(
            CharacterDamageReceiver attacker,
            ItemConfig weaponConfig,
            float damage,
            Vector3 point,
            Collider hitCollider)
        {
            Attacker = attacker;
            WeaponConfig = weaponConfig;
            Damage = damage;
            Point = point;
            HitCollider = hitCollider;
        }
    }
}
