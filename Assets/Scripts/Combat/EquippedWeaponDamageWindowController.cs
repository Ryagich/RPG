using Inventory.Item;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Owns the single active damage window of an equipped weapon visual.
    /// </summary>
    public sealed class EquippedWeaponDamageWindowController
    {
        private WeaponDamageZone activeDamageZone;

        public void Begin(
            GameObject weaponVisual,
            ItemConfig weaponConfig,
            CharacterDamageReceiver ownerDamageReceiver,
            bool isHeavyAttack)
        {
            if (activeDamageZone != null || weaponVisual == null || weaponConfig == null)
            {
                return;
            }

            var weapon = weaponVisual.GetComponentInChildren<Weapon>(true);
            var damageZone = weapon != null ? weapon.DamageZone : null;
            if (damageZone == null)
            {
                return;
            }

            activeDamageZone = damageZone;
            activeDamageZone.BeginDamageWindow(ownerDamageReceiver, weaponConfig, isHeavyAttack);
        }

        public void End()
        {
            if (activeDamageZone == null)
            {
                return;
            }

            activeDamageZone.EndDamageWindow();
            activeDamageZone = null;
        }
    }
}
