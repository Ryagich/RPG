using Combat;
using Container;
using Factions;
using NPC;
using TargetLock;
using UnityEngine;
using VContainer.Unity;

namespace Monsters
{
    /// <summary>
    /// Keeps PlantMonster_1's current hostile target. Target selection follows the same
    /// registry, visibility, life-state, and faction rules used by NPC combat bots, without
    /// inheriting their weapon, inventory, or tactical-decision responsibilities.
    /// </summary>
    public sealed class PlantMonsterTargetingController : IStartable, ITickable
    {
        private const float ScanInterval = 0.25f;

        private readonly Transform ownerTransform;
        private readonly PlantMonsterLifetimeScope ownerScope;
        private readonly NpcVision vision;
        private readonly CharacterVisionSensor visionSensor;
        private readonly PlantMonsterCombatVisionController combatVision;
        private readonly IFactionRelations factionRelations;
        private float nextScanTime;

        public PlantMonsterTargetingController(
            Transform ownerTransform,
            NpcVision vision,
            CharacterVisionSensor visionSensor,
            PlantMonsterCombatVisionController combatVision,
            IFactionRelations factionRelations)
        {
            this.ownerTransform = ownerTransform;
            ownerScope = ownerTransform != null ? ownerTransform.GetComponent<PlantMonsterLifetimeScope>() : null;
            this.vision = vision;
            this.visionSensor = visionSensor;
            this.combatVision = combatVision;
            this.factionRelations = factionRelations;
        }

        public TargetLockTarget CurrentTarget { get; private set; }
        public bool HasTarget => IsValidTarget(CurrentTarget);
        public bool IsTargetVisible => HasTarget
                                       && ((vision != null && vision.IsInView(CurrentTarget))
                                           || (combatVision != null && combatVision.IsInView(CurrentTarget)));
        public bool IsTargetInAttackView => HasTarget && vision != null && vision.IsInAttackView(CurrentTarget);
        public float AttackViewDistance => vision != null ? vision.AttackViewDistance : 0f;
        public Vector3 LastKnownTargetPosition { get; private set; }
        public bool HasLastKnownTargetPosition { get; private set; }

        public void Start() => ScanForEnemy(force: true);

        public void Tick()
        {
            if (IsTargetVisible)
            {
                RememberCurrentTargetPosition();
            }
            else if (HasTarget)
            {
                LoseCurrentTarget();
            }

            ScanForEnemy();
        }

        public bool ScanForEnemy(bool force = false)
        {
            if (!force && Time.time < nextScanTime)
            {
                return HasTarget;
            }

            nextScanTime = Time.time + ScanInterval;
            var target = FindBestVisibleEnemy();
            if (target == null)
            {
                return HasTarget;
            }

            CurrentTarget = target;
            RememberCurrentTargetPosition();
            return true;
        }

        public void ClearTarget()
        {
            CurrentTarget = null;
            ClearLastKnownTargetPosition();
        }

        public void ClearLastKnownTargetPosition()
        {
            HasLastKnownTargetPosition = false;
            LastKnownTargetPosition = default;
        }

        private void LoseCurrentTarget()
        {
            CurrentTarget = null;
        }

        private void RememberCurrentTargetPosition()
        {
            if (CurrentTarget == null)
            {
                return;
            }

            LastKnownTargetPosition = CurrentTarget.transform.position;
            HasLastKnownTargetPosition = true;
        }

        private TargetLockTarget FindBestVisibleEnemy()
        {
            if (ownerTransform == null || vision == null || visionSensor == null)
            {
                return null;
            }

            TargetLockTarget bestTarget = null;
            var bestDistanceSqr = float.PositiveInfinity;
            visionSensor.PruneInvalidCandidates();
            foreach (var candidate in visionSensor.TargetCandidates)
            {
                ConsiderCandidate(candidate, ref bestTarget, ref bestDistanceSqr);
            }

            if (combatVision?.Sensor != null && combatVision.IsEnabled)
            {
                combatVision.Sensor.PruneInvalidCandidates();
                foreach (var candidate in combatVision.Sensor.TargetCandidates)
                {
                    ConsiderCandidate(candidate, ref bestTarget, ref bestDistanceSqr);
                }
            }

            return bestTarget;
        }

        private void ConsiderCandidate(
            TargetLockTarget candidate,
            ref TargetLockTarget bestTarget,
            ref float bestDistanceSqr)
        {
            if (!IsValidTarget(candidate) || candidate.transform == ownerTransform)
            {
                return;
            }

            var distanceSqr = (candidate.transform.position - ownerTransform.position).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr || !IsEnemy(candidate))
            {
                return;
            }

            var isVisibleByPrimaryVision = vision != null && vision.IsInView(candidate);
            var isVisibleByCombatVision = combatVision != null && combatVision.IsInView(candidate);
            if (!isVisibleByPrimaryVision && !isVisibleByCombatVision)
            {
                return;
            }

            bestTarget = candidate;
            bestDistanceSqr = distanceSqr;
        }

        private bool IsValidTarget(TargetLockTarget target)
        {
            if (target == null || !target.IsTargetable)
            {
                return false;
            }

            var receiver = target.GetComponentInParent<DamageReceiverHost>()?.Receiver;
            return receiver != null && receiver.IsAlive;
        }

        private bool IsEnemy(TargetLockTarget target)
        {
            var targetFaction = target.GetComponentInParent<NpcLifetimeScope>()?.Faction
                                ?? target.GetComponentInParent<PlayerLifetimeScope>()?.Faction;
            if (target.GetComponentInParent<PlayerLifetimeScope>() != null && targetFaction == null)
            {
                return false;
            }

            var ownerFaction = ownerScope != null ? ownerScope.Faction : null;
            if (ownerFaction == null || targetFaction == null)
            {
                return ownerScope != null && ownerScope.TreatFactionlessTargetsAsHostile;
            }

            return factionRelations != null && factionRelations.IsHostile(ownerFaction, targetFaction);
        }
    }

}
