using Combat;
using NPC;
using UnityEngine;

namespace TargetLock
{
    public sealed class TargetLockTargetFilter
    {
        private readonly Transform ownerTransform;
        private readonly DamageReceiverHost ownerDamageReceiverHost;
        private readonly INpcCombatRegistry combatRegistry;

        public TargetLockTargetFilter(
            Transform ownerTransform,
            DamageReceiverHost ownerDamageReceiverHost,
            INpcCombatRegistry combatRegistry)
        {
            this.ownerTransform = ownerTransform;
            this.ownerDamageReceiverHost = ownerDamageReceiverHost;
            this.combatRegistry = combatRegistry;
        }

        public bool CanLock(TargetLockTarget target)
        {
            return IsValidTargetObject(target)
                && IsAlive(target)
                && IsHostile(target);
        }

        public bool IsValidTargetObject(TargetLockTarget target)
        {
            if (target == null || !target.IsTargetable || ownerTransform == null)
            {
                return false;
            }

            if (target.transform == ownerTransform || target.transform.IsChildOf(ownerTransform))
            {
                return false;
            }

            var targetReceiver = target.GetComponentInParent<DamageReceiverHost>()?.Receiver;
            return targetReceiver == null
                || ownerDamageReceiverHost == null
                || targetReceiver != ownerDamageReceiverHost.Receiver;
        }

        public bool IsAlive(TargetLockTarget target)
        {
            var receiver = target != null
                ? target.GetComponentInParent<DamageReceiverHost>()?.Receiver
                : null;
            return receiver != null && receiver.IsAlive;
        }

        private bool IsHostile(TargetLockTarget target)
        {
            var ownerReceiver = ownerDamageReceiverHost != null ? ownerDamageReceiverHost.Receiver : null;
            return ownerReceiver != null
                && combatRegistry.IsTargetHostileToReceiver(target, ownerReceiver);
        }
    }
}
