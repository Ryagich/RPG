using Combat;
using Factions;
using NPC;
using UnityEngine;

namespace TargetLock
{
    public sealed class TargetLockTargetFilter
    {
        private readonly Transform ownerTransform;
        private readonly TargetLockTarget ownerTarget;
        private readonly DamageReceiverHost ownerDamageReceiverHost;
        private readonly INpcCombatRegistry combatRegistry;
        private readonly IFactionRelations factionRelations;

        public TargetLockTargetFilter(
            Transform ownerTransform,
            TargetLockTarget ownerTarget,
            DamageReceiverHost ownerDamageReceiverHost,
            INpcCombatRegistry combatRegistry,
            IFactionRelations factionRelations)
        {
            this.ownerTransform = ownerTransform;
            this.ownerTarget = ownerTarget;
            this.ownerDamageReceiverHost = ownerDamageReceiverHost;
            this.combatRegistry = combatRegistry;
            this.factionRelations = factionRelations;
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
            if (ownerReceiver != null && combatRegistry.IsTargetHostileToReceiver(target, ownerReceiver))
            {
                return true;
            }

            var ownerFaction = ownerTarget != null ? ownerTarget.Faction : null;
            var targetFaction = target != null ? target.Faction : null;
            return ownerFaction != null
                   && targetFaction != null
                   && factionRelations != null
                   && factionRelations.IsHostile(ownerFaction, targetFaction);
        }

    }
}
