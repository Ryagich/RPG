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

        public void ReceiveHit(BodyHitbox hitbox, in WeaponHit hit)
        {
            if (hitbox == null || hit.Damage <= 0f)
            {
                return;
            }

            var bodyMultiplier = Mathf.Max(0f, hitbox.DamageMultiplier);
            var physicalDefense = PhysicalDefenseCalculator.ResolveProtection(playerInventory, hitbox.BodyPart);
            var finalDamage = hit.Damage * bodyMultiplier * (1f - physicalDefense);

            if (finalDamage <= 0f)
            {
                return;
            }

            statsController.AddValue(StatType.Hp, -finalDamage);
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
