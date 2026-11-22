using System.Collections.Generic;
using Inventory.Item;
using MessagePipe;
using Messages;
using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WeaponDamageZone : MonoBehaviour
    {
        [SerializeField] private Collider zoneCollider;

        private readonly HashSet<CharacterDamageReceiver> hitReceivers = new();
        private readonly HashSet<WeaponHitReceiver> hitObjectReceivers = new();
        private CharacterDamageReceiver attacker;
        private CharacterDamageReceiver intendedTarget;
        private ItemConfig weaponConfig;
        private IPublisher<PhysicalContactMessage> contactPublisher;
        private bool isHeavyAttack;
        private bool isDamageWindowOpen;
        private bool intendedTargetWasHostile;

        private void Awake()
        {
            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<Collider>();
            }

            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }
        }

        private void OnDisable()
        {
            EndDamageWindow();
        }

        public bool BeginDamageWindow(
            CharacterDamageReceiver currentAttacker,
            ItemConfig currentWeaponConfig,
            IPublisher<PhysicalContactMessage> currentContactPublisher,
            bool useHeavyAttackDamage = false)
        {
            attacker = currentAttacker;
            weaponConfig = currentWeaponConfig;
            contactPublisher = currentContactPublisher;
            isHeavyAttack = useHeavyAttackDamage;
            intendedTarget = null;
            intendedTargetWasHostile = false;
            attacker?.ConsumeWeaponAttackIntent(out intendedTarget, out intendedTargetWasHostile);
            hitReceivers.Clear();
            hitObjectReceivers.Clear();
            isDamageWindowOpen = attacker != null
                                 && weaponConfig != null
                                 && !attacker.IsWeaponAttackSuppressed;
            return isDamageWindowOpen;
        }

        public void EndDamageWindow()
        {
            isDamageWindowOpen = false;
            attacker = null;
            intendedTarget = null;
            weaponConfig = null;
            contactPublisher = null;
            isHeavyAttack = false;
            intendedTargetWasHostile = false;
            hitReceivers.Clear();
            hitObjectReceivers.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isDamageWindowOpen
             || attacker == null
             || attacker.IsWeaponAttackSuppressed
             || other == null)
            {
                return;
            }

            var damage = weaponConfig.GetRandomWeaponDamage(isHeavyAttack);
            var hit = new WeaponHit(
                attacker,
                intendedTarget,
                intendedTargetWasHostile,
                weaponConfig,
                damage,
                other.ClosestPoint(transform.position),
                other);

            var objectReceiver = other.GetComponentInParent<WeaponHitReceiver>();
            if (objectReceiver != null)
            {
                if (hitObjectReceivers.Add(objectReceiver))
                {
                    var publisher = contactPublisher;
                    objectReceiver.ReceiveHit(hit);
                    publisher.Publish(new PhysicalContactMessage(hit.WeaponConfig, null, hit.Point));
                }

                return;
            }

            var hitbox = other.GetComponentInParent<BodyHitbox>();
            if (hitbox == null || !hitbox.TryGetReceiver(out var receiver))
            {
                return;
            }

            if (receiver == attacker || !hitReceivers.Add(receiver))
            {
                return;
            }

            hitbox.TryReceiveHit(hit);
        }
    }
}
