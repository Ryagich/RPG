using System.Collections.Generic;
using Combat;
using UnityEngine;

namespace Monsters
{
    /// <summary>
    /// Physical contact zone for a plant-monster attack. The combat controller owns when the
    /// window is open; this component only translates trigger contacts into one hit per victim.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlantMonsterDamageZone : MonoBehaviour
    {
        private readonly HashSet<CharacterDamageReceiver> hitReceivers = new();
        private Collider zoneCollider;
        private float damage;
        private bool isDamageWindowOpen;

        public void Configure(Collider collider)
        {
            zoneCollider = collider != null ? collider : GetComponent<Collider>();
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
                zoneCollider.enabled = false;
            }
        }

        public void BeginDamageWindow(float attackDamage)
        {
            if (zoneCollider == null)
            {
                Configure(GetComponent<Collider>());
            }

            damage = Mathf.Max(0f, attackDamage);
            hitReceivers.Clear();
            isDamageWindowOpen = damage > 0f && zoneCollider != null;
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
                zoneCollider.enabled = isDamageWindowOpen;
            }
        }

        public void EndDamageWindow()
        {
            isDamageWindowOpen = false;
            damage = 0f;
            hitReceivers.Clear();
            if (zoneCollider != null)
            {
                zoneCollider.enabled = false;
            }
        }

        private void Awake()
        {
            if (zoneCollider == null)
            {
                Configure(GetComponent<Collider>());
            }
        }

        private void OnDisable() => EndDamageWindow();

        private void OnTriggerEnter(Collider other)
        {
            if (!isDamageWindowOpen || other == null)
            {
                return;
            }

            var hitbox = other.GetComponentInParent<BodyHitbox>();
            if (hitbox == null || !hitbox.TryGetReceiver(out var receiver) || !hitReceivers.Add(receiver))
            {
                return;
            }

            receiver.ReceiveDirectHit(hitbox, damage, other.ClosestPoint(transform.position));
        }
    }
}
