using System.Collections.Generic;
using NPC;
using TargetLock;
using UnityEngine;

namespace Monsters
{
    /// <summary>Collects targets inside PlantMonster_1's short-range peripheral vision volume.</summary>
    [DisallowMultipleComponent]
    public sealed class PlantMonsterCombatVisionSensor : VisionSensorBase
    {
        private readonly HashSet<TargetLockTarget> targetCandidates = new();

        public IEnumerable<TargetLockTarget> TargetCandidates => targetCandidates;

        public void ClearCandidates() => targetCandidates.Clear();

        protected override void OnVisionTriggerEnter(Collider other)
        {
            var target = other != null ? other.GetComponentInParent<TargetLockTarget>() : null;
            if (target != null && target.transform.root != transform.root)
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
