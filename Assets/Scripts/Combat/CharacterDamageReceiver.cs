using Inventory.Inventories;
using Inventory.Item;
using MessagePipe;
using Messages;
using Stats;
using UnityEngine;

namespace Combat
{
    public sealed class CharacterDamageReceiver
    {
        private readonly Transform ownerTransform;
        private readonly StatsController statsController;
        private readonly PlayerInventory playerInventory;
        private readonly IPublisher<CharacterDamagedMessage> damagedPublisher;
        private bool isWeaponDamageBlocked;

        public CharacterDamageReceiver(
            Transform ownerTransform,
            StatsController statsController,
            PlayerInventory playerInventory,
            IPublisher<CharacterDamagedMessage> damagedPublisher)
        {
            this.ownerTransform = ownerTransform;
            this.statsController = statsController;
            this.playerInventory = playerInventory;
            this.damagedPublisher = damagedPublisher;
        }

        public float CurrentHp => statsController.Hp.Value.Value;
        public bool IsAlive => CurrentHp > 0f;
        public Transform OwnerTransform => ownerTransform;

        /// <summary>
        /// Blocks only hits delivered through <see cref="WeaponHit"/>. Environmental and periodic
        /// damage modify stats directly and therefore remain unaffected.
        /// </summary>
        public void SetWeaponDamageBlocked(bool isBlocked)
        {
            isWeaponDamageBlocked = isBlocked;
        }

        public void ReceiveHit(BodyHitbox hitbox, in WeaponHit hit)
        {
            if (isWeaponDamageBlocked || hitbox == null || hit.Damage <= 0f)
            {
                return;
            }

            var bodyMultiplier = Mathf.Max(0f, hitbox.DamageMultiplier);
            var physicalDefense = PhysicalDefenseCalculator.ResolveProtection(playerInventory, hitbox.BodyPart);
            var mitigatedDamage = hit.Damage * bodyMultiplier * (1f - physicalDefense);

            if (mitigatedDamage <= 0f)
            {
                return;
            }

            var hp = statsController.Hp;
            var previousHp = hp.Value.Value;
            statsController.AddValue(StatType.Hp, -mitigatedDamage);
            var finalDamage = Mathf.Max(0f, previousHp - hp.Value.Value);
            if (finalDamage <= 0f)
            {
                return;
            }

            damagedPublisher.Publish(
                new CharacterDamagedMessage(
                    ownerTransform.gameObject,
                    ownerTransform,
                    hit.Attacker,
                    hit.WeaponConfig,
                    hitbox.BodyPart,
                    hit.Damage,
                    bodyMultiplier,
                    physicalDefense,
                    finalDamage,
                    hit.Point));
        }
    }
}
