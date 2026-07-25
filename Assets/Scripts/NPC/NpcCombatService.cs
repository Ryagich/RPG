using System;
using System.Collections.Generic;
using System.Linq;
using Combat;
using Container;
using Factions;
using Inventory.Inventories;
using Inventory.Item;
using MessagePipe;
using Messages;
using Player;
using Stats;
using TargetLock;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using VContainer.Unity;

namespace NPC
{
    public sealed class NpcCombatService : IStartable, ITickable, IDisposable
    {
        private static readonly List<NpcCombatService> ActiveServices = new();

        private readonly Transform ownerTransform;
        private readonly NpcVision vision;
        private readonly NpcCombatConfig combatConfig;
        private readonly FactionRelationsConfig factionRelationsConfig;
        private readonly PlayerInventory inventory;
        private readonly StatsController statsController;
        private readonly CharacterDamageReceiver ownerDamageReceiver;
        private readonly NpcWeaponInHandController weaponController;
        private readonly NpcTargetLockController targetLockController;
        private readonly ICharacterHitReactionController hitReactionController;
        private readonly ISubscriber<CharacterDamagedMessage> damagedSubscriber;
        private readonly CompositeDisposable disposables = new();
        private readonly HashSet<CharacterDamageReceiver> personalEnemies = new();

        private NpcLifetimeScope ownerScope;
        private float nextScanTime;
        private float aggressionNotificationTimer;
        private TargetLockTarget notificationTarget;
        private TargetLockTarget initialCircleEvaluatedTarget;
        private bool hasPendingAggressionNotification;
        private bool hasPendingDamageFleeRequest;
        private float recentDamageTaken;
        private float recentDamageDealt;
        private float spacingRetreatCooldown;
        private int consecutiveSpacingRetreats;
        private float approachBurstCooldown;
        private int consecutiveAttackDecisions;
        private TargetLockTarget motionObservedTarget;
        private Vector3 previousTargetPosition;
        private bool hasPreviousTargetPosition;
        private float targetRetreatSpeed;

        public NpcCombatService(
            Transform ownerTransform,
            NpcVision vision,
            NpcCombatConfig combatConfig,
            FactionRelationsConfig factionRelationsConfig,
            PlayerInventory inventory,
            StatsController statsController,
            CharacterDamageReceiver ownerDamageReceiver,
            NpcWeaponInHandController weaponController,
            NpcTargetLockController targetLockController,
            ICharacterHitReactionController hitReactionController,
            ISubscriber<CharacterDamagedMessage> damagedSubscriber)
        {
            this.ownerTransform = ownerTransform;
            this.vision = vision;
            this.combatConfig = combatConfig;
            this.factionRelationsConfig = factionRelationsConfig;
            this.inventory = inventory;
            this.statsController = statsController;
            this.ownerDamageReceiver = ownerDamageReceiver;
            this.weaponController = weaponController;
            this.targetLockController = targetLockController;
            this.hitReactionController = hitReactionController;
            this.damagedSubscriber = damagedSubscriber;
        }

        public TargetLockTarget CurrentTarget { get; private set; }
        public Vector3 LastKnownTargetPosition { get; private set; }
        public Vector3 FleeDestination { get; private set; }
        public Vector3 CombatMoveDestination { get; private set; }
        public bool HasCombatTarget => CurrentTarget != null && CurrentTarget.IsTargetable && IsTargetAlive(CurrentTarget);
        public bool HasLastKnownTargetPosition { get; private set; }
        public bool HasFleeDestination { get; private set; }
        public bool HasCombatMoveDestination { get; private set; }
        public bool ShouldSearchLastKnownTarget => !HasCombatTarget && HasLastKnownTargetPosition;
        public bool IsCurrentTargetDown => CurrentTarget != null && (!CurrentTarget.IsTargetable || !IsTargetAlive(CurrentTarget));
        public bool IsTargetVisible => HasCombatTarget && vision != null && vision.IsInView(CurrentTarget);
        public bool IsTargetInAttackView => HasCombatTarget && vision != null && vision.IsInAttackView(CurrentTarget);
        public bool IsTargetInAttackRange
        {
            get
            {
                if (!HasCombatTarget || vision == null || ownerTransform == null)
                {
                    return false;
                }

                var distance = vision.AttackViewDistance + (combatConfig != null ? combatConfig.AttackStartDistanceTolerance : 0.25f);
                return PlanarDistance(ownerTransform.position, CurrentTarget.AimPosition) <= distance;
            }
        }

        public bool CanStartAttack => HasCombatTarget && HasClearAttackLane() && (IsTargetInAttackView || IsTargetInAttackRange);
        public bool HasWeaponReady => weaponController != null && weaponController.HasWeaponInWeaponSlots;
        public bool HasAnyWeaponAvailable => HasWeaponReady
                                             || inventory?.Items.Any(item => item?.ItemStack?.ItemConfig?.ItemType == ItemType.Weapon) == true;
        public bool HasThreat => HasCombatTarget || HasLastKnownTargetPosition;
        public bool ShouldFlee => HasCombatTarget && IsTargetVisible && !HasAnyWeaponAvailable;
        public bool ShouldFleeFromDamageThreat => hasPendingDamageFleeRequest && HasThreat && !HasAnyWeaponAvailable;
        public bool IsTargetTooClose => HasCombatTarget && ownerTransform != null
            && PlanarDistance(ownerTransform.position, CurrentTarget.transform.position) <= GetPersonalSpaceDistance();
        public float StaminaNormalized => weaponController != null ? weaponController.StaminaNormalized : 0f;

        public static bool IsTargetHostileToReceiver(TargetLockTarget potentialHostile, CharacterDamageReceiver receiver)
        {
            if (potentialHostile == null || receiver == null)
            {
                return false;
            }

            foreach (var service in ActiveServices.ToArray())
            {
                if (service == null
                 || service.ownerTransform == null
                 || !service.IsOwnerOfTarget(potentialHostile))
                {
                    continue;
                }

                return service.IsHostileTo(receiver);
            }

            return false;
        }

        public bool IsHostileToReceiver(CharacterDamageReceiver receiver)
        {
            return IsHostileTo(receiver);
        }

        public void Start()
        {
            ownerScope = ownerTransform != null ? ownerTransform.GetComponent<NpcLifetimeScope>() : null;
            if (!ActiveServices.Contains(this))
            {
                ActiveServices.Add(this);
            }

            damagedSubscriber.Subscribe(OnCharacterDamaged).AddTo(disposables);
        }

        public void Tick()
        {
            TickAggressionNotification();
            TickTacticalMemory();
        }

        public void Dispose()
        {
            ActiveServices.Remove(this);
            disposables.Dispose();
        }

        public bool ScanForEnemy(bool force = false)
        {
            if (!force && Time.time < nextScanTime)
            {
                return HasCombatTarget;
            }

            nextScanTime = Time.time + Mathf.Max(0.05f, combatConfig != null ? combatConfig.EnemyScanInterval : 0.25f);
            var bestTarget = FindBestVisibleEnemy();
            if (bestTarget == null)
            {
                return HasCombatTarget;
            }

            SetTarget(bestTarget);
            return true;
        }

        public void ClearTarget()
        {
            CurrentTarget = null;
            initialCircleEvaluatedTarget = null;
            hasPendingDamageFleeRequest = false;
            consecutiveAttackDecisions = 0;
            ResetTargetMotion();
            HasLastKnownTargetPosition = false;
            LastKnownTargetPosition = default;
            ClearFleeDestination();
            ClearAggressionNotification();
        }

        public void ClearDamageFleeRequest()
        {
            hasPendingDamageFleeRequest = false;
        }

        public void ClearFleeDestination()
        {
            HasFleeDestination = false;
        }

        public void ClearCombatMoveDestination()
        {
            HasCombatMoveDestination = false;
        }

        public bool TryGetKnownThreatDistance(out float distance)
        {
            distance = 0f;
            if (ownerTransform == null)
            {
                return false;
            }

            if (HasCombatTarget && IsTargetVisible)
            {
                distance = PlanarDistance(ownerTransform.position, CurrentTarget.transform.position);
                return true;
            }

            if (HasLastKnownTargetPosition)
            {
                distance = PlanarDistance(ownerTransform.position, LastKnownTargetPosition);
                return true;
            }

            if (HasCombatTarget)
            {
                distance = PlanarDistance(ownerTransform.position, CurrentTarget.transform.position);
                return true;
            }

            return false;
        }

        public float GetFleeDecisionDuration(bool forceFastDecision = false)
        {
            var maxDuration = Mathf.Max(0f, combatConfig != null ? combatConfig.FleeLookBackDuration : 1.5f);
            var minDuration = Mathf.Clamp(combatConfig != null ? combatConfig.FleeMinDecisionDuration : 0.15f, 0f, maxDuration);
            if (forceFastDecision || IsTargetInAttackRange)
            {
                return minDuration;
            }

            if (!TryGetKnownThreatDistance(out var threatDistance))
            {
                return maxDuration;
            }

            var closeDistance = Mathf.Max(0f, combatConfig != null ? combatConfig.FleeCloseThreatDistance : 2.25f);
            var farDistance = Mathf.Max(closeDistance + 0.01f, combatConfig != null ? combatConfig.FleeFarThreatDistance : 8f);
            if (threatDistance <= closeDistance)
            {
                return minDuration;
            }

            if (threatDistance >= farDistance)
            {
                return maxDuration;
            }

            var distanceFactor = Mathf.InverseLerp(closeDistance, farDistance, threatDistance);
            return Mathf.Lerp(minDuration, maxDuration, distanceFactor);
        }

        public bool TryPrepareWeapon()
        {
            if (weaponController == null)
            {
                return false;
            }

            if (!weaponController.HasWeaponInWeaponSlots)
            {
                inventory?.TryMoveFirstGridItemToEmptySlot(ItemType.Weapon);
            }

            return weaponController.RequestDrawWeapon();
        }

        public bool TrySelectFleeDestination()
        {
            if (ownerTransform == null || !TryGetThreatPosition(out var threatPosition))
            {
                return false;
            }

            var awayDirection = ownerTransform.position - threatPosition;
            awayDirection.y = 0f;
            if (awayDirection.sqrMagnitude <= 0.0001f)
            {
                awayDirection = -ownerTransform.forward;
                awayDirection.y = 0f;
            }

            awayDirection.Normalize();
            var minDistance = combatConfig != null ? combatConfig.FleeMinDistance : 6f;
            var maxDistance = Mathf.Max(minDistance, combatConfig != null ? combatConfig.FleeMaxDistance : 10f);
            var angleJitter = combatConfig != null ? combatConfig.FleeAngleJitter : 25f;
            var attempts = Mathf.Max(1, combatConfig != null ? combatConfig.FleeSampleAttempts : 8);
            var sampleRadius = combatConfig != null ? combatConfig.FleeNavMeshSampleRadius : 3f;
            var currentThreatDistance = PlanarDistance(ownerTransform.position, threatPosition);
            var primaryEscapeBlocked = IsPrimaryEscapeBlocked(awayDirection, minDistance, sampleRadius);
            var bestScore = float.NegativeInfinity;
            var bestPosition = Vector3.zero;
            var hasBestPosition = false;
            var angleStages = new[]
            {
                angleJitter,
                Mathf.Max(angleJitter, 60f),
                Mathf.Max(angleJitter, 120f),
                180f
            };

            foreach (var angleLimit in angleStages)
            {
                TryEvaluateFleeDirection(
                    awayDirection,
                    0f,
                    minDistance,
                    maxDistance,
                    sampleRadius,
                    threatPosition,
                    currentThreatDistance,
                    primaryEscapeBlocked,
                    ref bestScore,
                    ref bestPosition,
                    ref hasBestPosition);

                TryEvaluateFleeDirection(
                    awayDirection,
                    angleLimit * 0.5f,
                    minDistance,
                    maxDistance,
                    sampleRadius,
                    threatPosition,
                    currentThreatDistance,
                    primaryEscapeBlocked,
                    ref bestScore,
                    ref bestPosition,
                    ref hasBestPosition);

                TryEvaluateFleeDirection(
                    awayDirection,
                    -angleLimit * 0.5f,
                    minDistance,
                    maxDistance,
                    sampleRadius,
                    threatPosition,
                    currentThreatDistance,
                    primaryEscapeBlocked,
                    ref bestScore,
                    ref bestPosition,
                    ref hasBestPosition);

                TryEvaluateFleeDirection(
                    awayDirection,
                    angleLimit,
                    minDistance,
                    maxDistance,
                    sampleRadius,
                    threatPosition,
                    currentThreatDistance,
                    primaryEscapeBlocked,
                    ref bestScore,
                    ref bestPosition,
                    ref hasBestPosition);

                TryEvaluateFleeDirection(
                    awayDirection,
                    -angleLimit,
                    minDistance,
                    maxDistance,
                    sampleRadius,
                    threatPosition,
                    currentThreatDistance,
                    primaryEscapeBlocked,
                    ref bestScore,
                    ref bestPosition,
                    ref hasBestPosition);

                for (var attempt = 0; attempt < attempts; attempt++)
                {
                    var angle = UnityEngine.Random.Range(-angleLimit, angleLimit);
                    var distance = UnityEngine.Random.Range(minDistance, maxDistance);
                    var direction = Quaternion.Euler(0f, angle, 0f) * awayDirection;
                    var candidate = ownerTransform.position + direction * distance;
                    TryEvaluateFleeCandidate(
                        candidate,
                        sampleRadius,
                        threatPosition,
                        currentThreatDistance,
                        awayDirection,
                        primaryEscapeBlocked,
                        ref bestScore,
                        ref bestPosition,
                        ref hasBestPosition);
                }
            }

            if (!hasBestPosition)
            {
                return false;
            }

            FleeDestination = bestPosition;
            HasFleeDestination = true;
            return true;
        }

        private void TryEvaluateFleeDirection(
            Vector3 awayDirection,
            float angle,
            float minDistance,
            float maxDistance,
            float sampleRadius,
            Vector3 threatPosition,
            float currentThreatDistance,
            bool primaryEscapeBlocked,
            ref float bestScore,
            ref Vector3 bestPosition,
            ref bool hasBestPosition)
        {
            var direction = Quaternion.Euler(0f, angle, 0f) * awayDirection;
            var distances = new[]
            {
                maxDistance,
                (minDistance + maxDistance) * 0.5f,
                minDistance
            };

            foreach (var distance in distances)
            {
                var candidate = ownerTransform.position + direction * distance;
                TryEvaluateFleeCandidate(
                    candidate,
                    sampleRadius,
                    threatPosition,
                    currentThreatDistance,
                    awayDirection,
                    primaryEscapeBlocked,
                    ref bestScore,
                    ref bestPosition,
                    ref hasBestPosition);
            }
        }

        private void TryEvaluateFleeCandidate(
            Vector3 candidate,
            float sampleRadius,
            Vector3 threatPosition,
            float currentThreatDistance,
            Vector3 awayDirection,
            bool primaryEscapeBlocked,
            ref float bestScore,
            ref Vector3 bestPosition,
            ref bool hasBestPosition)
        {
            if (!TrySampleReachablePosition(candidate, sampleRadius, out var reachablePosition, out var pathLength, out var firstPathDirection))
            {
                return;
            }

            var displacement = reachablePosition - ownerTransform.position;
            displacement.y = 0f;
            if (displacement.sqrMagnitude <= 0.25f)
            {
                return;
            }

            var destinationThreatDistance = PlanarDistance(reachablePosition, threatPosition);
            var distanceGain = destinationThreatDistance - currentThreatDistance;
            var directness = displacement.sqrMagnitude > 0.0001f
                ? Vector3.Dot(displacement.normalized, awayDirection)
                : 0f;
            var firstMoveAway = firstPathDirection.sqrMagnitude > 0.0001f
                ? Vector3.Dot(firstPathDirection.normalized, awayDirection)
                : directness;
            var opennessProbeDistance = combatConfig != null ? combatConfig.FleeOpennessProbeDistance : 3f;
            var opennessWeight = combatConfig != null ? combatConfig.FleeOpennessWeight : 20f;
            if (primaryEscapeBlocked)
            {
                opennessWeight *= 1.5f;
            }

            var distanceGainWeight = primaryEscapeBlocked ? 6f : 8f;
            var openness = CalculateFleeOpenness(reachablePosition, opennessProbeDistance);
            var safetyPenalty = 0f;
            if (distanceGain < 0f)
            {
                safetyPenalty += Mathf.Abs(distanceGain) * 20f;
            }

            if (directness < 0f)
            {
                safetyPenalty += Mathf.Abs(directness) * 12f;
            }

            if (firstMoveAway < 0f)
            {
                safetyPenalty += Mathf.Abs(firstMoveAway) * 30f;
            }

            var score = distanceGain * distanceGainWeight
                        + Mathf.Max(0f, directness) * 2f
                        + Mathf.Max(0f, firstMoveAway) * 4f
                        + displacement.magnitude * 0.1f
                        + openness * opennessWeight
                        - pathLength * 0.05f
                        - safetyPenalty;

            if (score <= bestScore)
            {
                return;
            }

            bestScore = score;
            bestPosition = reachablePosition;
            hasBestPosition = true;
        }

        private bool TrySampleReachablePosition(
            Vector3 candidate,
            float sampleRadius,
            out Vector3 position,
            out float pathLength,
            out Vector3 firstPathDirection)
        {
            position = default;
            pathLength = 0f;
            firstPathDirection = default;
            if (!NavMesh.SamplePosition(candidate, out var destinationHit, sampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            if (!NavMesh.SamplePosition(ownerTransform.position, out var startHit, sampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(startHit.position, destinationHit.position, NavMesh.AllAreas, path)
             || path.status != NavMeshPathStatus.PathComplete
             || path.corners == null
             || path.corners.Length == 0)
            {
                return false;
            }

            for (var index = 1; index < path.corners.Length; index++)
            {
                var segment = path.corners[index] - path.corners[index - 1];
                segment.y = 0f;
                pathLength += segment.magnitude;
            }

            if (path.corners.Length > 1)
            {
                firstPathDirection = path.corners[1] - path.corners[0];
                firstPathDirection.y = 0f;
            }

            position = destinationHit.position;
            return true;
        }

        private bool IsPrimaryEscapeBlocked(Vector3 awayDirection, float distance, float sampleRadius)
        {
            if (!NavMesh.SamplePosition(ownerTransform.position, out var startHit, sampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            var target = startHit.position + awayDirection * distance;
            return NavMesh.Raycast(startHit.position, target, out _, NavMesh.AllAreas);
        }

        private static float CalculateFleeOpenness(Vector3 position, float probeDistance)
        {
            probeDistance = Mathf.Max(0.1f, probeDistance);
            const int probeCount = 8;
            var openness = 0f;
            for (var index = 0; index < probeCount; index++)
            {
                var angle = 360f / probeCount * index;
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var target = position + direction * probeDistance;
                if (!NavMesh.Raycast(position, target, out var hit, NavMesh.AllAreas))
                {
                    openness += 1f;
                    continue;
                }

                openness += Mathf.Clamp01(hit.distance / probeDistance);
            }

            return openness / probeCount;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void TickTacticalMemory()
        {
            var memoryDuration = Mathf.Max(0.1f, combatConfig != null ? combatConfig.DamageMemoryDuration : 4f);
            var decay = Time.deltaTime / memoryDuration;
            recentDamageTaken = Mathf.MoveTowards(recentDamageTaken, 0f, Mathf.Max(0.01f, recentDamageTaken) * decay);
            recentDamageDealt = Mathf.MoveTowards(recentDamageDealt, 0f, Mathf.Max(0.01f, recentDamageDealt) * decay);
            spacingRetreatCooldown = Mathf.Max(0f, spacingRetreatCooldown - Time.deltaTime);
            approachBurstCooldown = Mathf.Max(0f, approachBurstCooldown - Time.deltaTime);
            TickTargetMotion();

            if (HasCombatTarget && ownerTransform != null
                && PlanarDistance(ownerTransform.position, CurrentTarget.transform.position) >= GetPersonalSpaceReleaseDistance())
            {
                consecutiveSpacingRetreats = 0;
            }
        }

        private void TickTargetMotion()
        {
            if (!HasCombatTarget || ownerTransform == null)
            {
                ResetTargetMotion();
                return;
            }

            var target = CurrentTarget;
            var targetPosition = target.transform.position;
            if (motionObservedTarget != target || !hasPreviousTargetPosition)
            {
                motionObservedTarget = target;
                previousTargetPosition = targetPosition;
                hasPreviousTargetPosition = true;
                targetRetreatSpeed = 0f;
                return;
            }

            var movement = targetPosition - previousTargetPosition;
            movement.y = 0f;
            previousTargetPosition = targetPosition;

            var toTarget = targetPosition - ownerTransform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f || Time.deltaTime <= Mathf.Epsilon)
            {
                targetRetreatSpeed = 0f;
                return;
            }

            targetRetreatSpeed = Mathf.Max(0f, Vector3.Dot(movement / Time.deltaTime, toTarget.normalized));
        }

        private void ResetTargetMotion()
        {
            motionObservedTarget = null;
            previousTargetPosition = default;
            hasPreviousTargetPosition = false;
            targetRetreatSpeed = 0f;
        }

        private float GetSituationalAggression()
        {
            var baseAggression = GetProfileValue(profile => profile.Aggression, 0.5f);
            var pressureResponse = GetProfileValue(profile => profile.PressureResponse, 0.7f);
            var exchange = GetDamageExchange();
            var hpAdvantage = GetHpAdvantage();
            var lowHealthPressure = GetLowHealthPressure();
            return Mathf.Clamp01(baseAggression
                                 + (exchange - 0.5f) * pressureResponse
                                 + hpAdvantage * 0.25f
                                 - lowHealthPressure * 0.15f);
        }

        private float GetSituationalCaution()
        {
            var baseCaution = GetProfileValue(profile => profile.Caution, 0.5f);
            var damageResponse = GetProfileValue(profile => profile.DamageCautionResponse, 0.75f);
            var staminaResponse = GetProfileValue(profile => profile.LowStaminaCautionResponse, 0.8f);
            // An unknown/even exchange is neutral. Caution rises only after the NPC is
            // actually losing the recent exchange, rather than being permanently biased
            // towards retreat before either fighter has landed a hit.
            var damagePressure = Mathf.Max(0f, (0.5f - GetDamageExchange()) * 2f);
            var hpDeficit = Mathf.Max(0f, -GetHpAdvantage());
            var lowHealthPressure = GetLowHealthPressure();
            var staminaDeficit = 1f - StaminaNormalized;
            return Mathf.Clamp01(baseCaution
                                 + damagePressure * damageResponse * 0.45f
                                 + hpDeficit * damageResponse * 0.5f
                                 + lowHealthPressure * damageResponse * 0.75f
                                 + staminaDeficit * staminaResponse * 0.45f);
        }

        private float GetDamageExchange()
        {
            var total = recentDamageTaken + recentDamageDealt;
            return total <= Mathf.Epsilon ? 0.5f : recentDamageDealt / total;
        }

        private float GetOwnerHpNormalized()
        {
            var maxHp = statsController?.Hp.Max ?? 0f;
            return ownerDamageReceiver == null || maxHp <= Mathf.Epsilon
                ? 1f
                : Mathf.Clamp01(ownerDamageReceiver.CurrentHp / maxHp);
        }

        private float GetLowHealthPressure()
        {
            return Mathf.InverseLerp(0.65f, 0.2f, GetOwnerHpNormalized());
        }

        private float GetHpAdvantage()
        {
            if (ownerDamageReceiver == null || !HasCombatTarget)
            {
                return 0f;
            }

            var targetReceiver = FindDamageReceiver(CurrentTarget);
            if (targetReceiver == null)
            {
                return 0f;
            }

            var ownerMaxHp = statsController?.Hp.Max ?? 0f;
            var targetStats = GetStatsController(targetReceiver);
            var targetMaxHp = targetStats?.Hp.Max ?? 0f;
            if (ownerMaxHp <= Mathf.Epsilon || targetMaxHp <= Mathf.Epsilon)
            {
                return 0f;
            }

            var ownerHp = Mathf.Clamp01(ownerDamageReceiver.CurrentHp / ownerMaxHp);
            var targetHp = Mathf.Clamp01(targetReceiver.CurrentHp / targetMaxHp);
            return ownerHp - targetHp;
        }

        private static CharacterDamageReceiver FindDamageReceiver(TargetLockTarget target)
        {
            return target != null ? target.GetComponentInParent<DamageReceiverHost>()?.Receiver : null;
        }

        private static StatsController GetStatsController(CharacterDamageReceiver receiver)
        {
            var scope = receiver?.OwnerTransform?.GetComponentInParent<LifetimeScope>();
            return scope != null && scope.Container.TryResolve<StatsController>(out var targetStats)
                ? targetStats
                : null;
        }

        private float GetProfileValue(Func<NpcCombatProfile, float> selector, float fallback)
        {
            var profile = ownerScope?.CombatProfile;
            return profile != null && selector != null ? Mathf.Clamp01(selector(profile)) : fallback;
        }

        private float GetPersonalSpaceDistance()
        {
            return Mathf.Max(0.1f, combatConfig != null ? combatConfig.PersonalSpaceDistance : 1.05f);
        }

        private float GetPersonalSpaceReleaseDistance()
        {
            return Mathf.Max(GetPersonalSpaceDistance(), combatConfig != null ? combatConfig.PersonalSpaceReleaseDistance : 1.45f);
        }

        private bool CanUseSpacingRetreat()
        {
            var maxRetreats = Mathf.Max(1, combatConfig != null ? combatConfig.MaxConsecutiveSpacingRetreats : 1);
            return spacingRetreatCooldown <= 0f && consecutiveSpacingRetreats < maxRetreats;
        }

        private bool CanUseSpacingEvasion()
        {
            return CanUseSpacingRetreat()
                   && weaponController?.CanRequestEvasion == true
                   && StaminaNormalized >= 0.12f;
        }

        private void RegisterSpacingRetreat()
        {
            consecutiveSpacingRetreats++;
            spacingRetreatCooldown = Mathf.Max(0f, combatConfig != null ? combatConfig.SpacingRetreatCooldown : 0.8f);
        }

        private static float ScaleWeight(float weight, float multiplier)
        {
            return Mathf.Max(0f, weight) * Mathf.Max(0f, multiplier);
        }

        public bool TrySelectCombatManeuverDestination(NpcCombatManeuverKind kind)
        {
            if (ownerTransform == null || !HasCombatTarget)
            {
                return false;
            }

            var toTarget = CurrentTarget.transform.position - ownerTransform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            toTarget.Normalize();
            var sampleRadius = combatConfig != null ? combatConfig.CombatMoveNavMeshSampleRadius : 2f;
            var destination = kind switch
            {
                NpcCombatManeuverKind.Strafe => BuildStrafeDestination(toTarget),
                NpcCombatManeuverKind.Backstep => BuildBackstepDestination(toTarget),
                NpcCombatManeuverKind.Circle => BuildCircleDestination(),
                NpcCombatManeuverKind.QueueCircle => BuildQueueCircleDestination(),
                _ => ownerTransform.position
            };

            if (!TrySampleReachablePosition(destination, sampleRadius, out var reachablePosition))
            {
                return false;
            }

            CombatMoveDestination = reachablePosition;
            HasCombatMoveDestination = true;
            return true;
        }

        public NpcCombatDecision SelectPostAttackDecision()
        {
            if (!HasCombatTarget || !IsTargetVisible)
            {
                return FinalizePostAttackDecision(NpcCombatDecision.Approach);
            }

            if (!CanStartAttack)
            {
                return FinalizePostAttackDecision(NpcCombatDecision.Approach);
            }

            var aggression = GetSituationalAggression();
            var caution = GetSituationalCaution();
            var unpredictability = GetProfileValue(profile => profile.Unpredictability, 0.35f);
            var tooClose = IsTargetTooClose;
            var maxConsecutiveAttacks = Mathf.Max(1, combatConfig != null ? combatConfig.MaxConsecutiveAttackDecisions : 2);
            var repeatedAttackProgress = Mathf.Clamp01(consecutiveAttackDecisions / (float)maxConsecutiveAttacks);
            var repetitionPenalty = combatConfig != null ? combatConfig.AttackRepetitionPenalty : 0.65f;
            var variationBoost = 1f + repeatedAttackProgress * (0.2f + Mathf.Clamp01(repetitionPenalty) * 0.45f);

            var attackWeight = ScaleWeight(
                combatConfig != null ? combatConfig.PostAttackImmediateAttackWeight : 0.45f,
                (0.45f + aggression * 1.45f - caution * 0.35f)
                * (1f - repeatedAttackProgress * Mathf.Clamp01(repetitionPenalty)));
            var strafeWeight = ScaleWeight(
                combatConfig != null ? combatConfig.PostAttackStrafeWeight : 0.25f,
                (0.55f + caution * 1.25f + unpredictability * 0.35f) * variationBoost);
            var retreatWeight = CanUseSpacingRetreat()
                ? ScaleWeight(
                    combatConfig != null ? combatConfig.PostAttackBackstepWeight : 0.2f,
                    (tooClose ? 2.8f + caution * 1.4f : 0.25f + caution * 0.5f) * variationBoost)
                : 0f;
            var evasionShare = CanUseSpacingEvasion()
                ? Mathf.Lerp(0.3f, 0.7f, GetProfileValue(profile => profile.DodgePreference, 0.55f))
                : 0f;
            var evasionWeight = retreatWeight * evasionShare;
            var backstepWeight = retreatWeight * (1f - evasionShare);
            var circleWeight = ScaleWeight(
                combatConfig != null ? combatConfig.PostAttackCircleWeight : 0.1f,
                (0.45f + caution * 0.75f + unpredictability * 0.7f) * variationBoost);
            var waitWeight = ScaleWeight(
                combatConfig != null ? combatConfig.PostAttackWaitWeight : 0.12f,
                (0.35f + caution * 1.35f + (1f - aggression) * 0.25f) * variationBoost);
            var keepDistanceWeight = ScaleWeight(
                combatConfig != null ? combatConfig.PostAttackKeepDistanceWeight : 0.18f,
                (0.4f + caution * 1.55f + GetProfileValue(profile => profile.DistancePreference, 0.5f)) * variationBoost);
            var totalWeight = attackWeight + strafeWeight + evasionWeight + backstepWeight + circleWeight + waitWeight + keepDistanceWeight;
            if (totalWeight <= 0f)
            {
                return FinalizePostAttackDecision(NpcCombatDecision.Attack);
            }

            var roll = UnityEngine.Random.Range(0f, totalWeight);
            if (roll < attackWeight)
            {
                return FinalizePostAttackDecision(NpcCombatDecision.Attack);
            }

            roll -= attackWeight;
            if (roll < strafeWeight && TrySelectCombatManeuverDestination(NpcCombatManeuverKind.Strafe))
            {
                return FinalizePostAttackDecision(NpcCombatDecision.Maneuver);
            }

            roll -= strafeWeight;
            if (roll < evasionWeight)
            {
                return FinalizePostAttackDecision(NpcCombatDecision.Evade);
            }

            roll -= evasionWeight;
            if (roll < backstepWeight && TrySelectCombatManeuverDestination(NpcCombatManeuverKind.Backstep))
            {
                RegisterSpacingRetreat();
                return FinalizePostAttackDecision(NpcCombatDecision.Maneuver);
            }

            roll -= backstepWeight;
            if (roll < waitWeight)
            {
                return FinalizePostAttackDecision(NpcCombatDecision.Wait);
            }

            roll -= waitWeight;
            if (roll < keepDistanceWeight && TrySelectKeepDistanceDestination())
            {
                return FinalizePostAttackDecision(NpcCombatDecision.KeepDistance);
            }

            return FinalizePostAttackDecision(TrySelectCombatManeuverDestination(NpcCombatManeuverKind.Circle)
                ? NpcCombatDecision.Circle
                : NpcCombatDecision.Attack);
        }

        private NpcCombatDecision FinalizePostAttackDecision(NpcCombatDecision decision)
        {
            if (decision == NpcCombatDecision.Attack)
            {
                consecutiveAttackDecisions++;
            }
            else if (decision != NpcCombatDecision.None)
            {
                consecutiveAttackDecisions = 0;
            }

            return decision;
        }

        public void ReceiveAggressionNotification(TargetLockTarget target, bool sourceIsFriendly)
        {
            if (target == null || !target.IsTargetable || !IsTargetAlive(target) || target.transform == ownerTransform)
            {
                return;
            }

            if (!sourceIsFriendly && !IsEnemy(target))
            {
                return;
            }

            if (sourceIsFriendly)
            {
                RememberPersonalEnemy(target);
            }

            SetTarget(target);
        }

        public bool ShouldStartInitialCircle()
        {
            if (!HasCombatTarget || initialCircleEvaluatedTarget == CurrentTarget)
            {
                return false;
            }

            initialCircleEvaluatedTarget = CurrentTarget;
            var chance = combatConfig != null ? combatConfig.InitialCircleChance : 0.25f;
            return IsTargetVisible
                   && UnityEngine.Random.value < Mathf.Clamp01(chance)
                   && TrySelectCombatManeuverDestination(NpcCombatManeuverKind.Circle);
        }

        public bool ShouldQueueForCombatSlot()
        {
            return HasCombatTarget && GetDirectAttackRank() >= GetMaxDirectAttackers();
        }

        public bool HasDirectCombatSlot()
        {
            return HasCombatTarget && GetDirectAttackRank() < GetMaxDirectAttackers();
        }

        public bool TrySelectQueueCircleDestination()
        {
            return TrySelectCombatManeuverDestination(NpcCombatManeuverKind.QueueCircle);
        }

        public bool TrySelectKeepDistanceDestination()
        {
            if (ownerTransform == null || !HasCombatTarget)
            {
                return false;
            }

            var targetPosition = CurrentTarget.transform.position;
            var ownerPosition = ownerTransform.position;
            var fromTarget = ownerPosition - targetPosition;
            fromTarget.y = 0f;
            if (fromTarget.sqrMagnitude <= 0.0001f)
            {
                fromTarget = -ownerTransform.forward;
                fromTarget.y = 0f;
            }

            if (fromTarget.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            fromTarget.Normalize();
            var minRange = combatConfig != null ? combatConfig.KeepDistanceMinRange : 2.3f;
            var maxRange = Mathf.Max(minRange, combatConfig != null ? combatConfig.KeepDistanceMaxRange : 3.5f);
            var currentDistance = PlanarDistance(ownerPosition, targetPosition);
            var desiredRange = UnityEngine.Random.Range(minRange, maxRange);
            var direction = fromTarget;

            if (currentDistance < minRange)
            {
                var angle = combatConfig != null ? combatConfig.KeepDistanceRetreatAngle : 35f;
                direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-angle, angle), 0f) * fromTarget;
                desiredRange = maxRange;
            }
            else if (currentDistance <= maxRange)
            {
                var strafeChance = combatConfig != null ? combatConfig.KeepDistanceStrafeChance : 0.65f;
                if (UnityEngine.Random.value < Mathf.Clamp01(strafeChance))
                {
                    var side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                    var tangent = Vector3.Cross(Vector3.up, fromTarget).normalized * side;
                    direction = (fromTarget * 0.35f + tangent).normalized;
                    desiredRange = Mathf.Clamp(currentDistance, minRange, maxRange);
                }
            }

            var candidate = targetPosition + direction.normalized * desiredRange;
            var sampleRadius = combatConfig != null ? combatConfig.CombatMoveNavMeshSampleRadius : 2f;
            if (!TrySampleReachablePosition(candidate, sampleRadius, out var reachablePosition))
            {
                return TryGetAlternativeKeepDistanceDestination(targetPosition, fromTarget, minRange, maxRange, sampleRadius, out reachablePosition);
            }

            CombatMoveDestination = reachablePosition;
            HasCombatMoveDestination = true;
            return true;
        }

        public bool TryGetApproachDestination(out Vector3 destination, out float stoppingDistance)
        {
            destination = default;
            stoppingDistance = combatConfig != null ? combatConfig.ApproachStoppingDistance : 1.6f;
            if (!HasCombatTarget)
            {
                return false;
            }

            var rank = GetDirectAttackRank();
            if (rank < 0 || rank >= GetMaxDirectAttackers())
            {
                destination = CurrentTarget.transform.position;
                return true;
            }

            var radius = combatConfig != null ? combatConfig.DirectAttackSlotRadius : 1.65f;
            var slotCount = Mathf.Max(1, GetMaxDirectAttackers());
            var angle = 360f / slotCount * rank;
            var offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            destination = CurrentTarget.transform.position + offset;
            stoppingDistance = combatConfig != null ? combatConfig.CombatMoveReachedDistance : 0.45f;
            return true;
        }

        private bool TryGetAlternativeKeepDistanceDestination(
            Vector3 targetPosition,
            Vector3 fromTarget,
            float minRange,
            float maxRange,
            float sampleRadius,
            out Vector3 destination)
        {
            destination = default;
            var baseAngle = Mathf.Atan2(fromTarget.x, fromTarget.z) * Mathf.Rad2Deg;
            Span<float> angleOffsets = stackalloc[] { 30f, -30f, 60f, -60f, 100f, -100f, 145f, -145f, 180f };
            Span<float> ranges = stackalloc[]
            {
                maxRange,
                (minRange + maxRange) * 0.5f,
                minRange
            };

            foreach (var range in ranges)
            {
                foreach (var angleOffset in angleOffsets)
                {
                    var direction = Quaternion.Euler(0f, baseAngle + angleOffset, 0f) * Vector3.forward;
                    var candidate = targetPosition + direction * range;
                    if (!TrySampleReachablePosition(candidate, sampleRadius, out destination))
                    {
                        continue;
                    }

                    CombatMoveDestination = destination;
                    HasCombatMoveDestination = true;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetAlternativeApproachDestination(out Vector3 destination, out float stoppingDistance)
        {
            destination = default;
            stoppingDistance = combatConfig != null ? combatConfig.CombatMoveReachedDistance : 0.45f;
            if (ownerTransform == null || !HasCombatTarget)
            {
                return false;
            }

            var targetPosition = CurrentTarget.transform.position;
            var fromTarget = ownerTransform.position - targetPosition;
            fromTarget.y = 0f;
            if (fromTarget.sqrMagnitude <= 0.0001f)
            {
                fromTarget = -ownerTransform.forward;
                fromTarget.y = 0f;
            }

            fromTarget.Normalize();
            var baseAngle = Mathf.Atan2(fromTarget.x, fromTarget.z) * Mathf.Rad2Deg;
            var radius = combatConfig != null ? combatConfig.DirectAttackSlotRadius : 1.65f;
            var sampleRadius = combatConfig != null ? combatConfig.CombatMoveNavMeshSampleRadius : 2f;
            Span<float> angleOffsets = stackalloc[] { 45f, -45f, 90f, -90f, 135f, -135f, 180f, 0f };
            Span<float> radiusMultipliers = stackalloc[] { 1f, 1.25f, 0.8f };

            foreach (var radiusMultiplier in radiusMultipliers)
            {
                foreach (var angleOffset in angleOffsets)
                {
                    var direction = Quaternion.Euler(0f, baseAngle + angleOffset, 0f) * Vector3.forward;
                    var candidate = targetPosition + direction * radius * radiusMultiplier;
                    if (!TrySampleReachablePosition(candidate, sampleRadius, out destination))
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        public bool TryGetCloserAttackApproachDestination(out Vector3 destination, out float stoppingDistance)
        {
            destination = default;
            stoppingDistance = combatConfig != null ? combatConfig.CombatMoveReachedDistance : 0.45f;
            if (ownerTransform == null || !HasCombatTarget || vision == null)
            {
                return false;
            }

            var targetPosition = CurrentTarget.transform.position;
            var fromTarget = ownerTransform.position - targetPosition;
            fromTarget.y = 0f;
            if (fromTarget.sqrMagnitude <= 0.0001f)
            {
                fromTarget = -CurrentTarget.transform.forward;
                fromTarget.y = 0f;
            }

            if (fromTarget.sqrMagnitude <= 0.0001f)
            {
                fromTarget = -ownerTransform.forward;
                fromTarget.y = 0f;
            }

            fromTarget.Normalize();
            var attackDistance = Mathf.Max(0.35f, vision.AttackViewDistance);
            var tolerance = combatConfig != null ? combatConfig.AttackStartDistanceTolerance : 0.25f;
            var desiredRadius = Mathf.Max(0.35f, attackDistance - tolerance * 0.5f);
            var sampleRadius = combatConfig != null ? combatConfig.CombatMoveNavMeshSampleRadius : 2f;
            var baseAngle = Mathf.Atan2(fromTarget.x, fromTarget.z) * Mathf.Rad2Deg;
            Span<float> angleOffsets = stackalloc[] { 0f, 20f, -20f, 45f, -45f, 75f, -75f, 110f, -110f };
            Span<float> radiusMultipliers = stackalloc[] { 0.85f, 0.65f, 1f };

            foreach (var radiusMultiplier in radiusMultipliers)
            {
                foreach (var angleOffset in angleOffsets)
                {
                    var direction = Quaternion.Euler(0f, baseAngle + angleOffset, 0f) * Vector3.forward;
                    var candidate = targetPosition + direction * desiredRadius * radiusMultiplier;
                    if (!TrySampleReachablePosition(candidate, sampleRadius, out destination))
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        public bool TryResolveCurrentTargetDown()
        {
            if (!IsCurrentTargetDown)
            {
                return HasCombatTarget;
            }

            ClearAggressionNotification();
            var oldTarget = CurrentTarget;
            CurrentTarget = null;
            if (oldTarget != null)
            {
                LastKnownTargetPosition = oldTarget.transform.position;
                HasLastKnownTargetPosition = true;
            }

            return TryAdoptNearbyCombatTarget();
        }

        public bool TryAdoptNearbyCombatTarget()
        {
            if (ownerTransform == null)
            {
                return false;
            }

            var radius = combatConfig != null ? combatConfig.AggressionNotificationRadius : 12f;
            var radiusSqr = radius * radius;
            TargetLockTarget bestTarget = null;
            var bestDistanceSqr = float.PositiveInfinity;

            foreach (var other in ActiveServices.ToArray())
            {
                if (other == null || other == this || other.ownerTransform == null || !other.HasCombatTarget)
                {
                    continue;
                }

                if ((other.ownerTransform.position - ownerTransform.position).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                var relation = GetRelationTo(other);
                if (relation == NpcFactionRelation.Hostile)
                {
                    continue;
                }

                if (relation == NpcFactionRelation.Neutral && !IsEnemy(other.CurrentTarget))
                {
                    continue;
                }

                var distanceSqr = (other.CurrentTarget.transform.position - ownerTransform.position).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                bestTarget = other.CurrentTarget;
            }

            if (bestTarget == null)
            {
                return false;
            }

            RememberPersonalEnemy(bestTarget);
            SetTarget(bestTarget);
            return true;
        }

        public void SheatheWeapon()
        {
            weaponController?.RequestSheatheWeapon();
        }

        public bool RequestAttack()
        {
            return HasClearAttackLane() && weaponController != null && weaponController.RequestAttack();
        }

        public bool RequestHeavyAttack()
        {
            return HasClearAttackLane() && weaponController != null && weaponController.RequestHeavyAttack();
        }

        public bool ShouldUseHeavyAttack(bool isCombo)
        {
            if (!CanStartAttack || weaponController == null)
            {
                return false;
            }

            var aggression = GetSituationalAggression();
            var caution = GetSituationalCaution();
            var pressure = Mathf.Max(0f, GetDamageExchange() - 0.5f);
            var preference = GetProfileValue(profile => profile.HeavyAttackPreference, 0.35f);
            var unpredictability = GetProfileValue(profile => profile.Unpredictability, 0.35f);
            var chance = 0.03f
                         + preference * 0.14f
                         + aggression * 0.12f
                         + pressure * 0.16f
                         + (isCombo ? 0.05f + unpredictability * 0.07f : 0f)
                         - caution * 0.12f;

            if (IsTargetTooClose)
            {
                chance *= 0.45f;
            }

            if (StaminaNormalized < 0.25f)
            {
                chance *= 0.25f;
            }

            return UnityEngine.Random.value < Mathf.Clamp(chance, 0.02f, 0.45f);
        }

        public bool ConsumeAttackComboWindow()
        {
            return weaponController?.ConsumeAttackComboWindow() == true;
        }

        public bool RequestComboAttack()
        {
            return HasClearAttackLane() && weaponController != null && weaponController.RequestComboAttack();
        }

        public bool TryRequestSpacingEvasion()
        {
            if (!CanUseSpacingEvasion() || ownerTransform == null || CurrentTarget == null)
            {
                return false;
            }

            var awayFromTarget = ownerTransform.position - CurrentTarget.transform.position;
            awayFromTarget.y = 0f;
            if (awayFromTarget.sqrMagnitude <= 0.0001f)
            {
                awayFromTarget = -ownerTransform.forward;
            }

            awayFromTarget.Normalize();
            var caution = GetSituationalCaution();
            var useBackwardDirection = IsTargetTooClose && UnityEngine.Random.value < 0.55f + caution * 0.3f;
            var direction = useBackwardDirection
                ? awayFromTarget
                : Quaternion.AngleAxis(UnityEngine.Random.value < 0.5f ? -90f : 90f, Vector3.up) * awayFromTarget;
            var rollPreference = GetProfileValue(profile => profile.RollPreference, 0.3f);
            var dodgePreference = GetProfileValue(profile => profile.DodgePreference, 0.55f);
            var rollChance = Mathf.Clamp01(
                (rollPreference / Mathf.Max(0.01f, rollPreference + dodgePreference))
                + (IsTargetTooClose ? 0.12f : 0f)
                + (1f - GetOwnerHpNormalized()) * 0.12f
                - (1f - StaminaNormalized) * 0.35f);
            var requested = UnityEngine.Random.value < rollChance
                ? weaponController.RequestRoll(direction)
                : weaponController.RequestDodge(direction);

            if (requested)
            {
                RegisterSpacingRetreat();
            }

            return requested;
        }

        public bool TryRequestApproachBurst()
        {
            if (approachBurstCooldown > 0f
             || !HasCombatTarget
             || !IsTargetVisible
             || ownerTransform == null
             || weaponController?.CanRequestEvasion != true
             || StaminaNormalized < 0.25f)
            {
                return false;
            }

            var toTarget = CurrentTarget.transform.position - ownerTransform.position;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            var minDistance = Mathf.Max(0.1f, combatConfig != null ? combatConfig.ApproachBurstMinDistance : 2.5f);
            var maxDistance = Mathf.Max(minDistance, combatConfig != null ? combatConfig.ApproachBurstMaxDistance : 6.5f);
            if (distance < minDistance || distance > maxDistance || toTarget.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            // A lunge is intentionally a forward dodge/roll. If the NPC has not yet turned
            // towards the target, let normal approach facing finish first instead of producing
            // a visually confusing sideways dodge.
            if (Vector3.Dot(ownerTransform.forward, toTarget.normalized) < 0.8f)
            {
                return false;
            }

            var aggression = GetSituationalAggression();
            var unpredictability = GetProfileValue(profile => profile.Unpredictability, 0.35f);
            var retreatThreshold = combatConfig != null ? combatConfig.TargetRetreatSpeedThreshold : 0.5f;
            var targetIsRetreating = targetRetreatSpeed >= Mathf.Max(0f, retreatThreshold);
            var baseChance = combatConfig != null ? combatConfig.ApproachBurstBaseChance : 0.4f;
            var styleMultiplier = 0.35f + aggression * 0.6f + unpredictability * 0.3f;
            var movementMultiplier = targetIsRetreating ? 1.55f : 0.28f;
            var chance = Mathf.Clamp01(baseChance * styleMultiplier * movementMultiplier);
            if (UnityEngine.Random.value >= chance)
            {
                approachBurstCooldown = Mathf.Max(0.05f, combatConfig != null ? combatConfig.ApproachBurstDecisionInterval : 0.8f);
                return false;
            }

            var rollPreference = GetProfileValue(profile => profile.RollPreference, 0.3f);
            var dodgePreference = GetProfileValue(profile => profile.DodgePreference, 0.55f);
            var distanceProgress = Mathf.InverseLerp(minDistance, maxDistance, distance);
            var rollChance = Mathf.Clamp01(
                rollPreference / Mathf.Max(0.01f, rollPreference + dodgePreference)
                + distanceProgress * 0.2f
                + unpredictability * 0.1f
                - (1f - StaminaNormalized) * 0.25f);
            var requested = UnityEngine.Random.value < rollChance
                ? weaponController.RequestRoll(toTarget.normalized)
                : weaponController.RequestDodge(toTarget.normalized);
            if (requested)
            {
                approachBurstCooldown = Mathf.Max(0f, combatConfig != null ? combatConfig.ApproachBurstCooldown : 2.2f);
            }

            return requested;
        }

        public void ClearAttackRequest()
        {
            weaponController?.ClearAttackRequest();
        }

        public bool HasClearAttackLane()
        {
            if (combatConfig == null || !combatConfig.PreventFriendlyFire || ownerTransform == null || !HasCombatTarget)
            {
                return true;
            }

            var start = ownerTransform.position;
            var end = CurrentTarget.transform.position;
            start.y = 0f;
            end.y = 0f;
            var toTarget = end - start;
            var targetDistance = toTarget.magnitude;
            if (targetDistance <= 0.01f)
            {
                return true;
            }

            var direction = toTarget / targetDistance;
            var laneRadius = combatConfig.FriendlyFireLaneRadius;
            var laneRadiusSqr = laneRadius * laneRadius;

            foreach (var other in ActiveServices.ToArray())
            {
                if (other == null || other == this || other.ownerTransform == null)
                {
                    continue;
                }

                if (GetRelationTo(other) == NpcFactionRelation.Hostile)
                {
                    continue;
                }

                var otherPosition = other.ownerTransform.position;
                otherPosition.y = 0f;
                var toOther = otherPosition - start;
                var projectedDistance = Vector3.Dot(toOther, direction);
                if (projectedDistance <= 0f || projectedDistance >= targetDistance)
                {
                    continue;
                }

                var closestPoint = start + direction * projectedDistance;
                if ((otherPosition - closestPoint).sqrMagnitude <= laneRadiusSqr)
                {
                    return false;
                }
            }

            return true;
        }

        public void RefreshTargetVisibility()
        {
            if (!HasCombatTarget)
            {
                return;
            }

            if (IsTargetVisible)
            {
                LastKnownTargetPosition = CurrentTarget.transform.position;
                HasLastKnownTargetPosition = true;
            }
        }

        public void FaceTarget()
        {
            if (!HasCombatTarget || ownerTransform == null)
            {
                return;
            }

            if (targetLockController?.TryFace(CurrentTarget) == true)
            {
                return;
            }

            FacePosition(CurrentTarget.transform.position);
        }

        public void FaceLastKnownPosition()
        {
            if (!HasLastKnownTargetPosition)
            {
                return;
            }

            FacePosition(LastKnownTargetPosition);
        }

        private bool TryGetThreatPosition(out Vector3 threatPosition)
        {
            if (HasCombatTarget)
            {
                threatPosition = CurrentTarget.transform.position;
                LastKnownTargetPosition = threatPosition;
                HasLastKnownTargetPosition = true;
                return true;
            }

            if (HasLastKnownTargetPosition)
            {
                threatPosition = LastKnownTargetPosition;
                return true;
            }

            threatPosition = default;
            return false;
        }

        private Vector3 BuildStrafeDestination(Vector3 toTarget)
        {
            var side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            var distance = RandomRange(
                combatConfig != null ? combatConfig.StrafeMinDistance : 1.2f,
                combatConfig != null ? combatConfig.StrafeMaxDistance : 2.2f);
            var sideways = Vector3.Cross(Vector3.up, toTarget).normalized * side;
            return ownerTransform.position + sideways * distance;
        }

        private Vector3 BuildBackstepDestination(Vector3 toTarget)
        {
            var distance = RandomRange(
                combatConfig != null ? combatConfig.BackstepMinDistance : 1.2f,
                combatConfig != null ? combatConfig.BackstepMaxDistance : 2.4f);
            return ownerTransform.position - toTarget * distance;
        }

        private Vector3 BuildCircleDestination()
        {
            var targetPosition = CurrentTarget.transform.position;
            var fromTarget = ownerTransform.position - targetPosition;
            fromTarget.y = 0f;
            if (fromTarget.sqrMagnitude <= 0.0001f)
            {
                fromTarget = -ownerTransform.forward;
                fromTarget.y = 0f;
            }

            fromTarget.Normalize();
            var radius = RandomRange(
                combatConfig != null ? combatConfig.CircleMinRadius : 2.2f,
                combatConfig != null ? combatConfig.CircleMaxRadius : 3.6f);
            var minAngle = combatConfig != null ? combatConfig.CircleMinAngle : 35f;
            var maxAngle = Mathf.Max(minAngle, combatConfig != null ? combatConfig.CircleMaxAngle : 75f);
            var angle = UnityEngine.Random.Range(minAngle, maxAngle);
            if (UnityEngine.Random.value < 0.5f)
            {
                angle = -angle;
            }

            var direction = Quaternion.Euler(0f, angle, 0f) * fromTarget;
            return targetPosition + direction.normalized * radius;
        }

        private Vector3 BuildQueueCircleDestination()
        {
            var targetPosition = CurrentTarget.transform.position;
            var fromTarget = ownerTransform.position - targetPosition;
            fromTarget.y = 0f;
            if (fromTarget.sqrMagnitude <= 0.0001f)
            {
                fromTarget = -ownerTransform.forward;
                fromTarget.y = 0f;
            }

            fromTarget.Normalize();
            var radius = RandomRange(
                combatConfig != null ? combatConfig.QueueCircleMinRadius : 3.8f,
                combatConfig != null ? combatConfig.QueueCircleMaxRadius : 5.4f);
            var minAngle = combatConfig != null ? combatConfig.QueueCircleMinAngle : 25f;
            var maxAngle = Mathf.Max(minAngle, combatConfig != null ? combatConfig.QueueCircleMaxAngle : 65f);
            var angle = UnityEngine.Random.Range(minAngle, maxAngle);
            if (UnityEngine.Random.value < 0.5f)
            {
                angle = -angle;
            }

            var direction = Quaternion.Euler(0f, angle, 0f) * fromTarget;
            return targetPosition + direction.normalized * radius;
        }

        private bool TrySampleReachablePosition(Vector3 destination, float sampleRadius, out Vector3 reachablePosition)
        {
            reachablePosition = default;
            if (ownerTransform == null)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(destination, out var hit, sampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(ownerTransform.position, hit.position, NavMesh.AllAreas, path)
             || path.status != NavMeshPathStatus.PathComplete
             || path.corners == null
             || path.corners.Length == 0)
            {
                return false;
            }

            reachablePosition = hit.position;
            return true;
        }

        private int GetDirectAttackRank()
        {
            if (!HasCombatTarget)
            {
                return int.MaxValue;
            }

            var participants = GetTargetParticipants(CurrentTarget);
            for (var index = 0; index < participants.Count; index++)
            {
                if (participants[index] == this)
                {
                    return index;
                }
            }

            return int.MaxValue;
        }

        private List<NpcCombatService> GetTargetParticipants(TargetLockTarget target)
        {
            return ActiveServices
                .Where(service => service != null
                               && service.ownerTransform != null
                               && service.HasCombatTarget
                               && service.CurrentTarget == target
                               && service.HasAnyWeaponAvailable)
                .OrderBy(service => (service.ownerTransform.position - target.transform.position).sqrMagnitude)
                .ThenBy(service => service.ownerTransform.GetInstanceID())
                .ToList();
        }

        private int GetMaxDirectAttackers()
        {
            return Mathf.Max(1, combatConfig != null ? combatConfig.MaxDirectAttackersPerTarget : 4);
        }

        private static float RandomRange(float min, float max)
        {
            return UnityEngine.Random.Range(Mathf.Min(min, max), Mathf.Max(min, max));
        }

        private TargetLockTarget FindBestVisibleEnemy()
        {
            if (ownerTransform == null || vision == null)
            {
                return null;
            }

            var targets = UnityEngine.Object.FindObjectsByType<TargetLockTarget>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            TargetLockTarget bestTarget = null;
            var bestDistanceSqr = float.PositiveInfinity;
            var maxDistance = combatConfig != null ? combatConfig.TargetSearchRadius : 18f;
            var maxDistanceSqr = maxDistance * maxDistance;

            foreach (var target in targets)
            {
                if (target == null || !target.IsTargetable || !IsTargetAlive(target) || target.transform == ownerTransform)
                {
                    continue;
                }

                var targetScope = target.GetComponentInParent<NpcLifetimeScope>();
                if (targetScope == ownerScope)
                {
                    continue;
                }

                var distanceSqr = (target.transform.position - ownerTransform.position).sqrMagnitude;
                if (distanceSqr > maxDistanceSqr || distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                if (!vision.IsInView(target) || !IsEnemy(target))
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                bestTarget = target;
            }

            return bestTarget;
        }

        private bool IsEnemy(TargetLockTarget target)
        {
            if (target == null)
            {
                return false;
            }

            var damageReceiver = target.GetComponentInParent<DamageReceiverHost>()?.Receiver;
            if (damageReceiver != null && personalEnemies.Contains(damageReceiver))
            {
                return true;
            }

            var targetFaction = target.GetComponentInParent<NpcLifetimeScope>()?.Faction;
            var ownerFaction = ownerScope != null ? ownerScope.Faction : null;
            if (target.GetComponentInParent<PlayerLifetimeScope>() != null && targetFaction == null)
            {
                return false;
            }

            if (ownerFaction == null || targetFaction == null)
            {
                return combatConfig != null && combatConfig.TreatFactionlessTargetsAsHostile;
            }

            return factionRelationsConfig != null && factionRelationsConfig.IsHostile(ownerFaction, targetFaction);
        }

        private bool IsHostileTo(CharacterDamageReceiver receiver)
        {
            if (receiver == null || receiver == ownerDamageReceiver)
            {
                return false;
            }

            if (personalEnemies.Contains(receiver))
            {
                return true;
            }

            if (HasCombatTarget)
            {
                var currentTargetReceiver = CurrentTarget.GetComponentInParent<DamageReceiverHost>()?.Receiver;
                if (currentTargetReceiver == receiver)
                {
                    return true;
                }
            }

            var receiverTarget = FindTargetByReceiver(receiver);
            return receiverTarget != null && IsEnemy(receiverTarget);
        }

        private bool IsOwnerOfTarget(TargetLockTarget target)
        {
            if (target == null)
            {
                return false;
            }

            var targetScope = target.GetComponentInParent<NpcLifetimeScope>();
            if (targetScope != null && targetScope == ownerScope)
            {
                return true;
            }

            var targetReceiver = target.GetComponentInParent<DamageReceiverHost>()?.Receiver;
            return targetReceiver != null && targetReceiver == ownerDamageReceiver;
        }

        private void RememberPersonalEnemy(TargetLockTarget target)
        {
            var damageReceiver = target != null
                ? target.GetComponentInParent<DamageReceiverHost>()?.Receiver
                : null;
            if (damageReceiver != null)
            {
                personalEnemies.Add(damageReceiver);
            }
        }

        private bool IsTargetAlive(TargetLockTarget target)
        {
            var receiver = target != null
                ? target.GetComponentInParent<DamageReceiverHost>()?.Receiver
                : null;
            return receiver != null && receiver.IsAlive;
        }

        private void SetTarget(TargetLockTarget target)
        {
            if (target != null && (!target.IsTargetable || !IsTargetAlive(target)))
            {
                return;
            }

            var previousTarget = CurrentTarget;
            CurrentTarget = target;
            if (target != previousTarget)
            {
                initialCircleEvaluatedTarget = null;
                consecutiveAttackDecisions = 0;
                ResetTargetMotion();
            }

            if (target != null)
            {
                LastKnownTargetPosition = target.transform.position;
                HasLastKnownTargetPosition = true;
            }

            if (target != null && target != previousTarget)
            {
                ScheduleAggressionNotification(target);
            }
        }

        private void ScheduleAggressionNotification(TargetLockTarget target)
        {
            notificationTarget = target;
            hasPendingAggressionNotification = true;
            aggressionNotificationTimer = combatConfig != null ? combatConfig.AggressionNotificationDelay : 1.2f;
        }

        private void ClearAggressionNotification()
        {
            notificationTarget = null;
            hasPendingAggressionNotification = false;
            aggressionNotificationTimer = 0f;
        }

        private void TickAggressionNotification()
        {
            if (!hasPendingAggressionNotification)
            {
                return;
            }

            if (statsController?.Hp?.Value?.Value <= 0f || notificationTarget == null || !notificationTarget.IsTargetable)
            {
                ClearAggressionNotification();
                return;
            }

            if (hitReactionController?.IsReacting == true)
            {
                aggressionNotificationTimer = combatConfig != null ? combatConfig.AggressionNotificationDelay : 1.2f;
                return;
            }

            aggressionNotificationTimer -= Time.deltaTime;
            if (aggressionNotificationTimer > 0f)
            {
                return;
            }

            NotifyNearbyNpcsAboutAggression();
            ClearAggressionNotification();
        }

        private void NotifyNearbyNpcsAboutAggression()
        {
            if (ownerTransform == null || notificationTarget == null)
            {
                return;
            }

            var radius = combatConfig != null ? combatConfig.AggressionNotificationRadius : 12f;
            var radiusSqr = radius * radius;
            foreach (var other in ActiveServices.ToArray())
            {
                if (other == null || other == this || other.ownerTransform == null)
                {
                    continue;
                }

                if ((other.ownerTransform.position - ownerTransform.position).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                var relation = GetRelationTo(other);
                if (relation == NpcFactionRelation.Hostile)
                {
                    continue;
                }

                other.ReceiveAggressionNotification(notificationTarget, relation == NpcFactionRelation.Friendly);
            }
        }

        private NpcFactionRelation GetRelationTo(NpcCombatService other)
        {
            var ownerFaction = ownerScope != null ? ownerScope.Faction : null;
            var otherFaction = other.ownerScope != null ? other.ownerScope.Faction : null;
            if (ownerFaction != null && ownerFaction == otherFaction)
            {
                return NpcFactionRelation.Friendly;
            }

            if (ownerFaction == null || otherFaction == null || factionRelationsConfig == null)
            {
                return NpcFactionRelation.Neutral;
            }

            if (factionRelationsConfig.IsHostile(ownerFaction, otherFaction))
            {
                return NpcFactionRelation.Hostile;
            }

            return factionRelationsConfig.IsFriendly(ownerFaction, otherFaction)
                ? NpcFactionRelation.Friendly
                : NpcFactionRelation.Neutral;
        }

        private void OnCharacterDamaged(CharacterDamagedMessage message)
        {
            if (message.Attacker == ownerDamageReceiver)
            {
                recentDamageDealt += message.FinalDamage;
                return;
            }

            if (message.CharacterTransform != ownerTransform || message.Attacker == null)
            {
                return;
            }

            recentDamageTaken += message.FinalDamage;
            personalEnemies.Add(message.Attacker);
            var attackerTarget = FindTargetByReceiver(message.Attacker);
            if (attackerTarget != null)
            {
                SetTarget(attackerTarget);
            }
            else
            {
                LastKnownTargetPosition = message.Attacker.OwnerTransform != null
                    ? message.Attacker.OwnerTransform.position
                    : message.Point;
                HasLastKnownTargetPosition = true;
            }

            hasPendingDamageFleeRequest = true;
        }

        private static TargetLockTarget FindTargetByReceiver(CharacterDamageReceiver receiver)
        {
            var targetFromOwner = receiver?.OwnerTransform != null
                ? receiver.OwnerTransform.GetComponentInParent<TargetLockTarget>()
                : null;
            if (targetFromOwner != null)
            {
                return targetFromOwner;
            }

            var hosts = UnityEngine.Object.FindObjectsByType<DamageReceiverHost>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (var host in hosts)
            {
                if (host != null && host.Receiver == receiver)
                {
                    return host.GetComponentInParent<TargetLockTarget>();
                }
            }

            return null;
        }

        private void FacePosition(Vector3 position)
        {
            var direction = position - ownerTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            ownerTransform.rotation = Quaternion.RotateTowards(
                ownerTransform.rotation,
                Quaternion.LookRotation(direction.normalized, Vector3.up),
                720f * Time.deltaTime);
        }
    }
}
