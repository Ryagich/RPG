using System.Collections.Generic;
using TargetLock;
using UnityEngine;

namespace NPC
{
    /// <summary>Collects nearby targetable characters for combat and character-aware behaviours.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterVisionSensor : VisionSensorBase
    {
        private readonly HashSet<TargetLockTarget> targetCandidates = new();

        public IEnumerable<TargetLockTarget> TargetCandidates => targetCandidates;

        protected override void OnVisionTriggerEnter(Collider other)
        {
            var target = other != null ? other.GetComponentInParent<TargetLockTarget>() : null;
            if (target != null && target.gameObject != gameObject)
            {
                targetCandidates.Add(target);
            }
        }

        protected override void OnVisionTriggerExit(Collider other)
        {
            var target = other != null ? other.GetComponentInParent<TargetLockTarget>() : null;
            if (target != null)
            {
                targetCandidates.Remove(target);
            }
        }

        public void PruneInvalidCandidates() =>
            targetCandidates.RemoveWhere(target => target == null || !target.IsTargetable);
    }
}
