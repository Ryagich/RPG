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
        private readonly IEquipmentInventory inventory;
        private readonly INonLethalCombatSessionRegistry nonLethalCombatSessions;
        private readonly IPublisher<CharacterDamagedMessage> damagedPublisher;
        private bool isWeaponDamageBlocked;
        private bool isWeaponAttackSuppressed;
        private CharacterDamageReceiver weaponAttackIntentTarget;
        private bool weaponAttackIntentTargetsHostile;

        public CharacterDamageReceiver(
            Transform ownerTransform,
            StatsController statsController,
            IEquipmentInventory inventory,
            INonLethalCombatSessionRegistry nonLethalCombatSessions,
            IPublisher<CharacterDamagedMessage> damagedPublisher)
        {
            this.ownerTransform = ownerTransform;
            this.statsController = statsController;
            this.inventory = inventory;
            this.nonLethalCombatSessions = nonLethalCombatSessions;
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

        /// <summary>
        /// Suppresses outgoing hits made by this character's weapon. This is intentionally
        /// independent from <see cref="SetWeaponDamageBlocked"/>, which protects this receiver
        /// from incoming weapon damage during invulnerability frames.
        /// </summary>
        public void SetWeaponAttackSuppressed(bool isSuppressed)
        {
            isWeaponAttackSuppressed = isSuppressed;
        }

        public bool IsWeaponAttackSuppressed => isWeaponAttackSuppressed;

        /// <summary>
        /// Records the intended target for the next weapon damage window. The intent is consumed
        /// by <see cref="WeaponDamageZone"/> when the animation actually opens that window.
        /// It deliberately survives an AI-state transition: the animator can open its damage
        /// window after the state machine has already left the attack state.
        /// </summary>
        public void SetWeaponAttackIntent(CharacterDamageReceiver intendedTarget, bool targetsHostile)
        {
            weaponAttackIntentTarget = intendedTarget;
            // Hostility is a fact captured when the attack was accepted. Do not derive it from
            // the Unity object later: the intended enemy may die or be destroyed before this
            // swing contacts an ally.
            weaponAttackIntentTargetsHostile = targetsHostile;
        }

        public void ClearWeaponAttackIntent()
        {
            weaponAttackIntentTarget = null;
            weaponAttackIntentTargetsHostile = false;
        }

        public void ConsumeWeaponAttackIntent(
            out CharacterDamageReceiver intendedTarget,
            out bool targetsHostile)
        {
            intendedTarget = weaponAttackIntentTarget;
            targetsHostile = weaponAttackIntentTargetsHostile;
            ClearWeaponAttackIntent();
        }

        public void ReceiveHit(BodyHitbox hitbox, in WeaponHit hit)
        {
            if (isWeaponDamageBlocked || hitbox == null || hit.Damage <= 0f)
            {
                return;
            }

            var bodyMultiplier = Mathf.Max(0f, hitbox.DamageMultiplier);
            var physicalDefense = PhysicalDefenseCalculator.ResolveProtection(inventory, hitbox.BodyPart);
            var mitigatedDamage = hit.Damage * bodyMultiplier * (1f - physicalDefense);
            mitigatedDamage = nonLethalCombatSessions.ClampIncomingWeaponDamage(this, mitigatedDamage);

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
                    hit.IntendedTarget,
                    hit.IntendedTargetWasHostile,
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
