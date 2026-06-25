using System;
using System.Collections.Generic;
using CameraScripts;
using GameModes;
using MessagePipe;
using Messages;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace TargetLock
{
    public sealed class TargetLockController : IStartable, ITickable, IDisposable
    {
        private const float DirectionEpsilon = 0.001f;

        private readonly TargetLockConfig config;
        private readonly Camera camera;
        private readonly CameraMotor cameraMotor;
        private readonly Transform playerTransform;
        private readonly Transform visualTransform;
        private readonly ISubscriber<TargetLockInputMessage> targetLockInputSubscriber;
        private readonly ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber;
        private readonly CompositeDisposable disposables = new();

        private GameMode currentGameMode = GameMode.Game;
        private float invalidCurrentTargetTime;

        public TargetLockController(
            TargetLockConfig config,
            Camera camera,
            CameraMotor cameraMotor,
            Transform playerTransform,
            Animator animator,
            ISubscriber<TargetLockInputMessage> targetLockInputSubscriber,
            ISubscriber<GameModeChangedMessage> gameModeChangedSubscriber)
        {
            this.config = config;
            this.camera = camera;
            this.cameraMotor = cameraMotor;
            this.playerTransform = playerTransform;
            visualTransform = animator != null ? animator.transform : playerTransform;
            this.targetLockInputSubscriber = targetLockInputSubscriber;
            this.gameModeChangedSubscriber = gameModeChangedSubscriber;
        }

        public TargetLockTarget CurrentTarget { get; private set; }
        public TargetLockMode Mode { get; private set; } = TargetLockMode.Disabled;
        public bool IsLocked => CurrentTarget != null && Mode != TargetLockMode.Disabled;
        public bool IsHardLocked => CurrentTarget != null && Mode == TargetLockMode.Hard;
        public bool IsSoftLocked => CurrentTarget != null && Mode == TargetLockMode.Soft;

        public void Start()
        {
            targetLockInputSubscriber.Subscribe(OnTargetLockInput).AddTo(disposables);
            gameModeChangedSubscriber.Subscribe(OnGameModeChanged).AddTo(disposables);
        }

        public void Tick()
        {
            if (Mode == TargetLockMode.Disabled || CurrentTarget == null)
            {
                return;
            }

            if (ShouldBreakImmediately(CurrentTarget))
            {
                Unlock();
                return;
            }

            if (!IsMaintainedTarget(CurrentTarget))
            {
                invalidCurrentTargetTime += Time.deltaTime;
                if (invalidCurrentTargetTime >= config.LostTargetGraceSeconds)
                {
                    Unlock();
                }

                return;
            }

            invalidCurrentTargetTime = 0f;
            UpdateCameraTarget();

            if (Mode is TargetLockMode.Hard or TargetLockMode.Soft)
            {
                RotateVisualTowardsTarget(false);
            }
        }

        public void Dispose()
        {
            Unlock();
            disposables.Dispose();
        }

        public bool TryFaceAttackTarget()
        {
            if (Mode == TargetLockMode.Disabled)
            {
                return false;
            }

            if (CurrentTarget == null
             || ShouldBreakImmediately(CurrentTarget)
             || !IsMaintainedTarget(CurrentTarget))
            {
                if (!TryLockBestTarget(Mode))
                {
                    return false;
                }
            }

            return RotateVisualTowardsTarget(Mode == TargetLockMode.Hard);
        }

        private void OnTargetLockInput(TargetLockInputMessage message)
        {
            if (currentGameMode != GameMode.Game)
            {
                return;
            }

            switch (message.Command)
            {
                case TargetLockCommand.Toggle:
                    CycleMode();
                    break;
                case TargetLockCommand.Next:
                    SelectAdjacentTarget(1);
                    break;
                case TargetLockCommand.Previous:
                    SelectAdjacentTarget(-1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnGameModeChanged(GameModeChangedMessage message)
        {
            currentGameMode = message.GameMode;

            if (currentGameMode != GameMode.Game)
            {
                Unlock();
            }
        }

        private void CycleMode()
        {
            switch (Mode)
            {
                case TargetLockMode.Disabled:
                    TryLockBestTarget(TargetLockMode.Hard);
                    break;
                case TargetLockMode.Hard:
                    SwitchMode(TargetLockMode.Soft);
                    break;
                case TargetLockMode.Soft:
                    Unlock();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SwitchMode(TargetLockMode nextMode)
        {
            if (nextMode == TargetLockMode.Disabled)
            {
                Unlock();
                return;
            }

            if (CurrentTarget == null
             || ShouldBreakImmediately(CurrentTarget)
             || !IsMaintainedTarget(CurrentTarget))
            {
                TryLockBestTarget(nextMode);
                return;
            }

            Mode = nextMode;
            invalidCurrentTargetTime = 0f;
            UpdateCameraTarget();
        }

        private void SelectAdjacentTarget(int direction)
        {
            if (Mode == TargetLockMode.Disabled || CurrentTarget == null)
            {
                TryLockBestTarget(TargetLockMode.Hard);
                return;
            }

            var candidates = GetCandidates();
            if (candidates.Count <= 1)
            {
                return;
            }

            var currentViewport = camera.WorldToViewportPoint(CurrentTarget.AimPosition);
            TargetLockTarget selected = null;
            var selectedDelta = float.PositiveInfinity;
            TargetLockTarget wrapped = null;
            var wrappedViewportX = direction > 0 ? float.PositiveInfinity : float.NegativeInfinity;

            foreach (var candidate in candidates)
            {
                if (candidate == CurrentTarget)
                {
                    continue;
                }

                if (!TryGetScore(candidate, out _))
                {
                    continue;
                }

                var viewport = camera.WorldToViewportPoint(candidate.AimPosition);
                var delta = (viewport.x - currentViewport.x) * direction;

                if (delta > DirectionEpsilon && delta < selectedDelta)
                {
                    selectedDelta = delta;
                    selected = candidate;
                }

                if (direction > 0 && viewport.x < wrappedViewportX)
                {
                    wrappedViewportX = viewport.x;
                    wrapped = candidate;
                }
                else if (direction < 0 && viewport.x > wrappedViewportX)
                {
                    wrappedViewportX = viewport.x;
                    wrapped = candidate;
                }
            }

            Lock(selected != null ? selected : wrapped, Mode);
        }

        private bool TryLockBestTarget(TargetLockMode mode)
        {
            var candidates = GetCandidates();
            TargetLockTarget bestTarget = null;
            var bestScore = float.PositiveInfinity;

            foreach (var candidate in candidates)
            {
                if (!TryGetScore(candidate, out var score))
                {
                    continue;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            if (bestTarget == null)
            {
                return false;
            }

            Lock(bestTarget, mode);
            return true;
        }

        private List<TargetLockTarget> GetCandidates()
        {
            var targets = UnityEngine.Object.FindObjectsByType<TargetLockTarget>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var candidates = new List<TargetLockTarget>(targets.Length);

            foreach (var target in targets)
            {
                if (IsSearchCandidate(target))
                {
                    candidates.Add(target);
                }
            }

            return candidates;
        }

        private bool TryGetScore(TargetLockTarget target, out float score)
        {
            score = 0f;

            if (camera == null)
            {
                return false;
            }

            var targetPosition = target.AimPosition;
            var viewport = camera.WorldToViewportPoint(targetPosition);
            if (viewport.z <= 0f)
            {
                return false;
            }

            var directionFromCamera = targetPosition - camera.transform.position;
            var angle = Vector3.Angle(camera.transform.forward, directionFromCamera);
            if (angle > config.MaxScreenAngle)
            {
                return false;
            }

            if (GetPlayerAngle(target) > config.MaxPlayerAngle)
            {
                return false;
            }

            if (!HasLineOfSight(target))
            {
                return false;
            }

            var centerDistance = Vector2.Distance(
                new Vector2(viewport.x, viewport.y),
                new Vector2(0.5f, 0.5f));
            var distanceNormalized = Mathf.Clamp01(GetPlayerDistance(target) / Mathf.Max(config.SearchRadius, 0.01f));
            var facingNormalized = Mathf.Clamp01(GetPlayerAngle(target) / Mathf.Max(config.MaxPlayerAngle, 0.01f));

            score = centerDistance * config.CenterWeight
                  + distanceNormalized * config.DistanceWeight
                  + facingNormalized * config.PlayerFacingWeight;
            return true;
        }

        private bool IsSearchCandidate(TargetLockTarget target)
        {
            return IsValidTarget(target)
                && GetPlayerDistance(target) <= config.SearchRadius
                && HasLineOfSight(target);
        }

        private bool IsValidTarget(TargetLockTarget target)
        {
            return target != null && target.IsTargetable && target.transform != playerTransform;
        }

        private bool ShouldBreakImmediately(TargetLockTarget target)
        {
            return !IsValidTarget(target) || GetPlayerDistance(target) > config.BreakRadius;
        }

        private bool IsMaintainedTarget(TargetLockTarget target)
        {
            if (!HasLineOfSight(target))
            {
                return false;
            }

            if (Mode != TargetLockMode.Hard || camera == null)
            {
                return true;
            }

            var directionFromCamera = target.AimPosition - camera.transform.position;
            return Vector3.Angle(camera.transform.forward, directionFromCamera) <= config.BreakScreenAngle;
        }

        private float GetPlayerDistance(TargetLockTarget target)
        {
            return Vector3.Distance(playerTransform.position, target.AimPosition);
        }

        private float GetPlayerAngle(TargetLockTarget target)
        {
            var direction = target.AimPosition - playerTransform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= DirectionEpsilon)
            {
                return 0f;
            }

            var playerForward = visualTransform != null ? visualTransform.forward : playerTransform.forward;
            playerForward.y = 0f;

            if (playerForward.sqrMagnitude <= DirectionEpsilon)
            {
                return 0f;
            }

            return Vector3.Angle(playerForward.normalized, direction.normalized);
        }

        private bool HasLineOfSight(TargetLockTarget target)
        {
            var origin = playerTransform.position + config.LineOfSightOriginOffset;
            var targetPosition = target.AimPosition;
            var direction = targetPosition - origin;
            var distance = direction.magnitude;

            if (distance <= DirectionEpsilon)
            {
                return true;
            }

            var hits = Physics.RaycastAll(
                origin,
                direction / distance,
                distance,
                config.LineOfSightMask,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var hitTransform = hit.transform;
                if (hitTransform == null)
                {
                    continue;
                }

                if (hitTransform == playerTransform || hitTransform.IsChildOf(playerTransform))
                {
                    continue;
                }

                if (hitTransform == target.transform || hitTransform.IsChildOf(target.transform))
                {
                    return true;
                }

                return false;
            }

            return true;
        }

        private void Lock(TargetLockTarget target, TargetLockMode mode)
        {
            if (target == null || mode == TargetLockMode.Disabled)
            {
                return;
            }

            CurrentTarget = target;
            Mode = mode;
            invalidCurrentTargetTime = 0f;
            UpdateCameraTarget();
        }

        private void Unlock()
        {
            CurrentTarget = null;
            Mode = TargetLockMode.Disabled;
            invalidCurrentTargetTime = 0f;
            cameraMotor.SetTargetLockTarget(null);
        }

        private void UpdateCameraTarget()
        {
            cameraMotor.SetTargetLockTarget(Mode == TargetLockMode.Hard ? CurrentTarget?.AimTransform : null);
        }

        private bool RotateVisualTowardsTarget(bool immediate)
        {
            if (visualTransform == null || CurrentTarget == null)
            {
                return false;
            }

            var direction = CurrentTarget.AimPosition - visualTransform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= DirectionEpsilon)
            {
                return false;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            visualTransform.rotation = immediate
                ? targetRotation
                : Quaternion.RotateTowards(
                    visualTransform.rotation,
                    targetRotation,
                    config.FacingRotationSpeed * Time.deltaTime);
            return true;
        }
    }
}
