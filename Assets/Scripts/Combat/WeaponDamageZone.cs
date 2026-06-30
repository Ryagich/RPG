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
        private CharacterDamageReceiver attacker;
        private ItemConfig weaponConfig;
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

        public void BeginDamageWindow(CharacterDamageReceiver currentAttacker, ItemConfig currentWeaponConfig)
        {
            attacker = currentAttacker;
            weaponConfig = currentWeaponConfig;
            hitReceivers.Clear();
            isDamageWindowOpen = weaponConfig != null;
        }

        public void EndDamageWindow()
        {
            isDamageWindowOpen = false;
            attacker = null;
            weaponConfig = null;
            hitReceivers.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isDamageWindowOpen || other == null)
            {
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

            var damage = weaponConfig.GetRandomWeaponDamage();
            var hit = new WeaponHit(
                attacker,
                weaponConfig,
                damage,
                other.ClosestPoint(transform.position),
                other);

            hitbox.TryReceiveHit(hit);
        }
    }
}
