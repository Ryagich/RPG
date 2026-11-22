using Inventory.Item;
using MessagePipe;
using Messages;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Owns the single active damage window of an equipped weapon visual.
    /// </summary>
    public sealed class EquippedWeaponDamageWindowController
    {
        private readonly IPublisher<WeaponSwingMessage> swingPublisher;
        private readonly IPublisher<PhysicalContactMessage> contactPublisher;
        private WeaponDamageZone activeDamageZone;

        public EquippedWeaponDamageWindowController(
            IPublisher<WeaponSwingMessage> swingPublisher,
            IPublisher<PhysicalContactMessage> contactPublisher)
        {
            this.swingPublisher = swingPublisher;
            this.contactPublisher = contactPublisher;
        }

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
            if (activeDamageZone.BeginDamageWindow(ownerDamageReceiver, weaponConfig, contactPublisher, isHeavyAttack))
            {
                swingPublisher.Publish(new WeaponSwingMessage(weaponConfig, damageZone.transform.position));
            }
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
