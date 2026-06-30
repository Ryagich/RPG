using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    public sealed class BodyHitbox : MonoBehaviour
    {
        [SerializeField] private DamageBodyPart bodyPart = DamageBodyPart.None;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;

        private DamageReceiverHost receiverHost;

        public DamageBodyPart BodyPart => bodyPart;
        public float DamageMultiplier => damageMultiplier;

        public void ConfigureDefaults(DamageBodyPart defaultBodyPart, bool applyDefaultDamageMultiplier)
        {
            if (bodyPart == DamageBodyPart.None)
            {
                bodyPart = defaultBodyPart;
            }

            if (applyDefaultDamageMultiplier)
            {
                damageMultiplier = DamageBodyPartUtility.GetDefaultDamageMultiplier(bodyPart);
            }
        }

        private void Awake()
        {
            CacheReceiverHost();
        }

        public bool TryGetReceiver(out CharacterDamageReceiver receiver)
        {
            CacheReceiverHost();
            receiver = receiverHost != null ? receiverHost.Receiver : null;
            return receiver != null;
        }

        public bool TryReceiveHit(in WeaponHit hit)
        {
            if (!TryGetReceiver(out var receiver))
            {
                return false;
            }

            receiver.ReceiveHit(this, hit);
            return true;
        }

        private void CacheReceiverHost()
        {
            if (receiverHost != null)
            {
                return;
            }

            receiverHost = GetComponentInParent<DamageReceiverHost>(true);
        }
    }
}
