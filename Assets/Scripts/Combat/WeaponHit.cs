using Inventory.Item;
using UnityEngine;

namespace Combat
{
    public readonly struct WeaponHit
    {
        public readonly CharacterDamageReceiver Attacker;
        public readonly CharacterDamageReceiver IntendedTarget;
        public readonly bool IntendedTargetWasHostile;
        public readonly ItemConfig WeaponConfig;
        public readonly float Damage;
        public readonly Vector3 Point;
        public readonly Collider HitCollider;

        public WeaponHit(
            CharacterDamageReceiver attacker,
            CharacterDamageReceiver intendedTarget,
            bool intendedTargetWasHostile,
            ItemConfig weaponConfig,
            float damage,
            Vector3 point,
            Collider hitCollider)
        {
            Attacker = attacker;
            IntendedTarget = intendedTarget;
            IntendedTargetWasHostile = intendedTargetWasHostile;
            WeaponConfig = weaponConfig;
            Damage = damage;
            Point = point;
            HitCollider = hitCollider;
        }
    }
}
