using System.Collections.Generic;
using Inventory.Item;
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
        private ItemConfig weaponConfig;
        private bool isHeavyAttack;
        private bool isDamageWindowOpen;

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

        public void BeginDamageWindow(
            CharacterDamageReceiver currentAttacker,
            ItemConfig currentWeaponConfig,
            bool useHeavyAttackDamage = false)
        {
            attacker = currentAttacker;
            weaponConfig = currentWeaponConfig;
            isHeavyAttack = useHeavyAttackDamage;
            hitReceivers.Clear();
            hitObjectReceivers.Clear();
            isDamageWindowOpen = weaponConfig != null;
        }

        public void EndDamageWindow()
        {
            isDamageWindowOpen = false;
            attacker = null;
            weaponConfig = null;
            isHeavyAttack = false;
            hitReceivers.Clear();
            hitObjectReceivers.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isDamageWindowOpen || other == null)
            {
                return;
            }

            var damage = weaponConfig.GetRandomWeaponDamage(isHeavyAttack);
            var hit = new WeaponHit(
                attacker,
                weaponConfig,
                damage,
                other.ClosestPoint(transform.position),
                other);

            var objectReceiver = other.GetComponentInParent<WeaponHitReceiver>();
            if (objectReceiver != null)
            {
                if (hitObjectReceivers.Add(objectReceiver))
                {
                    objectReceiver.ReceiveHit(hit);
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
